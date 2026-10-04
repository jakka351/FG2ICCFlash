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

        private ComboBox comboDevice;
        private Button btnRefresh;
        private ComboBox comboBus;
        private TextBox txtTxId;
        private TextBox txtRxId;
        private Label lblVoltage;
        private TextBox txtSbl;
        private Button btnBrowseSbl;
        private TextBox txtApp;
        private Button btnBrowseApp;
        private Button btnAutoDetect;
        private Label lblSblInfo;
        private Label lblAppInfo;
        private ComboBox comboKey;
        private TextBox txtSession;
        private ComboBox comboDfi;
        private CheckBox chkDryRun;
        private CheckBox chkSbl;
        private CheckBox chkErase;
        private CheckBox chkVerify;
        private CheckBox chkReset;
        private Button btnReadInfo;
        private Button btnFlash;
        private Button btnAbort;
        private ProgressBar progress;
        private Label lblPhase;
        private TextBox txtLog;
        private GroupBox grpConn;
        private GroupBox grpFw;
        private GroupBox grpOpt;

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // ---- Connection group ----
            this.grpConn = new GroupBox { Text = "Interface", Location = new Point(12, 8), Size = new Size(788, 86) };
            var lblDev = new Label { Text = "J2534 Device:", Location = new Point(12, 26), AutoSize = true };
            this.comboDevice = new ComboBox { Location = new Point(100, 22), Size = new Size(300, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnRefresh = new Button { Text = "Refresh", Location = new Point(408, 21), Size = new Size(70, 25) };
            var lblBus = new Label { Text = "Bus:", Location = new Point(496, 26), AutoSize = true };
            this.comboBus = new ComboBox { Location = new Point(530, 22), Size = new Size(240, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            var lblTx = new Label { Text = "Req ID:", Location = new Point(12, 56), AutoSize = true };
            this.txtTxId = new TextBox { Text = "7A6", Location = new Point(100, 53), Size = new Size(60, 23) };
            var lblRx = new Label { Text = "Resp ID:", Location = new Point(176, 56), AutoSize = true };
            this.txtRxId = new TextBox { Text = "7AE", Location = new Point(236, 53), Size = new Size(60, 23) };
            this.lblVoltage = new Label { Text = "Battery: --", Location = new Point(530, 56), AutoSize = true };
            this.grpConn.Controls.AddRange(new Control[] { lblDev, comboDevice, btnRefresh, lblBus, comboBus, lblTx, txtTxId, lblRx, txtRxId, lblVoltage });

            // ---- Firmware group ----
            this.grpFw = new GroupBox { Text = "Firmware (PHF)", Location = new Point(12, 100), Size = new Size(788, 128) };
            var lblSbl = new Label { Text = "Flash driver (SBL):", Location = new Point(12, 24), AutoSize = true };
            this.txtSbl = new TextBox { Location = new Point(130, 21), Size = new Size(560, 23), ReadOnly = true };
            this.btnBrowseSbl = new Button { Text = "Browse...", Location = new Point(698, 20), Size = new Size(78, 25) };
            this.lblSblInfo = new Label { Text = "", Location = new Point(130, 46), AutoSize = true, ForeColor = Color.DimGray };
            var lblApp = new Label { Text = "Application:", Location = new Point(12, 70), AutoSize = true };
            this.txtApp = new TextBox { Location = new Point(130, 67), Size = new Size(560, 23), ReadOnly = true };
            this.btnBrowseApp = new Button { Text = "Browse...", Location = new Point(698, 66), Size = new Size(78, 25) };
            this.lblAppInfo = new Label { Text = "", Location = new Point(130, 92), AutoSize = true, ForeColor = Color.DimGray };
            this.btnAutoDetect = new Button { Text = "Auto-detect from Firmware folder", Location = new Point(12, 92), Size = new Size(210, 25) };
            this.grpFw.Controls.AddRange(new Control[] { lblSbl, txtSbl, btnBrowseSbl, lblSblInfo, lblApp, txtApp, btnBrowseApp, lblAppInfo, btnAutoDetect });

            // ---- Options group ----
            this.grpOpt = new GroupBox { Text = "Options", Location = new Point(12, 234), Size = new Size(788, 96) };
            var lblKey = new Label { Text = "Security key:", Location = new Point(12, 26), AutoSize = true };
            this.comboKey = new ComboBox { Location = new Point(100, 22), Size = new Size(140, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            var lblSess = new Label { Text = "Session:", Location = new Point(258, 26), AutoSize = true };
            this.txtSession = new TextBox { Text = "85", Location = new Point(312, 23), Size = new Size(44, 23) };
            var lblDfi = new Label { Text = "DFI:", Location = new Point(372, 26), AutoSize = true };
            this.comboDfi = new ComboBox { Location = new Point(404, 22), Size = new Size(90, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            this.chkDryRun = new CheckBox { Text = "Dry Run (simulate, no hardware)", Location = new Point(520, 24), AutoSize = true, Checked = true };
            this.chkSbl = new CheckBox { Text = "Download SBL", Location = new Point(12, 58), AutoSize = true, Checked = true };
            this.chkErase = new CheckBox { Text = "Erase flash", Location = new Point(140, 58), AutoSize = true, Checked = true };
            this.chkVerify = new CheckBox { Text = "Verify", Location = new Point(250, 58), AutoSize = true, Checked = true };
            this.chkReset = new CheckBox { Text = "ECU reset after", Location = new Point(330, 58), AutoSize = true, Checked = true };
            this.grpOpt.Controls.AddRange(new Control[] { lblKey, comboKey, lblSess, txtSession, lblDfi, comboDfi, chkDryRun, chkSbl, chkErase, chkVerify, chkReset });

            // ---- Action buttons ----
            this.btnReadInfo = new Button { Text = "Read Module Info", Location = new Point(12, 338), Size = new Size(130, 30) };
            this.btnFlash = new Button { Text = "Flash", Location = new Point(560, 338), Size = new Size(120, 30) };
            this.btnAbort = new Button { Text = "Abort", Location = new Point(686, 338), Size = new Size(114, 30), Enabled = false };

            // ---- Progress ----
            this.progress = new ProgressBar { Location = new Point(12, 376), Size = new Size(788, 18), Minimum = 0, Maximum = 100 };
            this.lblPhase = new Label { Text = "Idle.", Location = new Point(12, 398), AutoSize = true };

            // ---- Log ----
            this.txtLog = new TextBox
            {
                Location = new Point(12, 420),
                Size = new Size(788, 220),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5f),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // ---- Form ----
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(812, 652);
            this.MinimumSize = new Size(828, 540);
            this.Controls.AddRange(new Control[] { grpConn, grpFw, grpOpt, btnReadInfo, btnFlash, btnAbort, progress, lblPhase, txtLog });
            this.Text = "FG2ICC Flasher — FDIM 0x7A6 (CAN GDS v2003 / J2534)";
            this.StartPosition = FormStartPosition.CenterScreen;
        }
    }
}
