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
        private volatile bool _opBusy;   // a one-shot routine/diagnostic op is running on a worker thread
        private volatile bool _opAbort;  // cooperative cancel for a one-shot op (e.g. on window close)

        private List<UsbDrive> _usbDrives = new List<UsbDrive>();
        private AsBuiltConfig _config = new AsBuiltConfig();

        // ---- Recore ----
        private RecoreCatalog _recoreCat;
        private readonly List<RecoreEntry> _recoreItems = new List<RecoreEntry>();
        private string _recoreCustomScript;   // edited z.sh to inject, or null
        private bool _recoreInit;
        // Default local catalog folder on this workshop machine (editable in the UI).
        private const string DefaultRecoreFolder = @"J:\testerPresent\Information\Ford\MKII ICC\FG2 RECORE PACKAGES";
        private sealed class PayloadDef
        {
            public readonly string Name, Key, Hint;
            public PayloadDef(string name, string key, string hint) { Name = name; Key = key; Hint = hint; }
        }
        private readonly PayloadDef[] _payloads =
        {
            new PayloadDef("Official ICC Application (swsa_meta_install)", "Official",
                "contents → USB root"),
            new PayloadDef("FSA R1208 ICC Software Update", "FSA",
                "contents → USB root"),
            new PayloadDef("Recore (image-usb-recore)", "Recore",
                "contents → USB root"),
        };

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
            new ToolTip().SetToolTip(comboKey, "Pick a listed key, or type/paste a custom 5-byte hex key, e.g. 11 22 33 44 55");
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
                (g, o) => ModuleOps.RunSelfTest(g, o, Log, ModuleOps.RoutineOnDemandSelfTest, "On-Demand Self Test", () => _opAbort));
            btnEolSelfTest.Click += (s, e) => RunOp("EOL / Assembly Self Test", true, 0x87, true, true,
                (g, o) => ModuleOps.RunSelfTest(g, o, Log, ModuleOps.RoutineAssemblySelfTest, "EOL / Assembly Self Test", () => _opAbort));
            btnRecore.Click += (s, e) => StartRecore();
            btnRunCustom.Click += (s, e) => StartCustom();

            // Stage 2 — USB
            comboPayload.Items.AddRange(_payloads.Select(p => (object)p.Name).ToArray());
            comboPayload.SelectedIndex = 0;
            btnUsbRefresh.Click += (s, e) => RefreshUsbDrives();
            comboPayload.SelectedIndexChanged += (s, e) => OnPayloadSelected();
            btnBrowsePayload.Click += (s, e) => BrowsePayloadSource();
            btnFormatUsb.Click += (s, e) => FormatUsb();
            btnWriteUsb.Click += (s, e) => WriteUsb();
            btnUsbAbort.Click += (s, e) => { _opAbort = true; Log("USB abort requested..."); };
            comboRecoreBackup.SelectedIndexChanged += (s, e) => OnRecoreSelected();
            btnLoadCatalog.Click += (s, e) => LoadRecoreCatalog((txtRecoreUrl.Text ?? "").Trim(), true);
            btnEditScript.Click += (s, e) => EditRecoreScript();

            // Configuration (As-Built)
            comboZone.Items.AddRange(new object[] { "Single Zone", "Dual Zone" });
            comboZone.SelectedIndex = 0;
            btnReadConfig.Click += (s, e) => ReadConfigFromModule();
            btnLoadAbt.Click += (s, e) => LoadAbtFile();
            btnSaveAbt.Click += (s, e) => SaveAbtFile();
            btnWriteVin.Click += (s, e) => WriteVinToModule();
            btnWriteZone.Click += (s, e) => WriteZoneToModule();
            btnApplyRaw.Click += (s, e) => ApplyRawEdits();
            btnWriteBlock.Click += (s, e) => WriteSelectedBlock();
            gridConfig.CellEndEdit += (s, e) => OnConfigCellEndEdit(e.RowIndex, e.ColumnIndex);

            Load += (s, e) =>
            {
                string rep;
                bool ok = FordSecurity.SelfTest(out rep);
                Log(rep);
                if (!ok) Log("WARNING: security algorithm self-test FAILED — do not flash.");
                LoadFirmwareCatalog();
                RefreshDevices();
                RefreshUsbDrives();
                OnPayloadSelected();
                Log("Ready. This tool flashes a live module — ensure stable power before flashing.");
            };
        }

        // ---------------- Branding ----------------

        private void LoadLogo()
        {
            try
            {
                // Copy into an independent Bitmap so the PictureBox does not depend on the (closing)
                // manifest-resource stream for the lifetime of the Image.
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FG2ICCFlasher.TesterPresentLogo.png"))
                    if (s != null) using (var tmp = Image.FromStream(s)) logoPicture.Image = new Bitmap(tmp);
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
            if (cur != null)
            {
                int i = combo.Items.IndexOf(cur);
                combo.SelectedIndex = i >= 0 ? i : -1;
            }
            else
            {
                // Never leave the "Browse…" sentinel as the resting selection.
                combo.SelectedIndex = -1;
            }
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
                // Security key: a listed name (Janis/BradW) or a custom 5-byte hex key typed into the box.
                var kw = comboKey.SelectedItem as FordSecurity.KeyWord;
                string keyTxt = (comboKey.Text ?? "").Trim();
                if (kw != null && string.Equals(keyTxt, kw.ToString(), StringComparison.Ordinal))
                {
                    opt.KeyWord = kw.Bytes;
                }
                else
                {
                    var kb = HexUtil.FromHex(keyTxt);
                    if (kb.Length != 5) { error = "Security key must be a listed name or exactly 5 hex bytes (e.g. 42 72 61 64 57)."; return false; }
                    opt.KeyWord = kb;
                }
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
            UiInvoke(() =>
            {
                progress.Value = Math.Max(0, Math.Min(100, pct));
                lblPhase.Text = phase;
            });
        }

        private void OnFlashCompleted(bool ok, string message)
        {
            UiInvoke(() =>
            {
                Log((ok ? "SUCCESS: " : "FAILED: ") + message);
                lblPhase.Text = ok ? "Completed." : "Failed.";
                SetBusy(false, flashing: true);
                MessageBox.Show(this, message, ok ? "Completed" : "Failed", MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            });
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
            byte[] bytes;
            try { bytes = HexUtil.FromHex(txtCustom.Text); }
            catch (Exception ex) { Warn("Invalid hex request: " + ex.Message); return; }
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

            _opBusy = true;
            _opAbort = false;
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
                    _opBusy = false;
                    UiInvoke(() => { SetBusy(false, flashing: false); lblPhase.Text = "Idle."; });
                }
            }) { IsBackground = true };
            t.Start();
        }

        // ---------------- UI state ----------------

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            bool flashing = _seq != null && _seq.IsRunning;
            if (flashing || _opBusy)
            {
                string what = flashing ? "A flash is in progress." : "A module operation is in progress.";
                var r = MessageBox.Show(this,
                    what + "\r\nInterrupting it can leave the module in a partially-programmed state.\r\n\r\nClose anyway?",
                    "Operation in progress", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r != DialogResult.Yes) { e.Cancel = true; base.OnFormClosing(e); return; }
                _seq?.Abort();
                _opAbort = true;
                // Give the worker a bounded chance to stop at a safe point before we tear down.
                for (int i = 0; i < 60 && ((_seq != null && _seq.IsRunning) || _opBusy); i++) Thread.Sleep(50);
            }
            base.OnFormClosing(e);
        }

        private void SetBusy(bool busy, bool flashing)
        {
            Control[] actions = { btnFlash, btnReadInfo, btnReadDtc, btnClearDtc, btnSelfTest, btnEolSelfTest, btnRecore, btnRunCustom,
                                  comboApp, comboSbl, btnBrowseApp, btnBrowseSbl, comboDevice, btnRefresh, comboBus,
                                  btnReadConfig, btnLoadAbt, btnSaveAbt, btnWriteVin, btnWriteZone, btnApplyRaw, btnWriteBlock, btnFormatUsb, btnWriteUsb };
            foreach (var c in actions) c.Enabled = !busy;
            btnAbort.Enabled = busy && flashing;
        }

        private void SetVoltage(double v)
        {
            UiInvoke(() => lblVoltage.Text = $"Battery: {v:0.0} V");
        }

        private void UiInvoke(Action a)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(a); else a();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        // ---------------- Stage 2 — USB ----------------

        private void RefreshUsbDrives()
        {
            try
            {
                _usbDrives = UsbPrep.ListRemovable();
                comboUsbDrive.Items.Clear();
                foreach (var d in _usbDrives) comboUsbDrive.Items.Add(d.Display);
                if (comboUsbDrive.Items.Count > 0) comboUsbDrive.SelectedIndex = 0;
                Log($"Found {_usbDrives.Count} removable drive(s).");
            }
            catch (Exception ex) { Log("USB enumerate error: " + ex.Message); }
        }

        private UsbDrive SelectedUsbDrive()
        {
            int i = comboUsbDrive.SelectedIndex;
            return (i >= 0 && i < _usbDrives.Count) ? _usbDrives[i] : null;
        }

        private void OnPayloadSelected()
        {
            int i = comboPayload.SelectedIndex;
            if (i < 0 || i >= _payloads.Length) return;
            var p = _payloads[i];
            lblPayloadInfo.Text = p.Hint;

            bool recore = p.Key == "Recore";
            grpRecore.Visible = recore;
            txtUsbInstructions.Visible = !recore;
            btnWriteUsb.Text = recore ? "2.  Build recore USB" : "2.  Write payload to USB";
            if (recore)
            {
                lblPayloadInfo.Text = "choose a recore image below";
                txtPayloadSource.Text = "";
                InitRecoreUi();
                return;
            }

            var src = FindPayloadSource(p.Key);
            if (!string.IsNullOrEmpty(src)) { txtPayloadSource.Text = src; Log($"Payload source auto-detected: {src}"); }
            else txtPayloadSource.Text = "";
        }

        // ---------------- Stage 2 — Recore ----------------

        private void InitRecoreUi()
        {
            if (_recoreInit) return;
            _recoreInit = true;
            // Seed the catalog location: the workshop J: folder if present, else a local "Recore Backups" /
            // "FG2 RECORE PACKAGES" folder that already holds a manifest.json.
            string def = Directory.Exists(DefaultRecoreFolder) ? DefaultRecoreFolder : null;
            if (def == null)
            {
                var rb = FindFolderUpward("FG2 RECORE PACKAGES", "Recore Backups");
                if (rb != null && File.Exists(Path.Combine(rb, "manifest.json"))) def = rb;
            }
            if (!string.IsNullOrEmpty(def)) txtRecoreUrl.Text = def;

            // Always offer the built-in NaviMaps loader (ships in the project root).
            RebuildRecoreList(null);

            // Try to auto-load the local catalog so community/backup images appear without a click.
            if (!string.IsNullOrEmpty(def))
            {
                try { LoadRecoreCatalog(def, false); }
                catch (Exception ex) { Log("Recore catalog not loaded yet: " + ex.Message); }
            }
            else Log("Recore: no local catalog found. Enter the server folder URL and click Load catalog, " +
                     "or wait for the local packaging to finish.");
        }

        /// <summary>Find a file by name walking up from the exe directory (like the Firmware search).</summary>
        private static string FindFileUpward(params string[] names)
        {
            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 7 && dir != null; i++, dir = dir.Parent)
                    foreach (var n in names)
                    {
                        var p = Path.Combine(dir.FullName, n);
                        if (File.Exists(p)) return p;
                    }
            }
            catch { }
            return null;
        }

        /// <summary>Find a sub-folder by name walking up from the exe directory.</summary>
        private static string FindFolderUpward(params string[] names)
        {
            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 7 && dir != null; i++, dir = dir.Parent)
                    foreach (var n in names)
                    {
                        var p = Path.Combine(dir.FullName, n);
                        if (Directory.Exists(p)) return p;
                    }
            }
            catch { }
            return null;
        }

        /// <summary>Built-in NaviMaps entry (sat-nav maps loader), if navimaps(1).zip ships alongside the tool.</summary>
        private RecoreEntry NavimapsBuiltin()
        {
            var z = FindFileUpward("navimaps (1).zip", "navimaps(1).zip", "navimaps.zip");
            if (z == null) return null;
            return new RecoreEntry
            {
                Id = "navimaps", Name = "NaviMaps — sat-nav maps loader", File = Path.GetFileName(z),
                Nav = true, Body = "any", Zone = "", Engine = "", Date = "",
                LocalZipPath = z, Bytes = new FileInfo(z).Length
            };
        }

        /// <summary>Rebuild the combo from the catalog plus the built-in NaviMaps (deduped by id).</summary>
        private void RebuildRecoreList(RecoreCatalog cat)
        {
            _recoreItems.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (cat != null)
                foreach (var e in cat.Backups)
                    if (seen.Add(e.Id)) _recoreItems.Add(e);

            // Built-in NaviMaps loader (if it ships alongside the tool and the catalog didn't already list it).
            var nav = NavimapsBuiltin();
            if (nav != null && seen.Add(nav.Id)) _recoreItems.Add(nav);

            comboRecoreBackup.Items.Clear();
            foreach (var e in _recoreItems) comboRecoreBackup.Items.Add(e.ToString());
            if (comboRecoreBackup.Items.Count > 0) comboRecoreBackup.SelectedIndex = 0;
            else lblRecoreInfo.Text = "No recore images available yet — click Load catalog, or wait for packaging to finish.";
        }

        private void LoadRecoreCatalog(string location, bool verbose)
        {
            if (string.IsNullOrWhiteSpace(location)) { if (verbose) Warn("Enter a catalog folder path or server URL first."); return; }
            try
            {
                if (verbose) Log("---- Load recore catalog: " + location + " ----");
                var cat = RecoreCatalog.Load(location, Log);
                _recoreCat = cat;
                RebuildRecoreList(cat);
                Log($"Recore catalog loaded: {cat.Backups.Count} image(s).");
            }
            catch (Exception ex)
            {
                if (verbose) Warn("Failed to load recore catalog:\r\n" + ex.Message);
                else Log("Recore catalog load skipped: " + ex.Message);
            }
        }

        private RecoreEntry SelectedRecore()
        {
            int i = comboRecoreBackup.SelectedIndex;
            return (i >= 0 && i < _recoreItems.Count) ? _recoreItems[i] : null;
        }

        private void OnRecoreSelected()
        {
            _recoreCustomScript = null;   // an image switch drops any edited script
            var e = SelectedRecore();
            lblRecoreInfo.Text = e == null
                ? "No image selected. You can still open the script engine to author a z.sh, or Load a catalog."
                : e.Details() + (_recoreCustomScript != null ? "   [custom z.sh pending]" : "");
            // The script engine is always available (it falls back to the factory template).
            btnEditScript.Enabled = true;
        }

        private void EditRecoreScript()
        {
            var e = SelectedRecore();

            // Preload the editor with the selected image's current z.sh if we can reach it, else the factory template.
            string initial = null;
            string title = "Recore worker (z.sh)";
            if (e != null)
            {
                title = "Recore worker — " + e.Name;
                string zip = (!string.IsNullOrEmpty(e.LocalZipPath) && File.Exists(e.LocalZipPath)) ? e.LocalZipPath : null;
                if (zip != null) initial = RecoreBuilder.ReadWorker(zip);
                if (string.IsNullOrEmpty(initial) && zip != null) Log("This image has no z.sh of its own — starting from the factory template.");
            }
            if (string.IsNullOrEmpty(initial)) initial = RecoreScripts.FactoryRecoreWorker();

            using (var ed = new ScriptEditorForm(initial, title))
            {
                if (ed.ShowDialog(this) == DialogResult.OK)
                {
                    _recoreCustomScript = ed.ScriptText;
                    Log("Custom recore z.sh captured (" + _recoreCustomScript.Length + " bytes); it will be written to the USB as z.sh on Build.");
                    OnRecoreSelected();   // refresh the info line to show the pending-script marker
                }
            }
        }

        private void BuildRecoreUsb()
        {
            if (_opBusy) { Log("Busy."); return; }
            var d = SelectedUsbDrive();
            if (d == null) { Warn("Select a USB drive first (Refresh if none listed)."); return; }
            if (!d.Ready) { Warn("The selected USB drive is not ready / inserted."); return; }
            var e = SelectedRecore();
            if (e == null) { Warn("Select a recore image first."); return; }

            var msg = "Build a recore USB:\r\n\r\n" +
                      $"    Image: {e.Name}\r\n    USB:   {d.Letter}:\\   {d.Label}   {d.TotalGB:0.0} GB\r\n" +
                      (string.IsNullOrEmpty(e.RemoteBaseUrl) ? "" : "    (will download from the catalog server first)\r\n") +
                      (_recoreCustomScript != null ? "    (with your edited z.sh script)\r\n" : "") +
                      "\r\n⚠  This image must match the vehicle/body. The USB should already be FAT32-formatted (step 1).\r\n\r\nProceed?";
            if (MessageBox.Show(this, msg, "Build recore USB", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            _opBusy = true; _opAbort = false;
            SetUsbBusy(true);
            progress.Value = 0; lblPhase.Text = "Building recore USB...";
            Log("---- Build recore USB ----");
            Log($"  Image: {e.Name}  |  USB: {d.Letter}:\\");
            string destRoot = d.Root;
            string customScript = _recoreCustomScript;
            var t = new Thread(() =>
            {
                try
                {
                    string err;
                    string zip = RecoreCatalog.EnsureZip(e, Log, pct => OnProgress(pct, "Downloading image..."), () => _opAbort, out err);
                    if (zip == null) { FailUsb("Could not obtain the recore image: " + err); return; }
                    bool ok = RecoreBuilder.BuildFromZip(zip, destRoot, customScript, Log, pct => OnProgress(pct, "Writing recore USB..."), () => _opAbort, out err);
                    if (ok)
                    {
                        Log("Recore USB built and ready. Eject safely, then follow the on-vehicle recore steps.");
                        UiInvoke(() => MessageBox.Show(this, "Recore USB built.\r\n\r\nEject the USB safely, then perform the on-vehicle ICC recore.", "Recore USB ready", MessageBoxButtons.OK, MessageBoxIcon.Information));
                    }
                    else FailUsb("Recore build failed: " + err);
                }
                catch (Exception ex) { FailUsb("Recore build error: " + ex.Message); }
                finally { _opBusy = false; UiInvoke(() => { SetUsbBusy(false); lblPhase.Text = "Idle."; }); }
            }) { IsBackground = true };
            t.Start();
        }

        private void FailUsb(string m)
        {
            Log(m);
            UiInvoke(() => MessageBox.Show(this, m, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }

        private static string FindProjectRoot()
        {
            var fw = FindFirmwareDir();
            try { return fw != null ? Directory.GetParent(fw)?.FullName : null; } catch { return null; }
        }

        private static string FindPayloadSource(string key)
        {
            var root = FindProjectRoot();
            var candidates = new List<string>();
            if (root != null)
            {
                candidates.Add(Path.Combine(root, "USB Payloads", key));
                candidates.Add(Path.Combine(root, "Payloads", key));
                candidates.Add(Path.Combine(root, key));
            }
            foreach (var c in candidates) { try { if (Directory.Exists(c)) return c; } catch { } }
            return null;
        }

        private void BrowsePayloadSource()
        {
            using (var fbd = new FolderBrowserDialog { Description = "Select the payload source folder — its contents are written to the USB root." })
            {
                var cur = txtPayloadSource.Text;
                try { if (Directory.Exists(cur)) fbd.SelectedPath = cur; } catch { }
                if (fbd.ShowDialog(this) == DialogResult.OK) txtPayloadSource.Text = fbd.SelectedPath;
            }
        }

        private void FormatUsb()
        {
            if (_opBusy) { Log("Busy."); return; }
            var d = SelectedUsbDrive();
            if (d == null) { Warn("Select a USB drive first (Refresh if none listed)."); return; }

            var msg = "This opens the Windows Format dialog for:\r\n\r\n" +
                      $"    {d.Letter}:\\   {d.Label}   {d.TotalGB:0.0} GB\r\n\r\n" +
                      "ALL DATA on that drive will be erased. In the dialog, set the File system to FAT32, then click Start.\r\n\r\nProceed?";
            if (MessageBox.Show(this, msg, "Format USB (FAT32)", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            Log($"Opening Windows format dialog for {d.Letter}:\\ (select FAT32).");
            string r = UsbPrep.OpenFormatDialog(this.Handle, d.Letter);
            Log("  " + r);
            RefreshUsbDrives();
        }

        private void WriteUsb()
        {
            // The Recore payload uses a different pipeline (extract a recore image to the USB).
            int pcur = comboPayload.SelectedIndex;
            if (pcur >= 0 && pcur < _payloads.Length && _payloads[pcur].Key == "Recore") { BuildRecoreUsb(); return; }

            if (_opBusy) { Log("Busy."); return; }
            var d = SelectedUsbDrive();
            if (d == null) { Warn("Select a USB drive first (Refresh if none listed)."); return; }
            if (!d.Ready) { Warn("The selected USB drive is not ready / inserted."); return; }
            var src = (txtPayloadSource.Text ?? "").Trim();
            if (string.IsNullOrEmpty(src) || !Directory.Exists(src)) { Warn("Select a valid payload source folder (Browse)."); return; }
            int pi = comboPayload.SelectedIndex;
            string pname = (pi >= 0 && pi < _payloads.Length) ? _payloads[pi].Name : "payload";

            var msg = "Write this payload to the USB drive:\r\n\r\n" +
                      $"    Payload: {pname}\r\n    Source:  {src}\r\n    USB:     {d.Letter}:\\   {d.Label}   {d.TotalGB:0.0} GB\r\n\r\n" +
                      "⚠  Use ONLY the software level correct for this vehicle — the wrong level PERMANENTLY destroys the ICC.\r\n" +
                      "The USB should already be FAT32-formatted (use step 1 first).\r\n\r\nProceed?";
            if (MessageBox.Show(this, msg, "Write payload to USB", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            _opBusy = true; _opAbort = false;
            SetUsbBusy(true);
            progress.Value = 0; lblPhase.Text = "Writing USB...";
            Log("---- Write payload to USB ----");
            Log($"  Payload: {pname}  |  Source: {src}  |  USB: {d.Letter}:\\");
            string destRoot = d.Root;
            var t = new Thread(() =>
            {
                try
                {
                    string err;
                    bool ok = UsbPrep.CopyPayload(src, destRoot, Log, OnProgress, () => _opAbort, out err);
                    if (ok)
                    {
                        Log("USB payload written and verified. Eject safely, then follow the Stage-2 on-vehicle steps.");
                        UiInvoke(() => MessageBox.Show(this, "USB payload written and verified.\r\n\r\nEject the USB safely, then follow the on-vehicle Stage-2 procedure in the tab.", "USB ready", MessageBoxButtons.OK, MessageBoxIcon.Information));
                    }
                    else
                    {
                        Log("USB write failed: " + err);
                        UiInvoke(() => MessageBox.Show(this, "USB write failed:\r\n" + err, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    }
                }
                catch (Exception ex) { Log("USB write error: " + ex.Message); }
                finally { _opBusy = false; UiInvoke(() => { SetUsbBusy(false); lblPhase.Text = "Idle."; }); }
            }) { IsBackground = true };
            t.Start();
        }

        private void SetUsbBusy(bool busy)
        {
            Control[] c = { btnFormatUsb, btnWriteUsb, comboUsbDrive, comboPayload, btnBrowsePayload, btnUsbRefresh, btnFlash, btnReadInfo,
                            btnReadConfig, btnLoadAbt, btnSaveAbt, btnWriteVin, btnWriteZone, btnApplyRaw, btnWriteBlock,
                            comboRecoreBackup, btnLoadCatalog, btnEditScript };
            foreach (var x in c) x.Enabled = !busy;
            btnUsbAbort.Enabled = busy;
        }

        // ---------------- Configuration (As-Built) ----------------

        private bool _gridLoading;

        private void PopulateConfigUi(AsBuiltConfig cfg)
        {
            UiInvoke(() =>
            {
                _config = cfg;
                RefreshConfigGrid();
                var vin = cfg.Vin; if (!string.IsNullOrEmpty(vin)) txtVin.Text = vin;
                var zone = cfg.ZoneDual; if (zone.HasValue) comboZone.SelectedIndex = zone.Value ? 1 : 0;
            });
        }

        /// <summary>Rebuild the raw-block grid from _config (one row per location, Hex column editable).</summary>
        private void RefreshConfigGrid()
        {
            _gridLoading = true;
            try
            {
                gridConfig.Rows.Clear();
                foreach (var loc in _config.Blocks.Keys.OrderBy(k => k))
                {
                    var d = _config.Blocks[loc] ?? new byte[0];
                    int idx = gridConfig.Rows.Add(
                        "0x" + loc.ToString("X2"),
                        (loc + 1).ToString(),
                        AsBuiltConfig.MeaningOf(loc),
                        HexUtil.ToHex(d),
                        AsBuiltConfig.Ascii(d));
                    gridConfig.Rows[idx].Tag = loc;
                }
            }
            finally { _gridLoading = false; }
        }

        /// <summary>A Hex cell was edited: parse it, update _config, refresh the ASCII mirror and VIN/Zone shortcuts.</summary>
        private void OnConfigCellEndEdit(int rowIndex, int colIndex)
        {
            if (_gridLoading || rowIndex < 0 || rowIndex >= gridConfig.Rows.Count) return;
            var row = gridConfig.Rows[rowIndex];
            if (!(row.Tag is byte)) return;
            byte loc = (byte)row.Tag;
            if (gridConfig.Columns[colIndex].Name != "colHex") return;

            string hex = Convert.ToString(row.Cells["colHex"].Value) ?? "";
            byte[] bytes;
            try { bytes = HexUtil.FromHex(hex); }
            catch (Exception ex)
            {
                Warn("Invalid hex for block " + (loc + 1) + ": " + ex.Message);
                _gridLoading = true;
                try { row.Cells["colHex"].Value = HexUtil.ToHex(_config.Get(loc) ?? new byte[0]); }
                finally { _gridLoading = false; }
                return;
            }
            _config.Set(loc, bytes);
            _gridLoading = true;
            try
            {
                row.Cells["colHex"].Value = HexUtil.ToHex(bytes);
                row.Cells["colAscii"].Value = AsBuiltConfig.Ascii(bytes);
            }
            finally { _gridLoading = false; }

            // Keep the VIN / Zone convenience editors in sync with raw edits.
            if (loc == AsBuiltConfig.LocVin) { var v = _config.Vin; if (!string.IsNullOrEmpty(v)) txtVin.Text = v; }
            if (loc == AsBuiltConfig.LocZone) { var z = _config.ZoneDual; if (z.HasValue) comboZone.SelectedIndex = z.Value ? 1 : 0; }
            Log($"  block {loc + 1} (loc 0x{loc:X2}) edited -> {HexUtil.ToHex(bytes)}");
        }

        /// <summary>Commit any in-progress grid edit into _config (also folds in the VIN/Zone editors).</summary>
        private void ApplyRawEdits()
        {
            try { gridConfig.EndEdit(); } catch { }
            var vin = (txtVin.Text ?? "").Trim();
            if (vin.Length == 17) _config.Set(AsBuiltConfig.LocVin, System.Text.Encoding.ASCII.GetBytes(vin));
            if (comboZone.SelectedIndex >= 0) _config.Set(AsBuiltConfig.LocZone, _config.BuildZoneBlock(comboZone.SelectedIndex == 1));
            RefreshConfigGrid();
            Log("Raw As-Built edits applied to the working set. Use Save ABT… to export, or Write to apply to the module.");
        }

        /// <summary>Write the grid's selected block to the module ($3B, security-gated).</summary>
        private void WriteSelectedBlock()
        {
            if (gridConfig.CurrentRow == null || !(gridConfig.CurrentRow.Tag is byte)) { Warn("Select a block row first."); return; }
            byte loc = (byte)gridConfig.CurrentRow.Tag;
            var data = _config.Get(loc);
            if (data == null) { Warn("That block has no data."); return; }
            if (MessageBox.Show(this,
                    $"Write As-Built block {loc + 1} (loc 0x{loc:X2}) to the module?\r\n\r\n    3B {loc:X2} {HexUtil.ToHex(data)}\r\n\r\nThis needs security access and can misconfigure the ICC if wrong.",
                    "Write As-Built block", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            RunOp("Write block " + (loc + 1), true, 0x87, true, true, (g, o) => ConfigOps.WriteBlock(g, o, Log, loc, data));
        }

        /// <summary>Export the working As-Built set to a ForScan .abt file (round-trips a loaded layout).</summary>
        private void SaveAbtFile()
        {
            ApplyRawEdits();
            if (_config.RawLines.Count == 0)
            {
                Warn("Save round-trips a loaded ABT's row layout.\r\nLoad an ABT file first (the module read does not carry the ForScan row/line structure).");
                return;
            }
            using (var sfd = new SaveFileDialog { Filter = "As-Built files (*.abt)|*.abt|All files (*.*)|*.*", Title = "Save As-Built (ABT) file", FileName = "fdim.abt" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try { Log("---- Save ABT: " + Path.GetFileName(sfd.FileName) + " ----"); AbtFile.Save(_config, sfd.FileName, Log); }
                catch (Exception ex) { Warn("Failed to save ABT: " + ex.Message); }
            }
        }

        private void ReadConfigFromModule()
        {
            RunOp("Read As-Built", true, 0x87, false, true, (g, o) =>
            {
                var cfg = ConfigOps.ReadConfig(g, o, Log, AsBuiltConfig.DefaultLocations);
                PopulateConfigUi(cfg);
            });
        }

        private void LoadAbtFile()
        {
            using (var ofd = new OpenFileDialog { Filter = "As-Built files (*.abt;*.ab;*.txt)|*.abt;*.ab;*.txt|All files (*.*)|*.*", Title = "Load As-Built (ABT) file" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Log("---- Load ABT: " + Path.GetFileName(ofd.FileName) + " ----");
                    var cfg = AbtFile.Parse(ofd.FileName, Log);
                    PopulateConfigUi(cfg);
                    Log("ABT loaded into the Configuration tab. Review, then Write to apply to the module.");
                }
                catch (Exception ex) { Warn("Failed to parse ABT file: " + ex.Message); }
            }
        }

        private void WriteVinToModule()
        {
            var vin = (txtVin.Text ?? "").Trim();
            if (vin.Length != 17) { Warn("VIN must be exactly 17 characters."); return; }
            if (MessageBox.Show(this, $"Write VIN '{vin}' to As-Built block 0x01?\r\n\r\nNote: the module usually REJECTS a VIN rewrite.", "Write VIN", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            var bytes = System.Text.Encoding.ASCII.GetBytes(vin);
            RunOp("Write VIN", true, 0x87, true, true, (g, o) => ConfigOps.WriteBlock(g, o, Log, AsBuiltConfig.LocVin, bytes));
        }

        private void WriteZoneToModule()
        {
            bool dual = comboZone.SelectedIndex == 1;
            var block = _config.BuildZoneBlock(dual);
            if (MessageBox.Show(this, $"Set climate zone to {(dual ? "DUAL" : "SINGLE")}?\r\n\r\nWrites As-Built block 0x03 = {HexUtil.ToHex(block)}.", "Write Zone", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            RunOp("Write Zone", true, 0x87, true, true, (g, o) => ConfigOps.WriteBlock(g, o, Log, AsBuiltConfig.LocZone, block));
        }

        private void Warn(string msg) => MessageBox.Show(this, msg, "FG2ICCFlash", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private void Log(string message)
        {
            try
            {
                if (txtLog.IsDisposed || !txtLog.IsHandleCreated) return;
                if (txtLog.InvokeRequired) { txtLog.BeginInvoke((Action)(() => Log(message))); return; }
                txtLog.AppendText((message ?? "").TrimEnd('\r', '\n') + "\r\n");
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }
}
