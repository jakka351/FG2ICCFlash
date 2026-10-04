using System;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Owns an open ICanChannel + GdsClient for the FDIM and handles the common preamble:
    /// connect, optional diagnostic session, optional SecurityAccess, and tester-present.
    /// Shared by the flash sequencer and the one-shot routine/diagnostic operations so the
    /// connect/unlock logic lives in one place. Always uses a real J2534 channel unless a
    /// factory is supplied (tests inject the simulator).
    /// </summary>
    public sealed class GdsSession : IDisposable
    {
        private readonly FlashOptions _opt;
        private readonly Action<string> _log;
        private readonly Func<ICanChannel> _factory;
        private bool _tpStarted;

        public ICanChannel Channel { get; private set; }
        public GdsClient Gds { get; private set; }
        public double BatteryVolts { get; private set; }

        public GdsSession(FlashOptions opt, Action<string> log, Func<ICanChannel> channelFactory = null)
        {
            _opt = opt;
            _log = log ?? (s => { });
            _factory = channelFactory ?? (() => new J2534CanChannel());
        }

        private void L(string m) => _log(m);

        /// <summary>
        /// Open the channel and bring the module to the requested state.
        /// </summary>
        /// <param name="enterSession">send StartDiagnosticSession(sessionMode)</param>
        /// <param name="sessionMode">e.g. 0x85 programming, 0x87 adjustment, 0x81 default</param>
        /// <param name="doSecurity">perform SecurityAccess with the configured key-word</param>
        /// <param name="startTesterPresent">start the periodic tester-present keep-alive</param>
        public bool Open(bool enterSession, byte sessionMode, bool doSecurity, bool startTesterPresent, out string error)
        {
            error = null;
            Channel = _factory();
            Channel.Log += L;
            Channel.Frame += (dir, payload) => L($"  {dir}: {HexUtil.ToHex(payload)}");

            if (!Channel.Open(_opt.DeviceName, _opt.Bus, _opt.TxId, _opt.RxId))
            { error = "Failed to open J2534 channel."; return false; }

            BatteryVolts = Channel.ReadBatteryVoltage();
            if (BatteryVolts > 0)
            {
                L($"Battery voltage: {BatteryVolts:0.0} V");
                if (BatteryVolts < _opt.MinBatteryVolts)
                    L($"WARNING: battery {BatteryVolts:0.0} V is below {_opt.MinBatteryVolts:0.0} V — a drop-out during erase/program can brick the module.");
            }

            Gds = new GdsClient(Channel);
            Gds.Log += L;

            if (enterSession)
            {
                L($"[$10] StartDiagnosticSession 0x{sessionMode:X2}");
                var r = Gds.StartDiagnosticSession(sessionMode, _opt.P2TimeoutMs);
                if (!r.Positive) { error = "StartDiagnosticSession 0x" + sessionMode.ToString("X2") + " failed: " + r.Describe(); return false; }
            }

            if (doSecurity)
            {
                L($"[$27] SecurityAccess level 0x{_opt.SecurityLevel:X2} using key-word {HexUtil.ToHex(_opt.KeyWord)}");
                string secErr;
                if (!Gds.SecurityAccess(_opt.SecurityLevel, _opt.KeyWord, _opt.P2TimeoutMs, out secErr))
                { error = "SecurityAccess failed: " + secErr; return false; }
            }

            if (startTesterPresent)
            {
                Channel.StartTesterPresent(_opt.TesterPresentIntervalMs);
                _tpStarted = true;
            }
            return true;
        }

        public void Dispose()
        {
            try { if (Channel != null && _tpStarted) Channel.StopTesterPresent(); } catch { }
            try { Channel?.Close(); } catch { }
        }
    }
}
