using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using J2534;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Real ISO 15765 (ISO-TP) channel over a SAE J2534 PassThru adapter, addressed to a single
    /// ECU (txId/rxId). Reproduces the proven connect / flow-control-filter / send-receive sequence:
    /// MS-CAN uses ISO15765_PS @ 125 kbps with J1962_PINS = 0x030B (pins 3 &amp; 11); HS-CAN uses
    /// plain ISO15765 @ 500 kbps (pins 6 &amp; 14). Frame payloads are prefixed with the 4-byte
    /// big-endian CAN id {0x00,0x00,hi,lo}; responses are returned with that prefix stripped.
    /// </summary>
    public sealed class J2534CanChannel : ICanChannel
    {
        private sealed class Port
        {
            public J2534FunctionsExtended Functions = new J2534FunctionsExtended();
            public J2534Device LoadedDevice = new J2534Device();
        }

        private readonly Port _port = new Port();
        private readonly object _io = new object();

        private uint _deviceId;
        private uint _channelId;
        private int _filterId;
        private int _tpMsgId = -1;
        private bool _open;
        private ProtocolID _protocol = ProtocolID.ISO15765;

        public bool IsOpen => _open;
        public uint TxId { get; private set; }
        public uint RxId { get; private set; }

        public event Action<string> Log;
        public event Action<string, byte[]> Frame;

        private void L(string m) => Log?.Invoke(m);

        public bool Open(string deviceName, CanBus bus, uint txId, uint rxId)
        {
            TxId = txId; RxId = rxId;

            var devices = J2534DeviceFinder.FindInstalledJ2534DLLs();
            if (devices == null || devices.Count == 0) { L("No J2534 PassThru devices found in the registry."); return false; }

            J2534Device dev = null;
            if (!string.IsNullOrEmpty(deviceName))
                dev = devices.FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
            dev = dev ?? devices[0];
            _port.LoadedDevice = dev;
            L($"Using J2534 device: {dev.Name} ({dev.Vendor})");

            if (!_port.Functions.LoadLibrary(_port.LoadedDevice)) { L("Failed to load J2534 DLL: " + dev.FunctionLibrary); return false; }

            var err = _port.Functions.PassThruOpen(IntPtr.Zero, ref _deviceId);
            if (err != J2534Err.STATUS_NOERROR) { L("PassThruOpen error: " + err); return false; }

            ProtocolID proto; BaudRate baud; ushort pins;
            if (bus == CanBus.MsCan125) { proto = ProtocolID.ISO15765_PS; baud = BaudRate.CAN_125000; pins = 0x030B; }
            else { proto = ProtocolID.ISO15765; baud = BaudRate.CAN_500000; pins = 0x0000; }
            _protocol = proto;

            err = _port.Functions.PassThruConnect(_deviceId, proto, ConnectFlag.NONE, baud, ref _channelId);
            if (err != J2534Err.STATUS_NOERROR) { L("PassThruConnect error: " + err); return false; }
            L($"Connected: {proto} @ {(uint)baud} baud, channel {_channelId}.");

            // Channel config: pin-select for MS-CAN, and Ford 0x00 ISO-TP pad byte.
            var cfg = new List<SConfig>();
            if (pins != 0) cfg.Add(new SConfig { Parameter = ConfigParameter.J1962_PINS, Value = pins });
            cfg.Add(new SConfig { Parameter = ConfigParameter.ISO15765_PAD_VALUE, Value = 0x00 });
            err = _port.Functions.SetConfig((int)_channelId, ref cfg);
            if (err != J2534Err.STATUS_NOERROR) L("Warning: SetConfig (pins/pad) returned " + err + " (continuing).");
            else L($"Channel config set (pins=0x{pins:X3}, pad=0x00).");

            if (!SetupFlowControlFilter(txId, rxId)) return false;

            _open = true;
            L($"Channel ready: TX 0x{txId:X3} / RX 0x{rxId:X3}.");
            return true;
        }

        private bool SetupFlowControlFilter(uint txId, uint rxId)
        {
            // mask 0x7FF (match full 11-bit id), pattern = rxId (ECU reply), flowControl = txId (our send id)
            var mask = new byte[] { 0, 0, 0x07, 0xFF };
            var pattern = HexUtil.BE32(rxId);
            var flow = HexUtil.BE32(txId);
            var maskMsg = new PassThruMsg(_protocol, TxFlag.NONE, mask);
            var patMsg = new PassThruMsg(_protocol, TxFlag.NONE, pattern);
            var flowMsg = new PassThruMsg(_protocol, TxFlag.ISO15765_FRAME_PAD, flow);
            IntPtr mPtr = maskMsg.ToIntPtr(), pPtr = patMsg.ToIntPtr(), fPtr = flowMsg.ToIntPtr();
            try
            {
                var err = _port.Functions.PassThruStartMsgFilter((int)_channelId, FilterType.FLOW_CONTROL_FILTER, mPtr, pPtr, fPtr, ref _filterId);
                if (err != J2534Err.STATUS_NOERROR) { L("PassThruStartMsgFilter error: " + err); return false; }
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(mPtr); Marshal.FreeHGlobal(pPtr); Marshal.FreeHGlobal(fPtr);
            }
        }

        public byte[] SendReceive(byte[] uds, int timeoutMs)
        {
            lock (_io)
            {
                if (!_open) return null;
                var frame = HexUtil.Concat(HexUtil.BE32(TxId), uds);
                Frame?.Invoke("TX", uds);

                int num = 1;
                var txMsg = new PassThruMsg(_protocol, TxFlag.ISO15765_FRAME_PAD, frame);
                IntPtr txPtr = txMsg.ToIntPtr();
                J2534Err err;
                try { err = _port.Functions.PassThruWriteMsgs((int)_channelId, txPtr, ref num, 100); }
                finally { Marshal.FreeHGlobal(txPtr); }
                if (err != J2534Err.STATUS_NOERROR) { L("PassThruWriteMsgs error: " + err); return null; }

                return ReadLoop(timeoutMs);
            }
        }

        public byte[] ReadNext(int timeoutMs)
        {
            lock (_io) { return _open ? ReadLoop(timeoutMs) : null; }
        }

        /// <summary>Read until a genuine response (not a TX echo / first-frame indication) or timeout.</summary>
        private byte[] ReadLoop(int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            do
            {
                int remaining = Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds);
                int num = 1;
                IntPtr rxPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PassThruMsg)) * num);
                try
                {
                    var err = _port.Functions.PassThruReadMsgs((int)_channelId, rxPtr, ref num, remaining);
                    if (err != J2534Err.STATUS_NOERROR)
                    {
                        if (err == J2534Err.ERR_BUFFER_EMPTY || err == J2534Err.ERR_TIMEOUT) continue;
                        return null;
                    }
                    if (num < 1) continue;
                    var msg = rxPtr.AsMsgList(1).Last();
                    int status = (int)msg.RxStatus;
                    // Skip TX echoes (TX_MSG_TYPE 0x01) and the ISO-TP first-frame indication (START_OF_MESSAGE 0x02).
                    if ((status & (int)RxStatus.TX_MSG_TYPE) != 0 || (status & (int)RxStatus.START_OF_MESSAGE) != 0)
                        continue;
                    var all = msg.GetBytes();
                    if (all == null || all.Length <= 4) continue; // need at least the 4 CAN-id bytes + 1 UDS byte
                    var payload = new byte[all.Length - 4];
                    Buffer.BlockCopy(all, 4, payload, 0, payload.Length);
                    Frame?.Invoke("RX", payload);
                    return payload;
                }
                finally { Marshal.FreeHGlobal(rxPtr); }
            } while (sw.ElapsedMilliseconds < timeoutMs);
            return null;
        }

        public void StartTesterPresent(uint intervalMs)
        {
            lock (_io)
            {
                if (!_open) return;
                StopTesterPresentNoLock();
                // TesterPresent, suppress positive response: 3E 80
                var frame = HexUtil.Concat(HexUtil.BE32(TxId), new byte[] { 0x3E, 0x80 });
                var msg = new PassThruMsg(_protocol, TxFlag.ISO15765_FRAME_PAD, frame);
                IntPtr ptr = msg.ToIntPtr();
                try
                {
                    int id = 0;
                    var err = _port.Functions.PassThruStartPeriodicMsg((int)_channelId, ptr, ref id, (int)intervalMs);
                    if (err == J2534Err.STATUS_NOERROR) { _tpMsgId = id; L($"TesterPresent started ({intervalMs} ms)."); }
                    else L("Warning: could not start periodic TesterPresent: " + err);
                }
                finally { Marshal.FreeHGlobal(ptr); }
            }
        }

        public void StopTesterPresent() { lock (_io) StopTesterPresentNoLock(); }

        private void StopTesterPresentNoLock()
        {
            if (_tpMsgId >= 0)
            {
                try { _port.Functions.PassThruStopPeriodicMsg((int)_channelId, _tpMsgId); } catch { }
                _tpMsgId = -1;
            }
        }

        public double ReadBatteryVoltage()
        {
            lock (_io)
            {
                if (!_open) return 0;
                int mv = 0;
                try
                {
                    var err = _port.Functions.ReadBatteryVoltage((int)_deviceId, ref mv);
                    if (err == J2534Err.STATUS_NOERROR) return mv / 1000.0;
                }
                catch { }
                return 0;
            }
        }

        public void Close()
        {
            lock (_io)
            {
                if (!_open && _deviceId == 0) return;
                StopTesterPresentNoLock();
                try { if (_channelId != 0) _port.Functions.PassThruDisconnect((int)_channelId); } catch { }
                try { if (_deviceId != 0) _port.Functions.PassThruClose(_deviceId); } catch { }
                _channelId = 0; _deviceId = 0; _open = false;
                L("J2534 channel closed.");
            }
        }

        public void Dispose() => Close();
    }
}
