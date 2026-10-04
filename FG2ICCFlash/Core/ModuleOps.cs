using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// One-shot diagnostic / routine operations on the FDIM (0x7A6), built on GdsClient.
    /// The GDS-mandated execution routines are On-Demand Self Test (routineLocalId $02) and
    /// Assembly/EOL Self Test ($11); the FG-specific Recore routine is $AB (option $01), which
    /// commands the module to re-image its OS from USB. Each method assumes the caller has
    /// already opened a GdsSession with the appropriate session/security preamble.
    /// </summary>
    public static class ModuleOps
    {
        // Routine local identifiers.
        public const byte RoutineOnDemandSelfTest = 0x02;   // GDS mandatory
        public const byte RoutineAssemblySelfTest = 0x11;   // GDS assembly / EOL self-test
        public const byte RoutineRecore = 0xAB;             // FG FDIM re-image from USB
        public const byte RecoreOption = 0x01;

        private static readonly (ushort did, string name)[] InfoDids =
        {
            (0xF188, "Mfr ECU software number"),
            (0xE611, "Strategy software part number"),
            (0xE610, "Hardware part number"),
            (0xF110, "On-line diagnostic DB reference"),
            (0xF111, "ECU core assembly number"),
            (0xF113, "ECU delivery assembly number"),
            (0xE21A, "Part number identification"),
            (0xE6F2, "Config & programming version"),
            (0xE6F3, "CAN diagnostic spec version"),
        };

        public static void ReadModuleInfo(GdsClient gds, FlashOptions opt, Action<string> log)
        {
            log("Reading module identification DIDs...");
            foreach (var d in InfoDids)
            {
                var r = gds.ReadDid(d.did, opt.P2TimeoutMs);
                if (r.Positive && r.Data.Length > 3)
                {
                    var payload = r.Data.Skip(3).ToArray();
                    log($"  DID 0x{d.did:X4} {d.name,-32}: \"{Ascii(payload)}\"  [{HexUtil.ToHex(payload)}]");
                }
                else
                {
                    log($"  DID 0x{d.did:X4} {d.name,-32}: {r.Describe()}");
                }
            }
        }

        public static void RunSelfTest(GdsClient gds, FlashOptions opt, Action<string> log, byte routineId, string label, Func<bool> cancelled = null)
        {
            log($"[$31 {routineId:X2}] Starting {label}...");
            var start = gds.StartRoutine(new byte[] { routineId, 0x00 }, opt.RoutineTimeoutMs);
            if (!start.Positive) { log($"  Could not start {label}: " + start.Describe()); return; }

            log("  Routine running — polling for results ($33)...");
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < opt.RoutineTimeoutMs)
            {
                if (cancelled != null && cancelled()) { log("  Cancelled."); return; }
                var res = gds.RequestRoutineResults(new byte[] { routineId }, opt.P2TimeoutMs);
                if (res.Positive) { ReportRoutineResults(res.Data, routineId, label, log); return; }
                if (!res.TimedOut && res.NrcCode == 0x21) { Thread.Sleep(250); continue; } // busy-repeatRequest
                log("  Routine results: " + res.Describe());
                return;
            }
            log($"  {label} timed out waiting for results.");
        }

        private static void ReportRoutineResults(byte[] data, byte routineId, string label, Action<string> log)
        {
            // $73 [routineId] [status] [DTC hi][DTC lo]...
            int idx = 1;
            if (data.Length > idx && data[idx] == routineId) idx++;
            byte status = data.Length > idx ? data[idx++] : (byte)0xFF;
            string statusText;
            switch (status)
            {
                case 0x00: statusText = "COMPLETED - PASSED"; break;
                case 0x01: statusText = "COMPLETED - FAILED (DTCs set)"; break;
                case 0x02: statusText = "ABORTED"; break;
                default: statusText = $"status 0x{status:X2}"; break;
            }
            log($"  {label} result: {statusText}");

            var dtcs = new StringBuilder();
            int count = 0;
            for (int i = idx; i + 1 < data.Length; i += 2)
            {
                ushort dtc = (ushort)((data[i] << 8) | data[i + 1]);
                if (dtc == 0) continue;
                dtcs.Append((count++ > 0 ? ", " : "") + dtc.ToString("X4"));
            }
            if (count > 0) log($"  DTCs reported: {dtcs}");
            else if (status == 0x00) log("  No DTCs reported.");
        }

        public static void Recore(GdsClient gds, FlashOptions opt, Action<string> log)
        {
            log($"[$31 {RoutineRecore:X2} {RecoreOption:X2}] Recore — commanding module to re-image its OS from USB.");
            var r = gds.StartRoutine(new byte[] { RoutineRecore, RecoreOption }, opt.RoutineTimeoutMs);
            log("  Recore routine: " + r.Describe());
            if (r.Positive)
                log("  Module is re-imaging from the USB stick. Keep power stable; do not disconnect until it completes.");
        }

        public static void ReadDtcs(GdsClient gds, FlashOptions opt, Action<string> log)
        {
            log("[$18 00 FF 00] ReadDiagnosticTroubleCodesByStatus (all groups)...");
            var r = gds.ReadDtcsByStatus(0x00, 0xFF00, opt.P2TimeoutMs);
            if (!r.Positive) { log("  " + r.Describe()); return; }

            // $58 [numberOfDTC] then per DTC: [DTC hi][DTC lo][status]
            byte num = r.Data.Length > 1 ? r.Data[1] : (byte)0;
            log($"  {num} DTC(s) reported.");
            int i = 2, shown = 0;
            while (i + 2 < r.Data.Length)
            {
                ushort dtc = (ushort)((r.Data[i] << 8) | r.Data[i + 1]);
                byte st = r.Data[i + 2];
                log($"    DTC {dtc:X4}  status 0x{st:X2}");
                i += 3; shown++;
            }
            if (shown == 0 && num == 0) log("    (no stored DTCs)");
        }

        public static void ClearDtcs(GdsClient gds, FlashOptions opt, Action<string> log)
        {
            log("[$14 FF 00] ClearDiagnosticInformation (all groups)...");
            var r = gds.ClearDtcs(0xFF00, opt.P2TimeoutMs);
            log("  " + r.Describe());
        }

        private static string Ascii(byte[] data)
            => new string(data.Select(b => (b >= 0x20 && b < 0x7F) ? (char)b : '.').ToArray()).Trim();
    }
}
