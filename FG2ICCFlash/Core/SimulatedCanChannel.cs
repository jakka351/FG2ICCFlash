using System;
using System.Collections.Generic;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Offline simulator implementing ICanChannel. It answers each GDS service with a plausible
    /// positive response (request SID + 0x40), so the full flash sequence — including the $78
    /// "response pending" loop during Flash Erase — can be exercised and traced without a vehicle
    /// or adapter. It does NOT validate keys or program anything.
    /// </summary>
    public sealed class SimulatedCanChannel : ICanChannel
    {
        private bool _open;
        private readonly Queue<byte[]> _pending = new Queue<byte[]>();

        // A fixed seed so the computed key is deterministic in dry-runs (matches a known vector).
        private static readonly byte[] SimSeed = { 0xAF, 0xBB, 0x7F };

        public bool IsOpen => _open;
        public uint TxId { get; private set; }
        public uint RxId { get; private set; }

        public event Action<string> Log;
        public event Action<string, byte[]> Frame;

        public bool Open(string deviceName, CanBus bus, uint txId, uint rxId)
        {
            TxId = txId; RxId = rxId; _open = true;
            Log?.Invoke($"[SIM] Simulated channel open: {bus}, TX 0x{txId:X3} / RX 0x{rxId:X3}. No hardware in use.");
            return true;
        }

        public byte[] SendReceive(byte[] uds, int timeoutMs)
        {
            Frame?.Invoke("TX", uds);
            var rsp = BuildResponse(uds);
            if (rsp != null) Frame?.Invoke("RX", rsp);
            return rsp;
        }

        public byte[] ReadNext(int timeoutMs)
        {
            if (_pending.Count > 0)
            {
                var r = _pending.Dequeue();
                Frame?.Invoke("RX", r);
                return r;
            }
            return null;
        }

        private byte[] BuildResponse(byte[] uds)
        {
            if (uds == null || uds.Length == 0) return null;
            byte sid = uds[0];
            switch (sid)
            {
                case 0x10: // StartDiagnosticSession -> 50 [mode]
                    return new byte[] { 0x50, uds.Length > 1 ? uds[1] : (byte)0x00 };
                case 0x27: // SecurityAccess
                    if (uds.Length >= 2 && (uds[1] & 0x01) == 1) // odd sub-function = requestSeed
                        return new byte[] { 0x67, uds[1], SimSeed[0], SimSeed[1], SimSeed[2] };
                    return new byte[] { 0x67, uds.Length > 1 ? uds[1] : (byte)0x02 }; // sendKey -> granted
                case 0x34: // RequestDownload -> 74 [maxNumberOfBlockLength MSB LSB]; 0x0100 = 256
                    return new byte[] { 0x74, 0x01, 0x00 };
                case 0x36: // TransferData -> 76
                    return new byte[] { 0x76 };
                case 0x37: // RequestTransferExit -> 77
                    return new byte[] { 0x77 };
                case 0xB1: // diagnosticCommand (Flash Erase 00 B2): respond pending, then positive
                    if (uds.Length >= 3 && uds[1] == 0x00 && uds[2] == 0xB2)
                    {
                        _pending.Enqueue(new byte[] { 0xF1, 0x00, 0xB2 });
                        return new byte[] { 0x7F, 0xB1, Nrc.ResponsePending };
                    }
                    return new byte[] { 0xF1, uds.Length > 1 ? uds[1] : (byte)0x00, uds.Length > 2 ? uds[2] : (byte)0x00 };
                case 0x31: // StartRoutine -> 71 [routine...]
                    {
                        var r = new byte[Math.Min(uds.Length, 3)];
                        r[0] = 0x71;
                        if (uds.Length > 1) r[1] = uds[1];
                        if (uds.Length > 2) r[2] = uds[2];
                        return r;
                    }
                case 0x11: // ECUReset -> 51 [sub]
                    return new byte[] { 0x51, uds.Length > 1 ? uds[1] : (byte)0x01 };
                case 0x22: // ReadDataByIdentifier -> 62 [did] ASCII "AR79-14D017-SIM"
                    {
                        var ascii = System.Text.Encoding.ASCII.GetBytes("AR79-14D017-SIM ");
                        var r = new byte[3 + ascii.Length];
                        r[0] = 0x62; r[1] = uds.Length > 1 ? uds[1] : (byte)0; r[2] = uds.Length > 2 ? uds[2] : (byte)0;
                        Buffer.BlockCopy(ascii, 0, r, 3, ascii.Length);
                        return r;
                    }
                case 0x2E: // WriteDataByIdentifier -> 6E [did]
                    return new byte[] { 0x6E, uds.Length > 1 ? uds[1] : (byte)0, uds.Length > 2 ? uds[2] : (byte)0 };
                case 0x32: // stopRoutine -> 72 [routine]
                    return new byte[] { 0x72, uds.Length > 1 ? uds[1] : (byte)0 };
                case 0x33: // requestRoutineResults -> 73 [routine][status=00 passed]
                    return new byte[] { 0x73, uds.Length > 1 ? uds[1] : (byte)0, 0x00 };
                case 0x18: // readDTCByStatus -> 58 [numberOfDTC=0]
                    return new byte[] { 0x58, 0x00 };
                case 0x14: // clearDiagnosticInformation -> 54
                    return new byte[] { 0x54 };
                case 0x3E: // TesterPresent -> 7E (usually suppressed)
                    return new byte[] { 0x7E, 0x00 };
                default:
                    return new byte[] { 0x7F, sid, 0x11 }; // service not supported
            }
        }

        public bool StartTesterPresent(uint intervalMs) { Log?.Invoke($"[SIM] TesterPresent every {intervalMs} ms."); return true; }
        public void StopTesterPresent() { }
        public double ReadBatteryVoltage() => 13.8; // simulate a healthy battery
        public void Close() { _open = false; Log?.Invoke("[SIM] Simulated channel closed."); }
        public void Dispose() => Close();
    }
}
