using System;
using System.Threading;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Orchestrates the end-to-end CAN GDS v2003 "Method 3" reprogramming of the FDIM (0x7A6):
    /// connect -> programming session ($10 85) -> security access ($27, Janis) -> download the
    /// secondary bootloader/flash driver to RAM ($34/$36/$37) -> erase flash ($B1 00 B2) ->
    /// download the application ($34/$36/$37) -> verify -> ECU reset ($11 01).
    ///
    /// Runs on a background thread; raises Log/Progress/Completed events (marshal to the UI thread
    /// in the handler). Honours Abort between steps and between transfer blocks. In DryRun the whole
    /// sequence runs against the offline simulator so nothing is written to a module.
    /// </summary>
    public sealed class FlashSequencer
    {
        private readonly FlashOptions _opt;
        private readonly PhfFile _sbl;
        private readonly PhfFile _app;
        private volatile bool _abort;
        private Thread _thread;

        public event Action<string> Log;
        public event Action<int, string> Progress;     // percent (0-100), phase label
        public event Action<bool, string> Completed;    // success, message

        public FlashSequencer(FlashOptions options, PhfFile secondaryBootloader, PhfFile application)
        {
            _opt = options;
            _sbl = secondaryBootloader;
            _app = application;
        }

        public bool IsRunning => _thread != null && _thread.IsAlive;
        public void Abort() => _abort = true;

        public void Start()
        {
            _abort = false;
            _thread = new Thread(Run) { IsBackground = true, Name = "FlashSequencer" };
            _thread.Start();
        }

        private void L(string m) => Log?.Invoke(m);
        private void P(int pct, string phase) => Progress?.Invoke(pct, phase);

        private ICanChannel MakeChannel()
            => _opt.DryRun ? (ICanChannel)new SimulatedCanChannel() : new J2534CanChannel();

        private void Run()
        {
            ICanChannel ch = null;
            bool tpStarted = false;
            try
            {
                if (_app == null) { Done(false, "No application firmware loaded."); return; }

                L("=====================================================");
                L(_opt.DryRun ? "DRY RUN — simulator only, nothing will be written." : "LIVE FLASH — a real module will be reprogrammed.");
                L($"Target: FDIM TX 0x{_opt.TxId:X3} / RX 0x{_opt.RxId:X3} on {_opt.Bus}.");
                L($"Application: {_app.FileName}  ({_app.TotalDataBytes} bytes, 0x{_app.StartAddress:X8}..0x{_app.EndAddress - 1:X8}), DFI 0x{_opt.DataFormatIdentifier:X2}.");
                if (_opt.DownloadSbl && _sbl != null)
                    L($"Flash driver: {_sbl.FileName}  ({_sbl.TotalDataBytes} bytes, 0x{_sbl.StartAddress:X8}..0x{_sbl.EndAddress - 1:X8}).");
                L("=====================================================");

                ch = MakeChannel();
                ch.Log += L;
                ch.Frame += (dir, payload) => L($"  {dir}: {HexUtil.ToHex(payload)}");

                P(0, "Connecting");
                if (!ch.Open(_opt.DeviceName, _opt.Bus, _opt.TxId, _opt.RxId)) { Done(false, "Failed to open J2534 channel."); return; }
                if (Aborted(ref ch)) return;

                double volts = ch.ReadBatteryVoltage();
                if (volts > 0)
                {
                    L($"Battery voltage: {volts:0.0} V");
                    if (volts < _opt.MinBatteryVolts)
                        L($"WARNING: battery {volts:0.0} V is below {_opt.MinBatteryVolts:0.0} V. A drop-out during erase/program can brick the module.");
                }

                var gds = new GdsClient(ch);
                gds.Log += L;

                // 1) Programming session
                P(5, "Programming session");
                L($"[$10] StartDiagnosticSession 0x{_opt.ProgrammingSession:X2} (ECUProgrammingMode)");
                var r = gds.StartDiagnosticSession(_opt.ProgrammingSession, _opt.P2TimeoutMs);
                if (!r.Positive) { Done(false, "Could not enter programming session: " + r.Describe()); return; }
                if (Aborted(ref ch)) return;

                // 2) Security access
                P(10, "Security access");
                L($"[$27] SecurityAccess level 0x{_opt.SecurityLevel:X2} using key-word {HexUtil.ToHex(_opt.KeyWord)}");
                string secErr;
                if (!gds.SecurityAccess(_opt.SecurityLevel, _opt.KeyWord, _opt.P2TimeoutMs, out secErr))
                { Done(false, "Security access failed: " + secErr); return; }
                if (Aborted(ref ch)) return;

                // 3) Tester present keep-alive
                ch.StartTesterPresent(_opt.TesterPresentIntervalMs);
                tpStarted = true;

                // 4) Download the secondary bootloader / flash driver to RAM
                if (_opt.DownloadSbl && _sbl != null)
                {
                    L("[SBL] Downloading flash driver to RAM...");
                    if (!DownloadImage(gds, _sbl, 15, 30, "Flash driver")) { Done(false, "Flash driver download failed (see log)."); return; }
                    L("[SBL] Flash driver resident. (Module is expected to execute it on transfer-exit.)");
                    if (Aborted(ref ch)) return;
                }

                // 5) Erase flash
                if (_opt.EraseBeforeAppDownload)
                {
                    P(32, "Erasing flash");
                    L("[$B1 00 B2] Flash Memory Erase (this can take several seconds; $78 pending expected)...");
                    var er = gds.FlashErase(_opt.EraseTimeoutMs);
                    if (!er.Positive) { Done(false, "Flash erase failed: " + er.Describe()); return; }
                    L("[$B1 00 B2] Erase complete.");
                    if (Aborted(ref ch)) return;
                }

                // 6) Download the application
                L("[APP] Downloading application...");
                if (!DownloadImage(gds, _app, 35, 90, "Programming application")) { Done(false, "Application download failed (see log)."); return; }
                if (Aborted(ref ch)) return;

                // 7) Verify
                if (_opt.VerifyAfter)
                {
                    P(92, "Verifying");
                    L($"[verify] Header FILE CHECKSUM = 0x{_app.FileChecksum:X4}; additive-16 over data = 0x{_app.ComputeAdditiveChecksum16():X4}.");
                    L("[verify] Application validation is performed by the ECU on RequestTransferExit ($37) / ECUReset ($11).");
                }

                // 8) ECU reset
                if (_opt.EcuResetAfter)
                {
                    P(96, "Resetting ECU");
                    L("[$11 01] ECUReset (hardReset)");
                    var rr = gds.EcuReset(0x01, _opt.P2TimeoutMs);
                    L("[$11 01] " + rr.Describe());
                }

                ch.StopTesterPresent(); tpStarted = false;
                P(100, "Done");
                Done(true, _opt.DryRun
                    ? "Dry run completed successfully. Review the frame trace, then uncheck Dry Run to flash for real."
                    : "Flash completed successfully.");
            }
            catch (Exception ex)
            {
                Done(false, "Exception: " + ex.Message);
            }
            finally
            {
                try { if (ch != null && tpStarted) ch.StopTesterPresent(); } catch { }
                try { ch?.Close(); } catch { }
            }
        }

        /// <summary>Download one image via $34/$36.../$37, mapping progress into [pctFrom, pctTo].</summary>
        private bool DownloadImage(GdsClient gds, PhfFile phf, int pctFrom, int pctTo, string phase)
        {
            var flat = phf.Image.Flatten(_opt.FillByte);
            uint addr = phf.StartAddress;
            uint size = (uint)flat.Length;
            if (size == 0) { L("  Image is empty — nothing to download."); return false; }

            L($"[$34] RequestDownload addr=0x{addr:X8} size=0x{size:X6} ({size} bytes) DFI=0x{_opt.DataFormatIdentifier:X2}");
            UdsResult rd;
            int maxBlk = gds.RequestDownload(addr, size, _opt.DataFormatIdentifier, _opt.P2TimeoutMs, out rd);
            if (!rd.Positive) { L("  RequestDownload rejected: " + rd.Describe()); return false; }

            int blockData = _opt.BlockDataSizeOverride > 0
                ? _opt.BlockDataSizeOverride
                : (maxBlk > 1 ? maxBlk - 1 : 255);          // reserve 1 byte for the $36 SID
            if (blockData < 1) blockData = 1;
            if (blockData > 4093) blockData = 4093;          // GDS TransferData max 4094 incl. SID
            L($"[$34] maxNumberOfBlockLength=0x{maxBlk:X4}; using {blockData} data bytes/block.");

            int offset = 0, blockNo = 0;
            while (offset < flat.Length)
            {
                if (_abort) { L("  Aborted by user."); return false; }
                int n = Math.Min(blockData, flat.Length - offset);
                var block = new byte[n];
                Buffer.BlockCopy(flat, offset, block, 0, n);
                var tr = gds.TransferData(block, _opt.TransferTimeoutMs);
                if (!tr.Positive) { L($"  TransferData block {blockNo} (offset 0x{offset:X6}) failed: " + tr.Describe()); return false; }

                offset += n; blockNo++;
                int pct = pctFrom + (int)((long)(pctTo - pctFrom) * offset / flat.Length);
                P(pct, $"{phase} {offset}/{flat.Length} bytes");
            }
            L($"[$36] {blockNo} blocks transferred ({flat.Length} bytes).");

            L("[$37] RequestTransferExit");
            var ex = gds.RequestTransferExit(_opt.RoutineTimeoutMs);
            if (!ex.Positive) { L("  RequestTransferExit rejected: " + ex.Describe()); return false; }
            L("[$37] Transfer exit accepted.");
            return true;
        }

        private bool Aborted(ref ICanChannel ch)
        {
            if (!_abort) return false;
            try { ch?.StopTesterPresent(); } catch { }
            Done(false, "Aborted by user.");
            return true;
        }

        private void Done(bool ok, string msg) => Completed?.Invoke(ok, msg);
    }
}
