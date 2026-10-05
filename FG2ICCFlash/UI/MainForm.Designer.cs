using System.Drawing;
using System.Windows.Forms;

namespace FG2ICCFlasher.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        // Header
        private Panel headerPanel;
        private Label titleLabel;
        private Label subtitleLabel;
        private PictureBox logoPicture;

        // Tabs
        private TabControl tabs;
        private TabPage tabFlash;
        private TabPage tabDiag;

        // Flash tab — interface
        private GroupBox grpConn;
        private ComboBox comboDevice;
        private Button btnRefresh;
        private ComboBox comboBus;
        private TextBox txtTxId;
        private TextBox txtRxId;
        private Label lblVoltage;

        // Flash tab — firmware
        private GroupBox grpFw;
        private ComboBox comboApp;
        private Button btnBrowseApp;
        private Label lblAppInfo;
        private ComboBox comboSbl;
        private Button btnBrowseSbl;
        private Label lblSblInfo;

        // Flash tab — options
        private GroupBox grpOpt;
        private ComboBox comboKey;
        private TextBox txtSession;
        private ComboBox comboDfi;
        private CheckBox chkSbl;
        private CheckBox chkErase;
        private CheckBox chkVerify;
        private CheckBox chkReset;
        private Button btnFlash;
        private Button btnAbort;

        // Diagnostics tab
        private GroupBox grpIdent;
        private Button btnReadInfo;
        private Button btnReadDtc;
        private Button btnClearDtc;
        private GroupBox grpRoutines;
        private Button btnSelfTest;
        private Button btnEolSelfTest;
        private Button btnRecore;
        private TextBox txtCustom;
        private Button btnRunCustom;
        private CheckBox chkCustomUnlock;

        // Stage 2 — USB tab
        private TabPage tabUsb;
        private GroupBox grpUsbDrive;
        private ComboBox comboUsbDrive;
        private Button btnUsbRefresh;
        private Label lblUsbInfo;
        private GroupBox grpUsbPayload;
        private ComboBox comboPayload;
        private TextBox txtPayloadSource;
        private Button btnBrowsePayload;
        private Label lblPayloadInfo;
        private Button btnFormatUsb;
        private Button btnWriteUsb;
        private Button btnUsbAbort;
        private TextBox txtUsbInstructions;
        private GroupBox grpRecore;
        private ComboBox comboRecoreBackup;
        private Label lblRecoreInfo;
        private TextBox txtRecoreUrl;
        private Button btnLoadCatalog;
        private Button btnEditScript;

        // Configuration tab
        private TabPage tabConfig;
        private GroupBox grpConfigSrc;
        private Button btnReadConfig;
        private Button btnLoadAbt;
        private Button btnSaveAbt;
        private Label lblConfigStatus;
        private GroupBox grpConfigEdit;
        private TextBox txtVin;
        private Button btnWriteVin;
        private ComboBox comboZone;
        private Button btnWriteZone;
        private DataGridView gridConfig;
        private Button btnApplyRaw;
        private Button btnWriteBlock;
        private Label lblRawHeader;

        // Progress + log
        private ProgressBar progress;
        private Label lblPhase;
        private TextBox txtLog;

        // Status bar
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusFlagLabel;
        private ToolStripStatusLabel statusSpring;
        private ToolStripStatusLabel statusCompanyLabel;

        private static Label Lbl(string t, int x, int y) => new Label { Text = t, Location = new Point(x, y), AutoSize = true };

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // ---------------- Header ----------------
            this.headerPanel = new Panel { Location = new Point(0, 0), Size = new Size(852, 110), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            this.titleLabel = new Label { Text = "FG2ICCFlash", Location = new Point(14, 18), AutoSize = true, Font = new Font("Segoe UI", 16.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0x00, 0x24, 0x7D) };
            this.subtitleLabel = new Label { Text = "Front Display Interface Module (0x7A6)  ·  CAN GDS v2003  ·  SAE J2534 PassThru", Location = new Point(16, 56), AutoSize = true, ForeColor = Color.DimGray };
            this.logoPicture = new PictureBox { Location = new Point(566, 8), Size = new Size(276, 96), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.Transparent };
            this.headerPanel.Controls.AddRange(new Control[] { titleLabel, subtitleLabel, logoPicture });

            // ---------------- Tabs ----------------
            this.tabs = new TabControl { Location = new Point(12, 116), Size = new Size(828, 356), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            this.tabFlash = new TabPage { Text = "Stage 1 Flash CAN" };
            this.tabUsb = new TabPage { Text = "Stage 2 — ICC USB" };
            this.tabDiag = new TabPage { Text = "Diagnostics && Routines" };
            this.tabConfig = new TabPage { Text = "Configuration" };
            this.tabs.TabPages.AddRange(new TabPage[] { tabFlash, tabUsb, tabDiag, tabConfig });

            // ---- Interface group ----
            this.grpConn = new GroupBox { Text = "Interface", Location = new Point(8, 8), Size = new Size(804, 84) };
            this.comboDevice = new ComboBox { Location = new Point(100, 22), Size = new Size(300, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnRefresh = new Button { Text = "Refresh", Location = new Point(408, 21), Size = new Size(72, 25) };
            this.comboBus = new ComboBox { Location = new Point(534, 22), Size = new Size(262, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.txtTxId = new TextBox { Text = "7A6", Location = new Point(100, 52), Size = new Size(58, 23) };
            this.txtRxId = new TextBox { Text = "7AE", Location = new Point(236, 52), Size = new Size(58, 23) };
            this.lblVoltage = new Label { Text = "Battery: --", Location = new Point(534, 55), AutoSize = true };
            this.grpConn.Controls.AddRange(new Control[] {
                Lbl("J2534 Device:", 12, 26), comboDevice, btnRefresh, Lbl("Bus:", 492, 26), comboBus,
                Lbl("Req ID:", 12, 55), txtTxId, Lbl("Resp ID:", 176, 55), txtRxId, lblVoltage });

            // ---- Firmware group ----
            this.grpFw = new GroupBox { Text = "Firmware (PHF) — embedded, or Browse for a file", Location = new Point(8, 98), Size = new Size(804, 122) };
            this.comboApp = new ComboBox { Location = new Point(100, 24), Size = new Size(606, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnBrowseApp = new Button { Text = "Browse...", Location = new Point(714, 23), Size = new Size(80, 25) };
            this.lblAppInfo = new Label { Text = "", Location = new Point(100, 50), AutoSize = true, ForeColor = Color.DimGray };
            this.comboSbl = new ComboBox { Location = new Point(100, 78), Size = new Size(606, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnBrowseSbl = new Button { Text = "Browse...", Location = new Point(714, 77), Size = new Size(80, 25) };
            this.lblSblInfo = new Label { Text = "", Location = new Point(100, 104), AutoSize = true, ForeColor = Color.DimGray };
            this.grpFw.Controls.AddRange(new Control[] {
                Lbl("Application:", 12, 27), comboApp, btnBrowseApp, lblAppInfo,
                Lbl("Flash driver:", 12, 81), comboSbl, btnBrowseSbl, lblSblInfo });

            // ---- Options group ----
            this.grpOpt = new GroupBox { Text = "Flash options", Location = new Point(8, 228), Size = new Size(804, 58) };
            // Editable so a custom 5-byte hex key (e.g. from an external key-finder) can be typed/pasted.
            this.comboKey = new ComboBox { Location = new Point(90, 22), Size = new Size(118, 23), DropDownStyle = ComboBoxStyle.DropDown };
            this.txtSession = new TextBox { Text = "85", Location = new Point(268, 23), Size = new Size(38, 23) };
            this.comboDfi = new ComboBox { Location = new Point(344, 22), Size = new Size(60, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.chkSbl = new CheckBox { Text = "Download SBL", Location = new Point(420, 24), AutoSize = true, Checked = true };
            this.chkErase = new CheckBox { Text = "Erase", Location = new Point(534, 24), AutoSize = true, Checked = true };
            this.chkVerify = new CheckBox { Text = "Verify", Location = new Point(606, 24), AutoSize = true, Checked = true };
            this.chkReset = new CheckBox { Text = "ECU reset", Location = new Point(676, 24), AutoSize = true, Checked = true };
            this.grpOpt.Controls.AddRange(new Control[] {
                Lbl("Security key:", 10, 26), comboKey, Lbl("Session:", 216, 26), txtSession,
                Lbl("DFI:", 316, 26), comboDfi, chkSbl, chkErase, chkVerify, chkReset });

            this.btnFlash = new Button { Text = "Flash", Location = new Point(576, 296), Size = new Size(110, 32), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            this.btnAbort = new Button { Text = "Abort", Location = new Point(702, 296), Size = new Size(110, 32), Enabled = false };

            this.tabFlash.Controls.AddRange(new Control[] { grpConn, grpFw, grpOpt, btnFlash, btnAbort });

            // ---- Diagnostics tab ----
            this.grpIdent = new GroupBox { Text = "Identification && DTCs", Location = new Point(8, 8), Size = new Size(804, 76) };
            this.btnReadInfo = new Button { Text = "Read Module Info", Location = new Point(16, 28), Size = new Size(150, 32) };
            this.btnReadDtc = new Button { Text = "Read DTCs", Location = new Point(176, 28), Size = new Size(130, 32) };
            this.btnClearDtc = new Button { Text = "Clear DTCs", Location = new Point(316, 28), Size = new Size(130, 32) };
            this.grpIdent.Controls.AddRange(new Control[] { btnReadInfo, btnReadDtc, btnClearDtc,
                new Label { Text = "Reads identification DIDs;\r\nreads / clears stored DTCs.", Location = new Point(466, 26), AutoSize = true, ForeColor = Color.DimGray } });

            this.grpRoutines = new GroupBox { Text = "Routines (service $31)", Location = new Point(8, 92), Size = new Size(804, 206) };
            this.btnSelfTest = new Button { Text = "On-Demand Self Test", Location = new Point(16, 28), Size = new Size(200, 34) };
            this.btnEolSelfTest = new Button { Text = "EOL / Assembly Self Test", Location = new Point(16, 70), Size = new Size(200, 34) };
            this.btnRecore = new Button { Text = "Recore (USB re-image)", Location = new Point(16, 112), Size = new Size(200, 34) };
            var selfDesc = new Label { Text = "$31 02 — GDS on-demand self test; reports pass/fail + DTCs.", Location = new Point(228, 38), AutoSize = true, ForeColor = Color.DimGray };
            var eolDesc = new Label { Text = "$31 11 — assembly / end-of-line self test (enters adjustment session, unlocks).", Location = new Point(228, 80), AutoSize = true, ForeColor = Color.DimGray };
            var recoreDesc = new Label { Text = "$31 AB 01 — commands the FDIM to re-image its OS from USB (unlocks first).", Location = new Point(228, 122), AutoSize = true, ForeColor = Color.DimGray };

            this.txtCustom = new TextBox { Location = new Point(16, 164), Size = new Size(300, 23), Text = "31 02 00" };
            this.btnRunCustom = new Button { Text = "Send raw", Location = new Point(324, 163), Size = new Size(90, 25) };
            this.chkCustomUnlock = new CheckBox { Text = "Enter adjustment session + unlock first", Location = new Point(424, 166), AutoSize = true };
            this.grpRoutines.Controls.AddRange(new Control[] { btnSelfTest, btnEolSelfTest, btnRecore, selfDesc, eolDesc, recoreDesc,
                Lbl("Custom request (hex):", 16, 146), txtCustom, btnRunCustom, chkCustomUnlock });

            this.tabDiag.Controls.AddRange(new Control[] { grpIdent, grpRoutines });

            // ---- Stage 2 — ICC USB tab ----
            this.grpUsbDrive = new GroupBox { Text = "Target USB drive  (removable USB/SD only — fixed and system disks are never shown)", Location = new Point(8, 8), Size = new Size(804, 56) };
            this.comboUsbDrive = new ComboBox { Location = new Point(90, 22), Size = new Size(500, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnUsbRefresh = new Button { Text = "Refresh", Location = new Point(600, 21), Size = new Size(80, 25) };
            this.lblUsbInfo = new Label { Text = "", Location = new Point(690, 25), AutoSize = true, ForeColor = Color.DimGray };
            this.grpUsbDrive.Controls.AddRange(new Control[] { Lbl("USB drive:", 12, 25), comboUsbDrive, btnUsbRefresh, lblUsbInfo });

            this.grpUsbPayload = new GroupBox { Text = "Payload  (the selected folder's contents are written to the USB root)", Location = new Point(8, 70), Size = new Size(804, 84) };
            this.comboPayload = new ComboBox { Location = new Point(90, 22), Size = new Size(400, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.txtPayloadSource = new TextBox { Location = new Point(110, 52), Size = new Size(586, 23), ReadOnly = true };
            this.btnBrowsePayload = new Button { Text = "Browse...", Location = new Point(704, 51), Size = new Size(80, 25) };
            this.lblPayloadInfo = new Label { Text = "", Location = new Point(496, 25), AutoSize = true, ForeColor = Color.DimGray };
            this.grpUsbPayload.Controls.AddRange(new Control[] { Lbl("Payload:", 12, 25), comboPayload, lblPayloadInfo, Lbl("Source folder:", 12, 55), txtPayloadSource, btnBrowsePayload });

            this.btnFormatUsb = new Button { Text = "1.  Format USB (FAT32)…", Location = new Point(8, 164), Size = new Size(190, 30) };
            this.btnWriteUsb = new Button { Text = "2.  Write payload to USB", Location = new Point(206, 164), Size = new Size(190, 30), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            this.btnUsbAbort = new Button { Text = "Abort", Location = new Point(404, 164), Size = new Size(90, 30), Enabled = false };

            this.txtUsbInstructions = new TextBox
            {
                Location = new Point(8, 202),
                Size = new Size(804, 120),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(0xFF, 0xFD, 0xF0),
                Font = new Font("Segoe UI", 8.25f),
                Text =
                    "PREPARE THE USB  —  do this now, in this tool:\r\n" +
                    "   1.  Plug the USB stick into this PC, click Refresh, and select it above.\r\n" +
                    "   2.  Click \"1. Format USB (FAT32)…\"  — in the Windows dialog set File system = FAT32, then Start.\r\n" +
                    "   3.  Choose the Payload and its Source folder, then click \"2. Write payload to USB\". Wait for \"USB ready\", then safely eject.\r\n" +
                    "\r\nTHEN, ON THE VEHICLE  —  after the Stage-1 CAN flash (screen goes dark, then powers back up):\r\n" +
                    "   4.  External power supply on the battery, ignition ON. Insert the USB into the centre-console USB port.\r\n" +
                    "   5.  Wait for the USB light to stop flashing and the \"USB connected\" message to clear.\r\n" +
                    "   6.  Upload runs 5–30 min (TX 5–10 / TS 15–20 / Titanium 25–30). The USB light flashes — this is normal.\r\n" +
                    "   7.  Screen blanks up to 5 min, then the ICC reboots and a Bluetooth update runs (shows %). If it stalls below 17%, retry.\r\n" +
                    "   8.  When the original screen returns it is done: REMOVE the USB, then run a CMDTC self-test and clear any codes.\r\n" +
                    "\r\n⚠  Use ONLY the software level correct for this vehicle — the wrong level permanently destroys the ICC.\r\n" +
                    "⚠  Once the in-car upload starts, DO NOT remove the USB or switch the ignition OFF until it completes."
            };

            // Recore options panel — shown only when the "Recore" payload is selected (occupies the
            // instructions area; the instructions hide while it is visible).
            this.grpRecore = new GroupBox { Text = "Recore image  (factory package-tree re-image — select a community/backup image or load a catalog)", Location = new Point(8, 202), Size = new Size(804, 120), Visible = false };
            this.comboRecoreBackup = new ComboBox { Location = new Point(104, 20), Size = new Size(500, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnEditScript = new Button { Text = "Edit script (z.sh)…", Location = new Point(616, 19), Size = new Size(180, 25) };
            this.txtRecoreUrl = new TextBox { Location = new Point(150, 50), Size = new Size(454, 23) };
            this.btnLoadCatalog = new Button { Text = "Load catalog", Location = new Point(616, 49), Size = new Size(180, 25) };
            this.lblRecoreInfo = new Label { Text = "", Location = new Point(12, 82), Size = new Size(784, 32), ForeColor = Color.DimGray };
            this.grpRecore.Controls.AddRange(new Control[] {
                Lbl("Recore image:", 12, 24), comboRecoreBackup, btnEditScript,
                Lbl("Catalog URL / folder:", 12, 54), txtRecoreUrl, btnLoadCatalog, lblRecoreInfo });

            this.tabUsb.Controls.AddRange(new Control[] { grpUsbDrive, grpUsbPayload, btnFormatUsb, btnWriteUsb, btnUsbAbort, txtUsbInstructions, grpRecore });

            // ---- Configuration tab (As-Built) ----
            this.grpConfigSrc = new GroupBox { Text = "As-Built source  (read via $21 / write via $3B in the $10 87 adjustment session)", Location = new Point(8, 8), Size = new Size(804, 56) };
            this.btnReadConfig = new Button { Text = "Read As-Built from Module", Location = new Point(12, 19), Size = new Size(190, 28) };
            this.btnLoadAbt = new Button { Text = "Load ABT…", Location = new Point(210, 19), Size = new Size(110, 28) };
            this.btnSaveAbt = new Button { Text = "Save ABT…", Location = new Point(328, 19), Size = new Size(110, 28) };
            this.lblConfigStatus = new Label { Text = "Uses the device/bus/key from the Stage 1 tab.", Location = new Point(450, 25), AutoSize = true, ForeColor = Color.DimGray };
            this.grpConfigSrc.Controls.AddRange(new Control[] { btnReadConfig, btnLoadAbt, btnSaveAbt, lblConfigStatus });

            this.grpConfigEdit = new GroupBox { Text = "Configuration", Location = new Point(8, 70), Size = new Size(804, 96) };
            this.txtVin = new TextBox { Location = new Point(60, 24), Size = new Size(200, 23), CharacterCasing = CharacterCasing.Upper, MaxLength = 17, Font = new Font("Consolas", 9f) };
            this.btnWriteVin = new Button { Text = "Write VIN", Location = new Point(272, 23), Size = new Size(100, 26) };
            this.comboZone = new ComboBox { Location = new Point(60, 58), Size = new Size(160, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnWriteZone = new Button { Text = "Write Zone", Location = new Point(232, 57), Size = new Size(100, 26) };
            this.grpConfigEdit.Controls.AddRange(new Control[] {
                Lbl("VIN:", 12, 28), txtVin, btnWriteVin,
                new Label { Text = "(block 0x01 — the module usually rejects a VIN rewrite)", Location = new Point(380, 28), AutoSize = true, ForeColor = Color.DimGray },
                Lbl("Zone:", 12, 61), comboZone, btnWriteZone,
                new Label { Text = "(block 0x03 — 0x0B dual / 0x00 single)", Location = new Point(340, 61), AutoSize = true, ForeColor = Color.DimGray } });

            this.lblRawHeader = new Label { Text = "Raw As-Built blocks — edit the Hex column, press Enter, then Apply (or Write block to the module):", Location = new Point(10, 170), AutoSize = true, ForeColor = Color.FromArgb(0x00, 0x24, 0x7D) };
            this.gridConfig = new DataGridView
            {
                Location = new Point(8, 188),
                Size = new Size(804, 98),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5f),
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            this.gridConfig.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLoc", HeaderText = "Loc", Width = 44, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
            this.gridConfig.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBlk", HeaderText = "Blk", Width = 36, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
            this.gridConfig.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMeaning", HeaderText = "Meaning", Width = 236, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
            this.gridConfig.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHex", HeaderText = "Hex (editable)", Width = 300, SortMode = DataGridViewColumnSortMode.NotSortable });
            this.gridConfig.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAscii", HeaderText = "ASCII", Width = 170, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });

            this.btnApplyRaw = new Button { Text = "Apply edits", Location = new Point(8, 290), Size = new Size(120, 26) };
            this.btnWriteBlock = new Button { Text = "Write selected block…", Location = new Point(134, 290), Size = new Size(170, 26) };

            this.tabConfig.Controls.AddRange(new Control[] { grpConfigSrc, grpConfigEdit, lblRawHeader, gridConfig, btnApplyRaw, btnWriteBlock,
                new Label { Text = "⚠  Writes need security access.", Location = new Point(318, 296), AutoSize = true, ForeColor = Color.FromArgb(0xB0, 0x30, 0x00) } });

            // ---------------- Progress + log ----------------
            this.progress = new ProgressBar { Location = new Point(12, 480), Size = new Size(828, 18), Minimum = 0, Maximum = 100, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            this.lblPhase = new Label { Text = "Idle.", Location = new Point(12, 502), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            this.txtLog = new TextBox
            {
                Location = new Point(12, 524),
                Size = new Size(828, 190),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5f),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // ---------------- Status bar ----------------
            this.statusStrip = new StatusStrip { SizingGrip = true, BackColor = SystemColors.Control, ImageScalingSize = new Size(36, 18) };
            this.statusFlagLabel = new ToolStripStatusLabel { Text = "Developed in Australia", ImageAlign = ContentAlignment.MiddleLeft, TextAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText, Padding = new Padding(4, 0, 0, 0) };
            this.statusSpring = new ToolStripStatusLabel { Spring = true, Text = string.Empty };
            this.statusCompanyLabel = new ToolStripStatusLabel { Text = "Tester Present Specialist Automotive Solutions", TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 6, 0), Font = new Font("Segoe UI", 8.25f, FontStyle.Bold) };
            this.statusStrip.Items.AddRange(new ToolStripItem[] { statusFlagLabel, statusSpring, statusCompanyLabel });

            // ---------------- Form ----------------
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(852, 744);
            this.MinimumSize = new Size(868, 648);
            this.Controls.AddRange(new Control[] { headerPanel, tabs, progress, lblPhase, txtLog, statusStrip });
            this.Text = "FG2ICCFlash | Tester Present Specialist Automotive Solutions";
            this.StartPosition = FormStartPosition.CenterScreen;
        }
    }
}
