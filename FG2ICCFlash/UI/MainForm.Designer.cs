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
            this.headerPanel = new Panel { Location = new Point(0, 0), Size = new Size(852, 82), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            this.titleLabel = new Label { Text = "FG2ICCFlash", Location = new Point(14, 12), AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(0x00, 0x24, 0x7D) };
            this.subtitleLabel = new Label { Text = "Front Display Interface Module (0x7A6)  ·  CAN GDS v2003  ·  SAE J2534 PassThru", Location = new Point(16, 48), AutoSize = true, ForeColor = Color.DimGray };
            this.logoPicture = new PictureBox { Location = new Point(648, 8), Size = new Size(194, 66), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.Transparent };
            this.headerPanel.Controls.AddRange(new Control[] { titleLabel, subtitleLabel, logoPicture });

            // ---------------- Tabs ----------------
            this.tabs = new TabControl { Location = new Point(12, 88), Size = new Size(828, 356), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            this.tabFlash = new TabPage { Text = "Flash" };
            this.tabDiag = new TabPage { Text = "Diagnostics && Routines" };
            this.tabs.TabPages.AddRange(new TabPage[] { tabFlash, tabDiag });

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
            this.comboKey = new ComboBox { Location = new Point(90, 22), Size = new Size(118, 23), DropDownStyle = ComboBoxStyle.DropDownList };
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

            // ---------------- Progress + log ----------------
            this.progress = new ProgressBar { Location = new Point(12, 452), Size = new Size(828, 18), Minimum = 0, Maximum = 100, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            this.lblPhase = new Label { Text = "Idle.", Location = new Point(12, 474), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            this.txtLog = new TextBox
            {
                Location = new Point(12, 496),
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
            this.ClientSize = new Size(852, 716);
            this.MinimumSize = new Size(868, 620);
            this.Controls.AddRange(new Control[] { headerPanel, tabs, progress, lblPhase, txtLog, statusStrip });
            this.Text = "FG2ICCFlash | Tester Present Specialist Automotive Solutions";
            this.StartPosition = FormStartPosition.CenterScreen;
        }
    }
}
