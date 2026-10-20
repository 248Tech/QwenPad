using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace QwenPad
{
    static class Ui
    {
        public static void MakeDialog(Form f, string title)
        {
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            f.Font = SystemFonts.MessageBoxFont;
            f.Padding = new Padding(10);
            f.Load += delegate { Theme.ApplyForm(f); };
        }

        public static Button MakeButton(string text, DialogResult result)
        {
            return new Button { Text = text, DialogResult = result, AutoSize = true, MinimumSize = new Size(88, 0), Margin = new Padding(4) };
        }

        public static FlowLayoutPanel ButtonRow(params Button[] buttons)
        {
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
            for (int i = buttons.Length - 1; i >= 0; i--) row.Controls.Add(buttons[i]);
            return row;
        }
    }

    /// <summary>One-line text prompt with an optional checkbox (Go To, Translate, Ask Qwen).</summary>
    sealed class InputDialog : Form
    {
        readonly TextBox input = new TextBox();
        readonly CheckBox check = new CheckBox();

        public string Value { get { return input.Text; } }
        public bool Checked { get { return check.Checked; } }
        public Func<string, string> Validator;

        public InputDialog(string title, string label, string value, int width, string checkText, bool checkValue, bool checkEnabled)
        {
            Ui.MakeDialog(this, title);
            var table = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 3, 3, 6) });
            input.Text = value;
            input.Width = width;
            table.Controls.Add(input);
            if (checkText != null)
            {
                check.Text = checkText;
                check.AutoSize = true;
                check.Checked = checkValue && checkEnabled;
                check.Enabled = checkEnabled;
                check.Margin = new Padding(3, 8, 3, 0);
                table.Controls.Add(check);
            }
            var ok = Ui.MakeButton("OK", DialogResult.None);
            var cancel = Ui.MakeButton("Cancel", DialogResult.Cancel);
            ok.Click += delegate
            {
                string err = Validator != null ? Validator(input.Text) : null;
                if (err != null) { MessageBox.Show(this, err, title, MessageBoxButtons.OK, MessageBoxIcon.Warning); input.Focus(); input.SelectAll(); return; }
                DialogResult = DialogResult.OK;
            };
            table.Controls.Add(Ui.ButtonRow(ok, cancel));
            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate
            {
                // Owners like sticky notes can sit at a screen edge; keep the dialog fully visible and in front.
                var wa = Screen.FromControl(this).WorkingArea;
                Location = new Point(Math.Max(wa.Left, Math.Min(Left, wa.Right - Width)), Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height)));
                Activate();
                input.Focus();
                input.SelectAll();
            };
        }
    }

    /// <summary>Modeless Find / Replace dialog, like Notepad's.</summary>
    sealed class FindReplaceDialog : Form
    {
        readonly MainForm main;
        readonly TextBox txtFind = new TextBox { Width = 260 };
        readonly TextBox txtReplace = new TextBox { Width = 260 };
        readonly Label lblReplace = new Label { Text = "Re&place with:", AutoSize = true, Anchor = AnchorStyles.Left };
        readonly CheckBox chkCase = new CheckBox { Text = "Match &case", AutoSize = true };
        readonly CheckBox chkWrap = new CheckBox { Text = "W&rap around", AutoSize = true };
        readonly RadioButton rbUp = new RadioButton { Text = "&Up", AutoSize = true };
        readonly RadioButton rbDown = new RadioButton { Text = "&Down", AutoSize = true, Checked = true };
        readonly GroupBox grpDir = new GroupBox { Text = "Direction", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        readonly Button btnFind = Ui.MakeButton("&Find Next", DialogResult.None);
        readonly Button btnReplace = Ui.MakeButton("&Replace", DialogResult.None);
        readonly Button btnReplaceAll = Ui.MakeButton("Replace &All", DialogResult.None);
        readonly Button btnCancel = Ui.MakeButton("Cancel", DialogResult.None);

        public FindReplaceDialog(MainForm main)
        {
            this.main = main;
            Ui.MakeDialog(this, "Find");
            StartPosition = FormStartPosition.Manual;

            var fields = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
            fields.Controls.Add(new Label { Text = "Fi&nd what:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            fields.Controls.Add(txtFind, 1, 0);
            fields.Controls.Add(lblReplace, 0, 1);
            fields.Controls.Add(txtReplace, 1, 1);

            var dirFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 0) };
            dirFlow.Controls.Add(rbUp);
            dirFlow.Controls.Add(rbDown);
            grpDir.Controls.Add(dirFlow);
            var checks = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(0, 10, 12, 0) };
            checks.Controls.Add(chkCase);
            checks.Controls.Add(chkWrap);
            var options = new FlowLayoutPanel { AutoSize = true };
            options.Controls.Add(checks);
            options.Controls.Add(grpDir);

            var left = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
            left.Controls.Add(fields);
            left.Controls.Add(options);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(10, 0, 0, 0) };
            foreach (var b in new[] { btnFind, btnReplace, btnReplaceAll, btnCancel })
            {
                b.Dock = DockStyle.Top;
                buttons.Controls.Add(b);
            }

            var outer = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
            outer.Controls.Add(left, 0, 0);
            outer.Controls.Add(buttons, 1, 0);
            Controls.Add(outer);

            AcceptButton = btnFind;
            CancelButton = btnCancel;

            txtFind.TextChanged += delegate { UpdateButtons(); };
            btnFind.Click += delegate { Sync(); main.FindNext(true); };
            btnReplace.Click += delegate { Sync(); main.ReplaceOne(); };
            btnReplaceAll.Click += delegate { Sync(); main.ReplaceAll(); };
            btnCancel.Click += delegate { Hide(); };
        }

        public void ShowMode(bool replace, string initialText, bool matchCase, bool wrap, bool up)
        {
            Text = replace ? "Replace" : "Find";
            lblReplace.Visible = txtReplace.Visible = btnReplace.Visible = btnReplaceAll.Visible = replace;
            grpDir.Visible = !replace;
            if (!string.IsNullOrEmpty(initialText)) txtFind.Text = initialText;
            chkCase.Checked = matchCase;
            chkWrap.Checked = wrap;
            if (up) rbUp.Checked = true; else rbDown.Checked = true;
            UpdateButtons();
            if (!Visible)
            {
                var o = Owner.Bounds;
                Location = new Point(o.Right - Width - 40, o.Top + 90);
                Show(Owner);
            }
            Activate();
            txtFind.Focus();
            txtFind.SelectAll();
        }

        void UpdateButtons()
        {
            btnFind.Enabled = btnReplace.Enabled = btnReplaceAll.Enabled = txtFind.Text.Length > 0;
        }

        void Sync()
        {
            main.SetSearch(txtFind.Text, txtReplace.Text, chkCase.Checked, chkWrap.Checked, grpDir.Visible && rbUp.Checked);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
    }

    /// <summary>Settings for the local Qwen model server.</summary>
    sealed class SettingsDialog : Form
    {
        readonly TextBox txtUrl = new TextBox { Width = 360 };
        readonly TextBox txtExe = new TextBox { Width = 360 };
        readonly TextBox txtModel = new TextBox { Width = 360 };
        readonly TextBox txtArgs = new TextBox { Width = 360 };
        readonly NumericUpDown numTokens = new NumericUpDown { Minimum = 64, Maximum = 32768, Increment = 256, Width = 90 };
        readonly NumericUpDown numTemp = new NumericUpDown { Minimum = 0, Maximum = 2, DecimalPlaces = 2, Increment = 0.05m, Width = 90 };
        readonly CheckBox chkAuto = new CheckBox { Text = "Start the local model automatically when QwenPad opens", AutoSize = true };

        public SettingsDialog(Settings s)
        {
            Ui.MakeDialog(this, "Qwen Settings");
            var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true };
            int row = 0;
            AddRow(t, row++, "llama-server.exe:", txtExe, Browse(txtExe, "llama-server|llama-server.exe|Programs (*.exe)|*.exe"));
            AddRow(t, row++, "Model (.gguf):", txtModel, Browse(txtModel, "GGUF models (*.gguf)|*.gguf|All files (*.*)|*.*"));
            AddRow(t, row++, "Server arguments:", txtArgs, null);
            AddRow(t, row++, "Server URL:", txtUrl, null);
            AddRow(t, row++, "Max new tokens:", numTokens, null);
            AddRow(t, row++, "Temperature:", numTemp, null);
            t.Controls.Add(chkAuto, 1, row++);
            t.SetColumnSpan(chkAuto, 2);

            var ok = Ui.MakeButton("OK", DialogResult.OK);
            var cancel = Ui.MakeButton("Cancel", DialogResult.Cancel);
            var buttons = Ui.ButtonRow(ok, cancel);
            t.Controls.Add(buttons, 0, row);
            t.SetColumnSpan(buttons, 3);
            Controls.Add(t);
            AcceptButton = ok;
            CancelButton = cancel;

            txtUrl.Text = s.ServerUrl;
            txtExe.Text = s.ServerExe;
            txtModel.Text = s.ModelPath;
            txtArgs.Text = s.ServerArgs;
            numTokens.Value = Math.Max(numTokens.Minimum, Math.Min(numTokens.Maximum, s.MaxTokens));
            numTemp.Value = (decimal)Math.Max(0, Math.Min(2, s.Temperature));
            chkAuto.Checked = s.AutoStartServer;
        }

        /// <summary>Copies values into settings; returns true if the server needs a restart.</summary>
        public bool Apply(Settings s)
        {
            bool restart = s.ServerUrl != txtUrl.Text.Trim() || s.ServerExe != txtExe.Text.Trim() ||
                           s.ModelPath != txtModel.Text.Trim() || s.ServerArgs != txtArgs.Text.Trim();
            s.ServerUrl = txtUrl.Text.Trim();
            s.ServerExe = txtExe.Text.Trim();
            s.ModelPath = txtModel.Text.Trim();
            s.ServerArgs = txtArgs.Text.Trim();
            s.MaxTokens = (int)numTokens.Value;
            s.Temperature = (double)numTemp.Value;
            s.AutoStartServer = chkAuto.Checked;
            return restart;
        }

        static void AddRow(TableLayoutPanel t, int row, string label, Control c, Control extra)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            t.Controls.Add(c, 1, row);
            if (extra != null) t.Controls.Add(extra, 2, row);
        }

        Button Browse(TextBox target, string filter)
        {
            var b = new Button { Text = "...", Width = 32, Height = target.Height + 2, Margin = new Padding(2, 2, 3, 2) };
            b.Click += delegate
            {
                using (var dlg = new OpenFileDialog { Filter = filter })
                {
                    try { dlg.FileName = Settings.Resolve(target.Text); } catch { }
                    if (dlg.ShowDialog(this) == DialogResult.OK) target.Text = dlg.FileName;
                }
            };
            return b;
        }
    }

    static class GoToValidation
    {
        public static Func<string, string> Lines(int max)
        {
            return text =>
            {
                int n;
                if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out n) || n < 1 || n > max)
                    return "The line number is beyond the total number of lines";
                return null;
            };
        }
    }
}
