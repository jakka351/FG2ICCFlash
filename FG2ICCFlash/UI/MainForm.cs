using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FG2ICCFlasher.Core;
using J2534;

namespace FG2ICCFlasher.UI
{
    public partial class MainForm : Form
    {
        private const string BrowseSentinel = "Browse for a file…";
        private List<FirmwareEntry> _catalog = new List<FirmwareEntry>();
        private FirmwareEntry _selectedApp;
        private FirmwareEntry _selectedSbl;
        private FlashSequencer _seq;

        public MainForm()
        {
            InitializeComponent();

            try { statusFlagLabel.Image = AustralianFlagRenderer.Render(18); } catch { }
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            LoadLogo();

            comboBus.Items.AddRange(new object[] { "MS-CAN 125 kbps (pins 3/11)", "HS-CAN 500 kbps (pins 6/14)" });
            comboBus.SelectedIndex = 0;
            foreach (var k in FordSecurity.KnownKeys) comboKey.Items.Add(k);
            comboKey.SelectedIndex = 0;
            comboDfi.Items.AddRange(new object[] { "0x00", "0x01" });
            comboDfi.SelectedIndex = 0;

            btnRefresh.Click += (s, e) => RefreshDevices();
            btnBrowseApp.Click += (s, e) => BrowseInto(comboApp, false);
            btnBrowseSbl.Click += (s, e) => BrowseInto(comboSbl, true);
            comboApp.SelectedIndexChanged += (s, e) => OnFirmwareSelected(comboApp, false);
            comboSbl.SelectedIndexChanged += (s, e) => OnFirmwareSelected(comboSbl, true);

            btnFlash.Click += (s, e) => StartFlash();
            btnAbort.Click += (s, e) => { _seq?.Abort(); Log("Abort requested..."); };

            btnReadInfo.Click += (s, e) => RunOp("Read Module Info", false, 0, false, true, (g, o) => ModuleOps.ReadModuleInfo(g, o, Log));
            btnReadDtc.Click += (s, e) => RunOp("Read DTCs", false, 0, false, false, (g, o) => ModuleOps.ReadDtcs(g, o, Log));
            btnClearDtc.Click += (s, e) => RunOp("Clear DTCs", false, 0, false, false, (g, o) => ModuleOps.ClearDtcs(g, o, Log));
            btnSelfTest.Click += (s, e) => RunOp("On-Demand Self Test", false, 0, false, true,
                (g, o) => ModuleOps.RunSelfTest(g, o, Log, ModuleOps.RoutineOnDemandSelfTest, "On-Demand Self Test"));
            btnEolSelfTest.Click += (s, e) => RunOp("EOL / Assembly Self Test", true, 0x87, true, true,
                (g, o) => ModuleOps.RunSelfTest(g, o, Log, ModuleOps.RoutineAssemblySelfTest, "EOL / Assembly Self Test"));
            btnRecore.Click += (s, e) => StartRecore();
            btnRunCustom.Click += (s, e) => StartCustom();

            Load += (s, e) =>
            {
                string rep;
                bool ok = FordSecurity.SelfTest(out rep);
                Log(rep);
                if (!ok) Log("WARNING: security algorithm self-test FAILED — do not flash.");
                LoadFirmwareCatalog();
                RefreshDevices();
                Log("Ready. This tool flashes a live module — ensure stable power before flashing.");
            };
        }

        // ---------------- Branding ----------------

        private void LoadLogo()
        {
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FG2ICCFlasher.TesterPresentLogo.png"))
                    if (s != null) logoPicture.Image = Image.FromStream(s);
            }
            catch { }
        }

        // ---------------- Devices ----------------

        private void RefreshDevices()
        {
            try
            {
                comboDevice.Items.Clear();
                var devices = J2534DeviceFinder.FindInstalledJ2534DLLs();
                foreach (var d in devices) comboDevice.Items.Add(d.Name);
                if (comboDevice.Items.Count > 0) comboDevice.SelectedIndex = 0;
                Log($"Found {comboDevice.Items.Count} J2534 device(s).");
            }
            catch (Exception ex) { Log("Device enumeration error: " + ex.Message); }
        }

        // ---------------- Firmware ----------------

        private void LoadFirmwareCatalog()
        {
            try
            {
                _catalog = FirmwareCatalog.LoadEmbedded();
                var apps = _catalog.Applications();
                var sbls = _catalog.FlashDrivers();

                comboApp.Items.Clear();
                foreach (var a in apps) comboApp.Items.Add(a);
                comboApp.Items.Add(BrowseSentinel);
                comboSbl.Items.Clear();
                foreach (var b in sbls) comboSbl.Items.Add(b);
                comboSbl.Items.Add(BrowseSentinel);

                Log($"Embedded firmware: {apps.Count} application(s), {sbls.Count} flash driver(s).");

                // Default app = newest build (file ending in 'S'), else first.
                int appIdx = apps.FindIndex(a => Path.GetFileNameWithoutExtension(a.FileName).EndsWith("S", StringComparison.OrdinalIgnoreCase));
                comboApp.SelectedIndex = appIdx >= 0 ? appIdx : (apps.Count > 0 ? 0 : -1);
                comboSbl.SelectedIndex = sbls.Count > 0 ? 0 : -1;
            }
            catch (Exception ex) { Log("Firmware catalog error: " + ex.Message); }
        }

        private void OnFirmwareSelected(ComboBox combo, bool isSbl)
        {
            if (combo.SelectedItem is string s && s == BrowseSentinel) { BrowseInto(combo, isSbl); return; }
            var entry = combo.SelectedItem as FirmwareEntry;
            if (entry == null) return;
            if (isSbl) { _selectedSbl = entry; lblSblInfo.Text = Describe(entry); }
            else { _selectedApp = entry; lblAppInfo.Text = Describe(entry); }
            WarnIfNotFdim(entry);
        }

        private void BrowseInto(ComboBox combo, bool isSbl)
        {
            using (var ofd = new OpenFileDialog { Filter = "Ford PHF firmware (*.PHF)|*.PHF|All files (*.*)|*.*", Title = isSbl ? "Select the flash driver / SBL PHF" : "Select the application PHF" })
            {
                var dir = FindFirmwareDir();
                if (dir != null) ofd.InitialDirectory = dir;
                if (ofd.ShowDialog(this) != DialogResult.OK) { ResyncSelection(combo, isSbl); return; }
                try
                {
                    var entry = FirmwareCatalog.LoadFile(ofd.FileName);
                    int insertAt = Math.Max(0, combo.Items.Count - 1); // before the sentinel
                    combo.Items.Insert(insertAt, entry);
                    combo.SelectedIndex = insertAt;
                    Log($"Loaded {(isSbl ? "flash driver" : "application")} from file: {entry.FileName}");
                }
                catch (Exception ex) { Log("Failed to load PHF: " + ex.Message); ResyncSelection(combo, isSbl); }
            }
        }

        private void ResyncSelection(ComboBox combo, bool isSbl)
        {
            var cur = isSbl ? _selectedSbl : _selectedApp;
            if (cur != null) { int i = combo.Items.IndexOf(cur); if (i >= 0) combo.SelectedIndex = i; }
        }

        private static string Describe(FirmwareEntry e)
        {
            var p = e.Phf;
            return $"id 0x{p.ModuleId:X3}  {p.Application} v{p.MaskNumber}  0x{p.StartAddress:X8}..0x{p.EndAddress - 1:X8}  {p.TotalDataBytes} bytes  DL-fmt 0x{p.DownloadFormat:X2}  cksum 0x{p.FileChecksum:X4}  flashInd {p.FlashIndicator}";
        }

        private void WarnIfNotFdim(FirmwareEntry e)
        {
            if (e.Phf.ModuleId != 0 && e.Phf.ModuleId != 0x7A6)
                Log($"WARNING: {e.FileName} MODULE ID is 0x{e.Phf.ModuleId:X3}, not 0x7A6 (FDIM).");
        }

        private static string FindFirmwareDir()
        {
            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "Firmware");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            catch { }
            return null;
        }

        // ---------------- Options ----------------

        private bool TryBuildOptions(out FlashOptions opt, out string error)
        {
            opt = new FlashOptions();
            error = null;
            try
            {
                opt.DeviceName = comboDevice.SelectedItem?.ToString();
                opt.Bus = comboBus.SelectedIndex == 1 ? CanBus.HsCan500 : CanBus.MsCan125;
                opt.TxId = ParseHexUint(txtTxId.Text, 0x7A6);
                opt.RxId = ParseHexUint(txtRxId.Text, 0x7AE);
                opt.ProgrammingSession = (byte)ParseHexUint(txtSession.Text, 0x85);
                opt.KeyWord = ((FordSecurity.KeyWord)comboKey.SelectedItem).Bytes;
                opt.DataFormatIdentifier = (byte)(comboDfi.SelectedIndex == 1 ? 0x01 : 0x00);
                opt.DownloadSbl = chkSbl.Checked;
                opt.EraseBeforeAppDownload = chkErase.Checked;
                opt.VerifyAfter = chkVerify.Checked;
                opt.EcuResetAfter = chkReset.Checked;
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private static uint ParseHexUint(string s, uint fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            uint v;
            return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        // ---------------- Flash ----------------

        private void StartFlash()
        {
            if (_seq != null && _seq.IsRunning) { Log("A flash is already running."); return; }
            if (_selectedApp == null) { Warn("Select an application firmware first."); return; }

            FlashOptions opt; string err;
            if (!TryBuildOptions(out opt, out err)) { Warn("Invalid options: " + err); return; }
            if (opt.DownloadSbl && _selectedSbl == null) { Warn("Download SBL is enabled but no flash driver is selected."); return; }

            var msg = "LIVE FLASH — this will erase and reprogram the Front Display Interface Module (0x7A6).\r\n\r\n" +
                      "• Battery/charger must hold a steady voltage for the whole operation.\r\n" +
                      "• Do NOT disconnect power or the adapter until it finishes.\r\n" +
                      "• An interruption during erase/program can brick the module.\r\n\r\n" +
                      $"Application: {_selectedApp.FileName}\r\n" +
                      (opt.DownloadSbl ? $"Flash driver: {_selectedSbl.FileName}\r\n" : "") +
                      "\r\nProceed?";
            if (MessageBox.Show(this, msg, "Confirm flash", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            { Log("Flash cancelled by user."); return; }

            SetBusy(true, flashing: true);
            progress.Value = 0; lblPhase.Text = "Starting...";

            _seq = new FlashSequencer(opt, opt.DownloadSbl ? _selectedSbl.Phf : null, _selectedApp.Phf);
            _seq.Log += Log;
            _seq.Progress += OnProgress;
            _seq.Completed += OnFlashCompleted;
            _seq.Start();
        }

        private void OnProgress(int pct, string phase)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => OnProgress(pct, phase))); return; }
            progress.Value = Math.Max(0, Math.Min(100, pct));
            lblPhase.Text = phase;
        }

        private void OnFlashCompleted(bool ok, string message)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => OnFlashCompleted(ok, message))); return; }
            Log((ok ? "SUCCESS: " : "FAILED: ") + message);
            lblPhase.Text = ok ? "Completed." : "Failed.";
            SetBusy(false, flashing: true);
            MessageBox.Show(this, message, ok ? "Completed" : "Failed", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        // ---------------- Routines / one-shot ops ----------------

        private void StartRecore()
        {
            if (MessageBox.Show(this,
                "Recore commands the FDIM to erase and re-image its OS from a USB stick.\r\n" +
                "Keep power stable until it finishes. Proceed?",
                "Confirm Recore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            { Log("Recore cancelled."); return; }
            RunOp("Recore", true, 0x87, true, true, (g, o) => ModuleOps.Recore(g, o, Log));
        }

        private void StartCustom()
        {
            var bytes = HexUtil.FromHex(txtCustom.Text);
            if (bytes.Length == 0) { Warn("Enter a hex request, e.g. 31 02 00."); return; }
            bool unlock = chkCustomUnlock.Checked;
            RunOp("Custom request", unlock, 0x87, unlock, true, (g, o) =>
            {
                Log("  TX (UDS): " + HexUtil.ToHex(bytes));
                var r = g.Request(bytes, o.RoutineTimeoutMs);
                Log("  " + r.Describe());
            });
        }

        private void RunOp(string name, bool enterSession, byte sessionMode, bool security, bool tp, Action<GdsClient, FlashOptions> body)
        {
            FlashOptions opt; string err;
            if (!TryBuildOptions(out opt, out err)) { Warn("Invalid options: " + err); return; }

            SetBusy(true, flashing: false);
            lblPhase.Text = name + "...";
            var t = new Thread(() =>
            {
                GdsSession session = null;
                try
                {
                    Log("---- " + name + " ----");
                    session = new GdsSession(opt, Log); // real J2534 channel
                    string e2;
                    if (!session.Open(enterSession, sessionMode, security, tp, out e2)) { Log(name + " failed: " + e2); return; }
                    if (session.BatteryVolts > 0) SetVoltage(session.BatteryVolts);
                    body(session.Gds, opt);
                    Log(name + " done.");
                }
                catch (Exception ex) { Log(name + " error: " + ex.Message); }
                finally
                {
                    session?.Dispose();
                    UiInvoke(() => { SetBusy(false, flashing: false); lblPhase.Text = "Idle."; });
                }
            }) { IsBackground = true };
            t.Start();
        }

        // ---------------- UI state ----------------

        private void SetBusy(bool busy, bool flashing)
        {
            Control[] actions = { btnFlash, btnReadInfo, btnReadDtc, btnClearDtc, btnSelfTest, btnEolSelfTest, btnRecore, btnRunCustom,
                                  comboApp, comboSbl, btnBrowseApp, btnBrowseSbl, comboDevice, btnRefresh, comboBus };
            foreach (var c in actions) c.Enabled = !busy;
            btnAbort.Enabled = busy && flashing;
        }

        private void SetVoltage(double v)
        {
            UiInvoke(() => lblVoltage.Text = $"Battery: {v:0.0} V");
        }

        private void UiInvoke(Action a)
        {
            if (InvokeRequired) BeginInvoke(a); else a();
        }

        private void Warn(string msg) => MessageBox.Show(this, msg, "FG2ICCFlash", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private void Log(string message)
        {
            if (txtLog.InvokeRequired) { txtLog.BeginInvoke((Action)(() => Log(message))); return; }
            txtLog.AppendText((message ?? "").TrimEnd('\r', '\n') + "\r\n");
        }
    }
}
