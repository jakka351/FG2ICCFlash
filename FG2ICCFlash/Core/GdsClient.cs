using System;

namespace FG2ICCFlasher.Core
{
    /// <summary>Result of a single GDS/UDS request.</summary>
    public sealed class UdsResult
    {
        public bool Positive;      // true = positive response received
        public bool TimedOut;      // true = no response / read error
        public byte NrcCode;       // negative response code when !Positive && !TimedOut
        public byte[] Data;        // full response payload (SID + params)

        public static UdsResult Pos(byte[] data) => new UdsResult { Positive = true, Data = data };
        public static UdsResult Neg(byte nrc, byte[] data) => new UdsResult { Positive = false, NrcCode = nrc, Data = data };
        public static UdsResult Timeout() => new UdsResult { Positive = false, TimedOut = true };

        public string Describe()
        {
            if (Positive) return "positive (" + HexUtil.ToHex(Data) + ")";
            if (TimedOut) return "no response / timeout";
            return "negative " + Nrc.Describe(NrcCode);
        }
    }

    /// <summary>
    /// GDS v2003 (KWP2000-over-CAN) diagnostic service client over an ICanChannel. Handles the
    /// $78 "response pending" wait loop and decodes $7F negative responses. All multi-byte values
    /// are big-endian per the spec. The channel strips the CAN-id header, so payloads here start
    /// at the service id.
    /// </summary>
    public sealed class GdsClient
    {
        private readonly ICanChannel _ch;
        public event Action<string> Log;
        private void L(string m) => Log?.Invoke(m);

        public GdsClient(ICanChannel channel) { _ch = channel; }

        /// <summary>Send a request and resolve the response, following $78 pending until final.</summary>
        public UdsResult Request(byte[] uds, int timeoutMs)
        {
            byte reqSid = uds.Length > 0 ? uds[0] : (byte)0;
            byte[] rsp = _ch.SendReceive(uds, timeoutMs);
            int pendingGuard = 0;
            while (true)
            {
                if (rsp == null || rsp.Length == 0) return UdsResult.Timeout();

                if (rsp[0] == 0x7F)
                {
                    byte nrc = rsp.Length >= 3 ? rsp[2] : (byte)0x00;
                    if (Nrc.IsResponsePending(nrc))
                    {
                        if (++pendingGuard > 2000) return UdsResult.Timeout(); // safety bound
                        L($"  ... $78 response pending (service ${reqSid:X2}), waiting...");
                        rsp = _ch.ReadNext(timeoutMs);
                        continue;
                    }
                    return UdsResult.Neg(nrc, rsp);
                }

                // Positive response SID = request SID + 0x40.
                if (reqSid != 0 && rsp[0] == (byte)(reqSid + 0x40))
                    return UdsResult.Pos(rsp);

                // Unexpected SID: accept if it looks positive, else report raw.
                return UdsResult.Pos(rsp);
            }
        }

        // ---- Services ----

        public UdsResult StartDiagnosticSession(byte mode, int timeoutMs)
            => Request(new byte[] { 0x10, mode }, timeoutMs);

        public UdsResult EcuReset(byte sub, int timeoutMs)
            => Request(new byte[] { 0x11, sub }, timeoutMs);

        public UdsResult TesterPresent(int timeoutMs)
            => Request(new byte[] { 0x3E, 0x01 }, timeoutMs);

        public UdsResult ReadDid(ushort did, int timeoutMs)
            => Request(new byte[] { 0x22, (byte)(did >> 8), (byte)did }, timeoutMs);

        public UdsResult WriteDid(ushort did, byte[] data, int timeoutMs)
            => Request(HexUtil.Concat(new byte[] { 0x2E, (byte)(did >> 8), (byte)did }, data ?? new byte[0]), timeoutMs);

        public UdsResult StartRoutine(byte[] routineAndArgs, int timeoutMs)
            => Request(HexUtil.Concat(new byte[] { 0x31 }, routineAndArgs ?? new byte[0]), timeoutMs);

        /// <summary>stopRoutineByLocalIdentifier ($32).</summary>
        public UdsResult StopRoutine(byte[] routineAndArgs, int timeoutMs)
            => Request(HexUtil.Concat(new byte[] { 0x32 }, routineAndArgs ?? new byte[0]), timeoutMs);

        /// <summary>requestRoutineResultsByLocalIdentifier ($33) -> $73 [routineId][status][results...].</summary>
        public UdsResult RequestRoutineResults(byte[] routineId, int timeoutMs)
            => Request(HexUtil.Concat(new byte[] { 0x33 }, routineId ?? new byte[0]), timeoutMs);

        /// <summary>readDiagnosticTroubleCodesByStatus ($18). Default status 0x00, group 0xFF00 (all).</summary>
        public UdsResult ReadDtcsByStatus(byte statusOfDtc, ushort groupOfDtc, int timeoutMs)
            => Request(new byte[] { 0x18, statusOfDtc, (byte)(groupOfDtc >> 8), (byte)groupOfDtc }, timeoutMs);

        /// <summary>clearDiagnosticInformation ($14). Group 0xFF00 = all groups.</summary>
        public UdsResult ClearDtcs(ushort groupOfDtc, int timeoutMs)
            => Request(new byte[] { 0x14, (byte)(groupOfDtc >> 8), (byte)groupOfDtc }, timeoutMs);

        /// <summary>Flash Memory Erase: diagnosticCommand $B1 with commandCommonIdentifier $00B2.</summary>
        public UdsResult FlashErase(int timeoutMs)
            => Request(new byte[] { 0xB1, 0x00, 0xB2 }, timeoutMs);

        /// <summary>
        /// SecurityAccess seed/key exchange. Returns true on grant (or if already unlocked).
        /// requestSeed sub-function = level (odd); sendKey sub-function = level+1.
        /// </summary>
        public bool SecurityAccess(byte level, byte[] keyWord, int timeoutMs, out string error)
        {
            error = null;
            var seedRsp = Request(new byte[] { 0x27, level }, timeoutMs);
            if (!seedRsp.Positive) { error = "requestSeed failed: " + seedRsp.Describe(); return false; }
            if (seedRsp.Data.Length < 5) { error = "seed response too short: " + HexUtil.ToHex(seedRsp.Data); return false; }

            var seed = new byte[] { seedRsp.Data[2], seedRsp.Data[3], seedRsp.Data[4] };
            L($"  Seed: {HexUtil.ToHex(seed)}");
            if (seed[0] == 0 && seed[1] == 0 && seed[2] == 0)
            {
                L("  Seed is 0x000000 -> module already unlocked.");
                return true;
            }

            var key = FordSecurity.ComputeKey(seed, keyWord);
            L($"  Key:  {HexUtil.ToHex(key)}");
            var keyRsp = Request(HexUtil.Concat(new byte[] { 0x27, (byte)(level + 1) }, key), timeoutMs);
            if (keyRsp.Positive) { L("  SecurityAccess granted."); return true; }
            error = "sendKey rejected: " + keyRsp.Describe();
            return false;
        }

        /// <summary>
        /// RequestDownload ($34): 4-byte big-endian address, dataFormatIdentifier, 3-byte big-endian
        /// uncompressed size. On success returns maxNumberOfBlockLength (from the $74 response), else 0.
        /// </summary>
        public int RequestDownload(uint address, uint size, byte dfi, int timeoutMs, out UdsResult result)
        {
            var req = HexUtil.Concat(new byte[] { 0x34 }, HexUtil.BE32(address), new byte[] { dfi }, HexUtil.BE24(size));
            result = Request(req, timeoutMs);
            if (!result.Positive) return 0;
            if (result.Data.Length >= 3) return (result.Data[1] << 8) | result.Data[2];
            return 0;
        }

        /// <summary>TransferData ($36): raw data block (no block-sequence-counter in GDS/KWP).</summary>
        public UdsResult TransferData(byte[] block, int timeoutMs)
            => Request(HexUtil.Concat(new byte[] { 0x36 }, block), timeoutMs);

        /// <summary>RequestTransferExit ($37): no parameters. Triggers checksum/validation on the ECU.</summary>
        public UdsResult RequestTransferExit(int timeoutMs)
            => Request(new byte[] { 0x37 }, timeoutMs);
    }
}
