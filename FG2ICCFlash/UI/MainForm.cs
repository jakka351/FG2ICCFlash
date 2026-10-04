using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using FG2ICCFlasher.Core;
using J2534;

namespace FG2ICCFlasher.UI
{
    public partial class MainForm : Form
    {
        private PhfFile _sbl;
        private PhfFile _app;
        private FlashSequencer _seq;

        public MainForm()
        {
            InitializeComponent();

            comboBus.Items.AddRange(new object[] { "MS-CAN 125 kbps (pins 3/11)", "HS-CAN 500 kbps (pins 6/14)" });
            comboBus.SelectedIndex = 0;
            foreach (var k in FordSecurity.KnownKeys) comboKey.Items.Add(k);
            comboKey.SelectedIndex = 0; // Janis (MK2)
            comboDfi.Items.AddRange(new object[] { "0x00", "0x01" });
            comboDfi.SelectedIndex = 0;

            btnRefresh.Click += (s, e) => RefreshDevices();
            btnBrowseSbl.Click += (s, e) => BrowsePhf(true);
            btnBrowseApp.Click += (s, e) => BrowsePhf(false);
            btnAutoDetect.Click += (s, e) => AutoDetectFirmware();
            btnReadInfo.Click += (s, e) => ReadModuleInfo();
            btnFlash.Click += (s, e) => StartFlash();
            btnAbort.Click += (s, e) => { _seq?.Abort(); Log("Abort requested..."); };

            Load += (s, e) =>
            {
                string rep;
                bool ok = FordSecurity.SelfTest(out rep);
                Log(rep);
                if (!ok) Log("WARNING: security algorithm self-test FAILED — do not flash.");
                RefreshDevices();
                AutoDetectFirmware();
                Log("Ready. Dry Run is ON by default — nothing is written until you turn it off.");
            };
        }

        // ---------------- Device / firmware ----------------

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

        private void BrowsePhf(bool isSbl)
        {
            using (var ofd = new OpenFileDialog { Filter = "Ford PHF firmware (*.PHF)|*.PHF|All files (*.*)|*.*", Title = isSbl ? "Select the flash driver / SBL PHF" : "Select the application PHF" })
            {
                var dir = FindFirmwareDir();
                if (dir != null) ofd.InitialDirectory = dir;
                if (ofd.ShowDialog(this) == DialogResult.OK) LoadPhf(ofd.FileName, isSbl);
            }
        }

        private void LoadPhf(string path, bool isSbl)
        {
            try
            {
                var phf = PhfFile.Load(path);
                string info = $"ID 0x{phf.ModuleId:X3}  {phf.Application}  v{phf.MaskNumber}  " +
                              $"0x{phf.StartAddress:X8}..0x{phf.EndAddress - 1:X8}  {phf.TotalDataBytes} bytes  " +
                              $"DL-fmt 0x{phf.DownloadFormat:X2}  cksum 0x{phf.FileChecksum:X4}  flashInd {phf.FlashIndicator}";

                if (isSbl)
                {
                    if (!phf.IsFlashDriver) Log($"NOTE: selected SBL '{Path.GetFileName(path)}' has FLASH INDICATOR {phf.FlashIndicator} (expected 0 for a flash driver).");
                    _sbl = phf; txtSbl.Text = path; lblSblInfo.Text = info;
                }
                else
                {
                    if (phf.IsFlashDriver) Log($"NOTE: selected application '{Path.GetFileName(path)}' has FLASH INDICATOR 0 (looks like a flash driver).");
                    _app = phf; txtApp.Text = path; lblAppInfo.Text = info;
                }

                if (phf.ModuleId != 0 && phf.ModuleId != 0x7A6)
                    Log($"WARNING: {Path.GetFileName(path)} MODULE ID is 0x{phf.ModuleId:X3}, not 0x7A6 (FDIM). Check you selected the right file.");

                Log($"Loaded {(isSbl ? "SBL" : "application")}: {Path.GetFileName(path)}");
                Log("  " + info);
            }
            catch (Exception ex) { Log($"Failed to parse PHF '{Path.GetFileName(path)}': {ex.Message}"); }
        }

        private void AutoDetectFirmware()
        {
            try
            {
                var dir = FindFirmwareDir();
                if (dir == null) { Log("Firmware folder not found for auto-detect — use Browse."); return; }

                var phfs = Directory.GetFiles(dir, "*.PHF", SearchOption.TopDirectoryOnly);
                // SBL: name contains SBL or 14D019; application: 14D017, prefer the newest (-?S) build.
                var sbl = phfs.FirstOrDefault(p => Path.GetFileName(p).IndexOf("SBL", StringComparison.OrdinalIgnoreCase) >= 0
                                                || Path.GetFileName(p).IndexOf("14D019", StringComparison.OrdinalIgnoreCase) >= 0);
                var apps = phfs.Where(p => Path.GetFileName(p).IndexOf("14D017", StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(p => p).ToList();
                var app = apps.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).EndsWith("S", StringComparison.OrdinalIgnoreCase))
                          ?? apps.FirstOrDefault();

                if (sbl != null) LoadPhf(sbl, true);
                if (app != null) LoadPhf(app, false);
                if (sbl == null && app == null) Log($"No FDIM PHF files found in {dir}.");
            }
            catch (Exception ex) { Log("Auto-detect error: " + ex.Message); }
        }

        /// <summary>Walk up from the app base directory looking for a sibling "Firmware" folder.</summary>
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
                opt.DryRun = chkDryRun.Checked;
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
            if (_app == null) { MessageBox.Show(this, "Load an application PHF first.", "FG2ICC Flasher", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            FlashOptions opt; string err;
            if (!TryBuildOptions(out opt, out err)) { MessageBox.Show(this, "Invalid options: " + err, "FG2ICC Flasher", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (opt.DownloadSbl && _sbl == null) { MessageBox.Show(this, "Download SBL is enabled but no SBL is loaded. Load the flash driver PHF or uncheck Download SBL.", "FG2ICC Flasher", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (!opt.DryRun)
            {
                var msg = "LIVE FLASH — this will erase and reprogram the Front Display Interface Module (0x7A6).\r\n\r\n" +
                          "• Battery/charger must hold a steady voltage for the whole operation.\r\n" +
                          "• Do NOT disconnect power or the adapter until it finishes.\r\n" +
                          "• An interruption during erase/program can brick the module.\r\n\r\n" +
                          "Proceed with the LIVE flash?";
                if (MessageBox.Show(this, msg, "Confirm LIVE flash", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                { Log("Live flash cancelled by user."); return; }
            }

            SetRunning(true);
            progress.Value = 0; lblPhase.Text = "Starting...";

            _seq = new FlashSequencer(opt, opt.DownloadSbl ? _sbl : null, _app);
            _seq.Log += Log;
            _seq.Progress += OnProgress;
            _seq.Completed += OnCompleted;
            _seq.Start();
        }

        private void OnProgress(int pct, string phase)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => OnProgress(pct, phase))); return; }
            progress.Value = Math.Max(0, Math.Min(100, pct));
            lblPhase.Text = phase;
        }

        private void OnCompleted(bool ok, string message)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => OnCompleted(ok, message))); return; }
            Log((ok ? "SUCCESS: " : "FAILED: ") + message);
            lblPhase.Text = ok ? "Completed." : "Failed.";
            SetRunning(false);
            MessageBox.Show(this, message, ok ? "Completed" : "Failed", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private void SetRunning(bool running)
        {
            btnFlash.Enabled = !running;
            btnAbort.Enabled = running;
            btnReadInfo.Enabled = !running;
            btnBrowseSbl.Enabled = !running;
            btnBrowseApp.Enabled = !running;
            btnAutoDetect.Enabled = !running;
        }

        // ---------------- Read module info ----------------

        private void ReadModuleInfo()
        {
            FlashOptions opt; string err;
            if (!TryBuildOptions(out opt, out err)) { Log("Invalid options: " + err); return; }

            SetRunning(true);
            lblPhase.Text = "Reading module info...";
            var t = new Thread(() =>
            {
                ICanChannel ch = null;
                try
                {
                    ch = opt.DryRun ? (ICanChannel)new SimulatedCanChannel() : new J2534CanChannel();
                    ch.Log += Log;
                    ch.Frame += (dir, p) => Log($"  {dir}: {HexUtil.ToHex(p)}");
                    if (!ch.Open(opt.DeviceName, opt.Bus, opt.TxId, opt.RxId)) { Log("Open failed."); return; }

                    double v = ch.ReadBatteryVoltage();
                    if (v > 0) SetVoltage(v);

                    var gds = new GdsClient(ch); gds.Log += Log;
                    ReadOneDid(gds, 0xE611, "Strategy SW part number", opt.P2TimeoutMs);
                    ReadOneDid(gds, 0xE610, "Hardware part number", opt.P2TimeoutMs);
                    ReadOneDid(gds, 0xF188, "Mfr ECU software number", opt.P2TimeoutMs);
                }
                catch (Exception ex) { Log("Read info error: " + ex.Message); }
                finally
                {
                    try { ch?.Close(); } catch { }
                    if (InvokeRequired) BeginInvoke((Action)(() => { SetRunning(false); lblPhase.Text = "Idle."; }));
                    else { SetRunning(false); lblPhase.Text = "Idle."; }
                }
            }) { IsBackground = true };
            t.Start();
        }

        private void ReadOneDid(GdsClient gds, ushort did, string name, int timeout)
        {
            var r = gds.ReadDid(did, timeout);
            if (r.Positive && r.Data.Length > 3)
            {
                var ascii = new string(r.Data.Skip(3).Select(b => (b >= 0x20 && b < 0x7F) ? (char)b : '.').ToArray());
                Log($"  DID 0x{did:X4} ({name}): \"{ascii.Trim()}\"  [{HexUtil.ToHex(r.Data, 3, r.Data.Length - 3)}]");
            }
            else Log($"  DID 0x{did:X4} ({name}): {r.Describe()}");
        }

        private void SetVoltage(double v)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => SetVoltage(v))); return; }
            lblVoltage.Text = $"Battery: {v:0.0} V";
        }

        // ---------------- Logging ----------------

        private void Log(string message)
        {
            if (txtLog.InvokeRequired) { txtLog.BeginInvoke((Action)(() => Log(message))); return; }
            txtLog.AppendText(message.TrimEnd('\r', '\n') + "\r\n");
        }
    }
}
