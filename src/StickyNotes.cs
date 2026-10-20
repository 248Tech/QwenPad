using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace QwenPad
{
    /// <summary>A small borderless, colored note window that floats on the desktop.</summary>
    sealed class StickyNote : Form
    {
        internal static readonly string[] ColorNames = { "Yellow", "Green", "Pink", "Purple", "Blue", "Gray" };
        static readonly int[] BodyRgb = { 0xFFF7D1, 0xE4F9E0, 0xFFE4F1, 0xF2E6FF, 0xE2F1FF, 0xF3F2F1 };
        static readonly int[] HeadRgb = { 0xFFEB99, 0xC5EFBC, 0xFFC7E2, 0xDCC6F7, 0xC4E1FA, 0xDDDCDA };
        // Dark mode: deep tints of the same hues.
        static readonly int[] DarkBodyRgb = { 0x38331F, 0x22342A, 0x3A2531, 0x2E2840, 0x21303F, 0x2B2B2B };
        static readonly int[] DarkHeadRgb = { 0x5A4F2A, 0x33513D, 0x5C3A4C, 0x463B63, 0x30486A, 0x404040 };
        static readonly Image[] swatches = new Image[ColorNames.Length];
        const int Grip = 6;

        readonly NoteManager mgr;
        internal readonly string Id;
        internal readonly TextBox Body = new TextBox();
        readonly Panel header = new Panel();
        readonly Label lblStatus = new Label();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem miTop;
        readonly List<Label> headerButtons = new List<Label>();
        internal int ColorIndex { get; private set; }
        internal bool Busy { get; private set; }
        /// <summary>Whether the user wants this note shown. Saved instead of Form.Visible, which can drift from the real window.</summary>
        internal bool WantsVisible = true;
        internal bool AllowClose;

        internal StickyNote(NoteManager mgr, string id)
        {
            this.mgr = mgr;
            Id = id;
            FormBorderStyle = FormBorderStyle.None;
            // Each note is its own top-level window with a taskbar button; being in the same process,
            // Windows groups them under the QwenPad icon.
            ShowInTaskbar = true;
            KeyPreview = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Segoe UI", 10.5f);
            Size = new Size(Font.Height * 15, Font.Height * 13);
            MinimumSize = new Size(Font.Height * 8, Font.Height * 6);
            Padding = new Padding(Grip, 0, Grip, Grip);
            Text = "Sticky Note";

            header.Dock = DockStyle.Top;
            header.Height = Font.Height + 12;
            header.Controls.Add(lblStatus);
            // Later-added docked controls sit further out, so ✕ ends up at the far right.
            header.Controls.Add(HeaderButton("⋯", "Menu", DockStyle.Right, (s, e) => menu.Show((Control)s, 0, ((Control)s).Height)));
            header.Controls.Add(HeaderButton("✕", "Hide note (Ctrl+W)", DockStyle.Right, delegate { mgr.HideNote(this); }));
            header.Controls.Add(HeaderButton("+", "New note (Ctrl+N)", DockStyle.Left, delegate { mgr.New("", Bounds); }));
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.ForeColor = Color.FromArgb(90, 90, 90);
            lblStatus.Font = new Font("Segoe UI", 9f);
            header.MouseDown += DragWindow;
            lblStatus.MouseDown += DragWindow;

            Body.Multiline = true;
            Body.BorderStyle = BorderStyle.None;
            Body.Dock = DockStyle.Fill;
            Body.WordWrap = true;
            Body.ScrollBars = ScrollBars.Vertical;
            Body.AcceptsTab = true;
            Body.MaxLength = 0;
            Body.TextChanged += delegate { mgr.MarkDirty(this); UpdateCaption(); };
            Body.KeyDown += Body_KeyDown;

            Controls.Add(Body);
            Controls.Add(header);

            for (int i = 0; i < ColorNames.Length; i++)
            {
                int c = i;
                menu.Items.Add(new ToolStripMenuItem(ColorNames[i], Swatch(i), delegate { SetColor(c); mgr.MarkDirty(this); }));
            }
            miTop = new ToolStripMenuItem("Always on &Top", null, delegate { TopMost = !TopMost; mgr.MarkDirty(this); });
            menu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripSeparator(),
                new ToolStripMenuItem("✦ &Ask Qwen...", null, delegate { mgr.Ask(this); }) { ShortcutKeyDisplayString = "Ctrl+K" },
                new ToolStripSeparator(), miTop,
                new ToolStripMenuItem("&Insert into Document", null, delegate { mgr.InsertIntoDocument(Body.Text); }),
                new ToolStripSeparator(),
                new ToolStripMenuItem("&Delete Note", null, delegate { mgr.Delete(this); }) { ShortcutKeyDisplayString = "Ctrl+D" }
            });
            menu.Opening += delegate
            {
                miTop.Checked = TopMost;
                for (int i = 0; i < ColorNames.Length; i++) ((ToolStripMenuItem)menu.Items[i]).Checked = i == ColorIndex;
            };

            Move += delegate { mgr.MarkDirty(this); };
            Resize += delegate { mgr.MarkDirty(this); Invalidate(); };
            Body.HandleCreated += delegate { Theme.ApplyScrollbars(Body); };
            // Only the user hides notes (✕, Ctrl+W, Hide All). If anything else hides one, show it again.
            VisibleChanged += delegate
            {
                if (!Visible && WantsVisible && !AllowClose && IsHandleCreated && !IsDisposed)
                    BeginInvoke((Action)(() => { if (WantsVisible && !AllowClose && !IsDisposed && !Visible) Show(); }));
            };
            SetColor(0);
        }

        /// <summary>Taskbar / Alt+Tab caption: the note's first line.</summary>
        void UpdateCaption()
        {
            string t = Title;
            string caption = t == "(empty note)" ? "Sticky Note" : t + " - Sticky Note";
            if (Text != caption) Text = caption;
        }

        /// <summary>Re-applies colors after a light/dark switch.</summary>
        internal void ApplyTheme()
        {
            SetColor(ColorIndex);
            Theme.ApplyScrollbars(Body);
        }

        /// <summary>The first non-empty line, used in the Notes menu.</summary>
        internal string Title
        {
            get
            {
                foreach (string line in Body.Text.Split('\n'))
                {
                    string t = line.Trim().TrimStart('•', '-', '*', '#', ' ');
                    if (t.Length > 0) return t.Length > 40 ? t.Substring(0, 40) + "…" : t;
                }
                return "(empty note)";
            }
        }

        internal void SetColor(int index)
        {
            ColorIndex = Math.Max(0, Math.Min(ColorNames.Length - 1, index));
            bool dark = Theme.Dark;
            BackColor = Body.BackColor = FromRgb((dark ? DarkBodyRgb : BodyRgb)[ColorIndex]);
            header.BackColor = FromRgb((dark ? DarkHeadRgb : HeadRgb)[ColorIndex]);
            Body.ForeColor = dark ? Color.FromArgb(232, 232, 232) : Color.Black;
            lblStatus.ForeColor = dark ? Color.FromArgb(175, 175, 175) : Color.FromArgb(90, 90, 90);
            foreach (var b in headerButtons) b.ForeColor = dark ? Color.FromArgb(215, 215, 215) : Color.FromArgb(60, 60, 60);
            Invalidate();
        }

        /// <summary>Shows a status in the header and locks the text while Qwen writes into the note.</summary>
        internal void SetBusy(bool busy)
        {
            Busy = busy;
            lblStatus.Text = busy ? "✦ Qwen is writing… (Esc to stop)" : "";
            Body.ReadOnly = busy;
        }

        internal void SetStatus(string text) { lblStatus.Text = text; }

        /// <summary>Programmatic, non-undoable replacement that works while the note is read-only.</summary>
        internal void ReplaceRange(int start, int length, string text)
        {
            bool ro = Body.ReadOnly;
            Body.ReadOnly = false;
            Body.Select(start, length);
            Body.SelectedText = text;
            Body.ReadOnly = ro;
            Body.Select(start + text.Length, 0);
            Body.ScrollToCaret();
        }

        /// <summary>Set by whoever is streaming into this note; Esc calls it.</summary>
        internal Action CancelStreaming;

        internal void SetStreamText(string text)
        {
            Body.Text = text;
            Body.Select(Body.TextLength, 0);
            Body.ScrollToCaret();
        }

        internal string Meta()
        {
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            return string.Format(CultureInfo.InvariantCulture, "#note x={0} y={1} w={2} h={3} color={4} top={5} visible={6}",
                b.X, b.Y, b.Width, b.Height, ColorIndex, TopMost ? 1 : 0, WantsVisible ? 1 : 0);
        }

        internal void ApplyMeta(string line)
        {
            var v = new Dictionary<string, int>();
            foreach (string part in line.Split(' '))
            {
                int eq = part.IndexOf('='), n;
                if (eq > 0 && int.TryParse(part.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    v[part.Substring(0, eq)] = n;
            }
            int x, y, w, h, c, top, vis;
            if (v.TryGetValue("w", out w) && v.TryGetValue("h", out h)) Size = new Size(w, h);
            if (v.TryGetValue("x", out x) && v.TryGetValue("y", out y)) Location = new Point(x, y);
            if (v.TryGetValue("color", out c)) SetColor(c);
            if (v.TryGetValue("top", out top)) TopMost = top == 1;
            if (v.TryGetValue("visible", out vis)) WantsVisible = vis == 1;
            if (!OnScreen(Bounds)) Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - Width - 40, Screen.PrimaryScreen.WorkingArea.Top + 40);
        }

        internal static bool OnScreen(Rectangle r)
        {
            foreach (Screen s in Screen.AllScreens)
                if (s.WorkingArea.IntersectsWith(new Rectangle(r.X, r.Y, Math.Max(40, r.Width), 30))) return true;
            return false;
        }

        internal static Image Swatch(int i)
        {
            if (swatches[i] == null)
            {
                var bmp = new Bitmap(14, 14);
                using (var g = Graphics.FromImage(bmp))
                using (var fill = new SolidBrush(FromRgb(HeadRgb[i])))
                using (var pen = new Pen(Color.FromArgb(120, 0, 0, 0)))
                {
                    g.FillRectangle(fill, 1, 1, 12, 12);
                    g.DrawRectangle(pen, 1, 1, 11, 11);
                }
                swatches[i] = bmp;
            }
            return swatches[i];
        }

        static Color FromRgb(int rgb) { return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }

        Label HeaderButton(string glyph, string tip, DockStyle dock, EventHandler onClick)
        {
            var b = new Label
            {
                Text = glyph, Dock = dock, Width = header.Height, TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Symbol", 10f), ForeColor = Color.FromArgb(60, 60, 60), Cursor = Cursors.Hand
            };
            new ToolTip().SetToolTip(b, tip);
            headerButtons.Add(b);
            b.MouseEnter += delegate { b.BackColor = Theme.Dark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0); };
            b.MouseLeave += delegate { b.BackColor = Color.Transparent; };
            b.Click += onClick;
            return b;
        }

        void Body_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A) { Body.SelectAll(); e.Handled = e.SuppressKeyPress = true; return; }
            // While Qwen is writing, block editing shortcuts (typing is blocked by read-only).
            if (Busy && e.Control && e.KeyCode != Keys.C) e.Handled = e.SuppressKeyPress = true;
        }

        /// <summary>Note shortcuts, handled at window level so they work wherever the focus is inside the note.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    if (Busy && CancelStreaming != null) { CancelStreaming(); return true; }
                    break;
                case Keys.Control | Keys.K:
                    if (Busy) SetStatus("✦ Qwen is still writing… (Esc to stop)");
                    else mgr.Ask(this);
                    return true;
                case Keys.Control | Keys.N:
                    mgr.New("", Bounds);
                    return true;
                case Keys.Control | Keys.D:
                    if (!Busy) mgr.Delete(this);
                    return true;
                case Keys.Control | Keys.W:
                    mgr.HideNote(this);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                // Lets the borderless note minimize/restore from its taskbar button and show the taskbar window menu.
                cp.Style |= WS_MINIMIZEBOX | WS_SYSMENU;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Dark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        /// <summary>The padding around the text box acts as a resize border.</summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_NCHITTEST || (int)m.Result != HTCLIENT) return;
            long lp = m.LParam.ToInt64();
            var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            bool left = p.X < Grip, right = p.X >= ClientSize.Width - Grip, bottom = p.Y >= ClientSize.Height - Grip;
            if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else if (left) m.Result = (IntPtr)HTLEFT;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Alt+F4 on a note hides it; it is only really closed when deleted or when QwenPad exits.
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; mgr.HideNote(this); }
            base.OnFormClosing(e);
        }

        const int WM_NCHITTEST = 0x84, WM_NCLBUTTONDOWN = 0xA1, HTCLIENT = 1, HTCAPTION = 2;
        const int HTLEFT = 10, HTRIGHT = 11, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const int CS_DROPSHADOW = 0x20000, WS_MINIMIZEBOX = 0x20000, WS_SYSMENU = 0x80000;

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }

    /// <summary>
    /// Loads, saves and tracks sticky notes. Each note is a plain .txt file in the notes folder next to the
    /// exe (first line holds position/color). Only one QwenPad process owns the notes at a time.
    /// </summary>
    sealed class NoteManager : IDisposable
    {
        readonly Action<string> insertIntoDocument;
        readonly Action<StickyNote> askQwen;
        readonly List<StickyNote> notes = new List<StickyNote>();
        readonly HashSet<StickyNote> dirty = new HashSet<StickyNote>();
        readonly System.Windows.Forms.Timer saveTimer = new System.Windows.Forms.Timer { Interval = 800 };
        Mutex mutex;
        bool owner, loading;

        public static string Dir { get { return Path.Combine(Settings.AppDir, "notes"); } }

        public NoteManager(Action<string> insertIntoDocument, Action<StickyNote> askQwen)
        {
            this.insertIntoDocument = insertIntoDocument;
            this.askQwen = askQwen;
            saveTimer.Tick += delegate { Flush(); };
        }

        public IList<StickyNote> Notes { get { return notes.AsReadOnly(); } }

        /// <summary>True when this process owns the notes; claims them (and loads them) if nobody else does.</summary>
        public bool Available
        {
            get
            {
                if (!owner) TryAcquire();
                return owner;
            }
        }

        void TryAcquire()
        {
            try
            {
                // One owner per notes folder, so separate portable copies don't block each other.
                if (mutex == null) mutex = new Mutex(false, @"Local\QwenPad.StickyNotes." + FolderTag());
                try { owner = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { owner = true; }
            }
            catch { owner = false; }
            if (owner) Load();
        }

        void Load()
        {
            if (!Directory.Exists(Dir)) return;
            loading = true;
            try
            {
                foreach (string path in Directory.GetFiles(Dir, "*.txt"))
                {
                    try
                    {
                        string all = File.ReadAllText(path, Encoding.UTF8);
                        int nl = all.IndexOf('\n');
                        string head = (nl >= 0 ? all.Substring(0, nl) : all).TrimEnd('\r');
                        if (!head.StartsWith("#note")) continue;
                        var note = new StickyNote(this, Path.GetFileNameWithoutExtension(path));
                        note.ApplyMeta(head);
                        note.Body.Text = nl >= 0 ? Crlf(all.Substring(nl + 1)) : "";
                        notes.Add(note);
                        if (note.WantsVisible) note.Show();
                    }
                    catch { }
                }
            }
            finally { loading = false; }
        }

        /// <summary>Creates a note next to the given window bounds.</summary>
        public StickyNote New(string text, Rectangle near)
        {
            if (!Available) return null;
            string id = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            var note = new StickyNote(this, id);
            var area = Screen.FromRectangle(near).WorkingArea;
            int x = near.Right + 12, y = near.Top;
            if (near.Width > note.Width * 2)
            {
                // Next to the main window: cascade inside its top-right corner.
                int step = (CountVisible() % 8) * 26;
                x = near.Right - note.Width - 30 - step;
                y = near.Top + 80 + step;
            }
            else if (x + note.Width > area.Right) x = near.Left - note.Width - 12;
            note.Location = new Point(Math.Max(area.Left, Math.Min(x, area.Right - note.Width)), Math.Max(area.Top, Math.Min(y, area.Bottom - note.Height)));
            note.Body.Text = Crlf(text ?? "");
            notes.Add(note);
            note.Show();
            note.Activate();
            note.Body.Select(note.Body.TextLength, 0);
            MarkDirty(note);
            return note;
        }

        public void ShowNote(StickyNote note)
        {
            note.WantsVisible = true;
            if (note.WindowState == FormWindowState.Minimized) note.WindowState = FormWindowState.Normal;
            note.Show();
            note.Activate();
            MarkDirty(note);
        }

        public void HideNote(StickyNote note)
        {
            note.WantsVisible = false;
            note.Hide();
            MarkDirty(note);
        }

        public void ShowAll() { foreach (var n in notes) { n.WantsVisible = true; n.Show(); MarkDirty(n); } }
        public void HideAll() { foreach (var n in notes) HideNote(n); }
        public int CountVisible() { int c = 0; foreach (var n in notes) if (n.WantsVisible) c++; return c; }

        public void Delete(StickyNote note)
        {
            if (note.Body.Text.Trim().Length > 0 &&
                MessageBox.Show(note, "Delete this note?", "Sticky Notes", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            dirty.Remove(note);
            notes.Remove(note);
            try { File.Delete(PathOf(note)); } catch { }
            note.AllowClose = true;
            note.Close();
        }

        public void InsertIntoDocument(string text) { insertIntoDocument(text); }

        public void Ask(StickyNote note)
        {
            if (note.Busy) note.SetStatus("✦ Qwen is still writing… (Esc to stop)");
            else askQwen(note);
        }

        public void ApplyTheme() { foreach (var n in notes) n.ApplyTheme(); }

        public void MarkDirty(StickyNote note)
        {
            if (loading || !notes.Contains(note)) return;
            dirty.Add(note);
            saveTimer.Stop();
            saveTimer.Start();
        }

        void Flush()
        {
            saveTimer.Stop();
            if (dirty.Count == 0) return;
            try { Directory.CreateDirectory(Dir); } catch { return; }
            foreach (var note in dirty)
            {
                try { File.WriteAllText(PathOf(note), note.Meta() + "\r\n" + note.Body.Text, new UTF8Encoding(false)); }
                catch { }
            }
            dirty.Clear();
        }

        static string FolderTag()
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(Dir).ToLowerInvariant()));
                return BitConverter.ToString(h, 0, 8).Replace("-", "");
            }
        }

        static string PathOf(StickyNote note) { return Path.Combine(Dir, note.Id + ".txt"); }

        static string Crlf(string s) { return s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n"); }

        public void Dispose()
        {
            Flush();
            foreach (var n in notes.ToArray()) { n.AllowClose = true; n.Close(); }
            notes.Clear();
            saveTimer.Dispose();
            if (mutex != null)
            {
                if (owner) try { mutex.ReleaseMutex(); } catch { }
                mutex.Dispose();
            }
        }
    }
}
