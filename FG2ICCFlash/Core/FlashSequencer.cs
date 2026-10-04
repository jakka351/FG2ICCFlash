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
    /// Runs on a background thread; raises Log/Progress/Completed (marshal to the UI thread in the
    /// handler). Honours Abort between steps and between transfer blocks. A real J2534 channel is
    /// used unless a channel factory is supplied (tests inject the simulator).
    /// </summary>
    public sealed class FlashSequencer
    {
        private readonly FlashOptions _opt;
        private readonly PhfFile _sbl;
        private readonly PhfFile _app;
        private readonly Func<ICanChannel> _factory;
        private volatile bool _abort;
        private Thread _thread;

        public event Action<string> Log;
        public event Action<int, string> Progress;     // percent (0-100), phase label
        public event Action<bool, string> Completed;    // success, message

        public FlashSequencer(FlashOptions options, PhfFile secondaryBootloader, PhfFile application,
                              Func<ICanChannel> channelFactory = null)
        {
            _opt = options;
            _sbl = secondaryBootloader;
            _app = application;
            _factory = channelFactory;
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

        private void Run()
        {
            GdsSession session = null;
            try
            {
                if (_app == null) { Done(false, "No application firmware loaded."); return; }

                L("=====================================================");
                L("LIVE FLASH — the Front Display Interface Module (0x7A6) will be reprogrammed.");
                L($"Target: TX 0x{_opt.TxId:X3} / RX 0x{_opt.RxId:X3} on {_opt.Bus}.");
                L($"Application: {_app.FileName}  ({_app.TotalDataBytes} bytes, 0x{_app.StartAddress:X8}..0x{_app.EndAddress - 1:X8}), DFI 0x{_opt.DataFormatIdentifier:X2}.");
                if (_opt.DownloadSbl && _sbl != null)
                    L($"Flash driver: {_sbl.FileName}  ({_sbl.TotalDataBytes} bytes, 0x{_sbl.StartAddress:X8}..0x{_sbl.EndAddress - 1:X8}).");
                L("=====================================================");

                P(0, "Connecting");
                session = new GdsSession(_opt, L, _factory);

                // Connect + programming session + security + tester-present in one step.
                string err;
                P(5, "Session & security");
                if (!session.Open(true, _opt.ProgrammingSession, true, true, out err)) { Done(false, err); return; }
                if (Aborted()) return;

                var gds = session.Gds;

                // Download the secondary bootloader / flash driver to RAM.
                if (_opt.DownloadSbl && _sbl != null)
                {
                    L("[SBL] Downloading flash driver to RAM...");
                    if (!DownloadImage(gds, _sbl, 15, 30, "Flash driver")) { Done(false, "Flash driver download failed (see log)."); return; }
                    L("[SBL] Flash driver resident (module executes it on transfer-exit).");
                    if (Aborted()) return;
                }

                // Erase flash.
                if (_opt.EraseBeforeAppDownload)
                {
                    P(32, "Erasing flash");
                    L("[$B1 00 B2] Flash Memory Erase (can take several seconds; $78 pending expected)...");
                    var er = gds.FlashErase(_opt.EraseTimeoutMs);
                    if (!er.Positive) { Done(false, "Flash erase failed: " + er.Describe()); return; }
                    L("[$B1 00 B2] Erase complete.");
                    if (Aborted()) return;
                }

                // Download the application.
                L("[APP] Downloading application...");
                if (!DownloadImage(gds, _app, 35, 90, "Programming application")) { Done(false, "Application download failed (see log)."); return; }
                if (Aborted()) return;

                // Verify.
                if (_opt.VerifyAfter)
                {
                    P(92, "Verifying");
                    L($"[verify] Header FILE CHECKSUM = 0x{_app.FileChecksum:X4}; additive-16 over data = 0x{_app.ComputeAdditiveChecksum16():X4}.");
                    L("[verify] The ECU validates the application on RequestTransferExit ($37) / ECUReset ($11).");
                }

                // ECU reset.
                if (_opt.EcuResetAfter)
                {
                    P(96, "Resetting ECU");
                    L("[$11 01] ECUReset (hardReset)");
                    var rr = gds.EcuReset(0x01, _opt.P2TimeoutMs);
                    L("[$11 01] " + rr.Describe());
                }

                P(100, "Done");
                Done(true, "Flash completed successfully.");
            }
            catch (Exception ex)
            {
                Done(false, "Exception: " + ex.Message);
            }
            finally
            {
                session?.Dispose();
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

        private bool Aborted()
        {
            if (!_abort) return false;
            Done(false, "Aborted by user.");
            return true;
        }

        private void Done(bool ok, string msg) => Completed?.Invoke(ok, msg);
    }
}
