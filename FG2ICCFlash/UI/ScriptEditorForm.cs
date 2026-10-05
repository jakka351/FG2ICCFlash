using System;
using System.Drawing;
using System.Windows.Forms;
using FG2ICCFlasher.Core;

namespace FG2ICCFlasher.UI
{
    /// <summary>
    /// A small QNX shell-script editor for the USB recore worker (z.sh). It offers the factory
    /// templates and an insert palette of vetted snippets, and always returns LF-terminated text.
    /// </summary>
    public sealed class ScriptEditorForm : Form
    {
        private readonly TextBox _txt;
        private readonly ComboBox _snippets;
        private readonly ComboBox _templates;

        /// <summary>The edited script, normalised to LF line endings.</summary>
        public string ScriptText => RecoreScripts.ToUnix(_txt.Text);

        public ScriptEditorForm(string initial, string title)
        {
            Text = title ?? "Recore script editor (z.sh)";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = true;
            ClientSize = new Size(760, 520);
            MinimumSize = new Size(560, 360);
            Font = new Font("Segoe UI", 9f);

            var bar = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(8) };

            var lblT = new Label { Text = "Template:", Location = new Point(10, 12), AutoSize = true };
            _templates = new ComboBox { Location = new Point(76, 8), Size = new Size(260, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            _templates.Items.AddRange(new object[] { "Factory package-tree recore (z.sh)", "Gauges + FPV logo mods (z.sh)" });
            _templates.SelectedIndex = 0;
            var btnTpl = new Button { Text = "Load template", Location = new Point(344, 7), Size = new Size(110, 25) };
            btnTpl.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Replace the current script with the selected template?", "Load template",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _txt.Text = RecoreScripts.ToUnix(_templates.SelectedIndex == 1
                    ? RecoreScripts.GaugesModsWorker
                    : RecoreScripts.FactoryRecoreWorker()).Replace("\n", "\r\n");
            };

            var lblS = new Label { Text = "Insert snippet:", Location = new Point(10, 42), AutoSize = true };
            _snippets = new ComboBox { Location = new Point(96, 38), Size = new Size(358, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var kv in RecoreScripts.Snippets) _snippets.Items.Add(kv.Key);
            if (_snippets.Items.Count > 0) _snippets.SelectedIndex = 0;
            var btnIns = new Button { Text = "Insert", Location = new Point(462, 37), Size = new Size(80, 25) };
            btnIns.Click += (s, e) =>
            {
                int i = _snippets.SelectedIndex;
                if (i < 0 || i >= RecoreScripts.Snippets.Count) return;
                string snip = RecoreScripts.ToUnix(RecoreScripts.Snippets[i].Value).Replace("\n", "\r\n");
                int at = _txt.SelectionStart;
                _txt.Text = _txt.Text.Insert(at, snip);
                _txt.SelectionStart = at + snip.Length;
                _txt.Focus();
            };

            var btnLf = new Button { Text = "Normalise to LF", Location = new Point(560, 7), Size = new Size(120, 25) };
            btnLf.Click += (s, e) => _txt.Text = RecoreScripts.ToUnix(_txt.Text).Replace("\n", "\r\n");
            var lblHint = new Label { Text = "Saved with LF endings for QNX.", Location = new Point(560, 42), AutoSize = true, ForeColor = Color.DimGray };

            bar.Controls.AddRange(new Control[] { lblT, _templates, btnTpl, lblS, _snippets, btnIns, btnLf, lblHint });

            _txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                AcceptsTab = true,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E),
                ForeColor = Color.Gainsboro,
                Text = (initial ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n")
            };

            var foot = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
            var ok = new Button { Text = "Use this script", DialogResult = DialogResult.OK, Location = new Point(540, 8), Size = new Size(120, 28) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(666, 8), Size = new Size(84, 28) };
            foot.Controls.AddRange(new Control[] { ok, cancel });
            AcceptButton = ok; CancelButton = cancel;

            Controls.Add(_txt);
            Controls.Add(foot);
            Controls.Add(bar);
        }
    }
}
