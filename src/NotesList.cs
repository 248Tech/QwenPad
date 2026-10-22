using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace QwenPad
{
    /// <summary>
    /// "Notes list" palette: every sticky note as a colored card (title, date, preview), with search.
    /// Click selects, double-click / Enter opens, Delete deletes, right-click for more.
    /// </summary>
    sealed class NotesListForm : Form
    {
        readonly NoteManager mgr;
        readonly TextBox search = new TextBox();
        readonly FlowLayoutPanel grid = new FlowLayoutPanel();
        readonly Label lblEmpty = new Label();
        readonly Button btnNew = new Button();
        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer { Interval = 300 };
        NoteCard selected;

        public NotesListForm(NoteManager mgr)
        {
            this.mgr = mgr;
            Text = "Sticky Notes";
            Font = SystemFonts.MessageBoxFont;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.Manual;
            Size = new Size(Font.Height * 34, Font.Height * 32);
            MinimumSize = new Size(Font.Height * 20, Font.Height * 16);
            KeyPreview = true;

            var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(10, 10, 10, 4) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            search.Dock = DockStyle.Fill;
            search.Margin = new Padding(0, 2, 8, 0);
            search.TextChanged += delegate { RefreshCards(); };
            search.HandleCreated += delegate { SendMessage(search.Handle, 0x1501, (IntPtr)1, "Search notes"); };
            btnNew.Text = "+ New Note";
            btnNew.AutoSize = true;
            btnNew.Click += delegate { var n = mgr.New("", Bounds); if (n != null) n.Activate(); };
            top.Controls.Add(search, 0, 0);
            top.Controls.Add(btnNew, 1, 0);

            grid.Dock = DockStyle.Fill;
            grid.AutoScroll = true;
            grid.Padding = new Padding(8, 4, 8, 8);
            grid.Resize += delegate { SizeCards(); };
            grid.Click += delegate { Select(null); };
            lblEmpty.Dock = DockStyle.Fill;
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.Text = "No notes yet.\nClick \"+ New Note\" to create one.";
            lblEmpty.Visible = false;

            Controls.Add(grid);
            Controls.Add(lblEmpty);
            Controls.Add(top);

            refreshTimer.Tick += delegate { refreshTimer.Stop(); RefreshCards(); };
            EventHandler onChanged = delegate { if (Visible) { refreshTimer.Stop(); refreshTimer.Start(); } };
            mgr.Changed += onChanged;
            FormClosed += delegate { mgr.Changed -= onChanged; };
            Load += delegate { ApplyTheme(); };
        }

        public void ShowNear(Form near)
        {
            if (!Visible)
            {
                var area = Screen.FromControl(near ?? this).WorkingArea;
                int x = near != null ? near.Right + 12 : area.Left + 80, y = near != null ? near.Top : area.Top + 80;
                if (x + Width > area.Right) x = (near != null ? near.Left : area.Right) - Width - 12;
                Location = new Point(Math.Max(area.Left, x), Math.Max(area.Top, Math.Min(y, area.Bottom - Height)));
                RefreshCards();
                Show();
            }
            else RefreshCards();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            search.Focus();
        }

        public void ApplyTheme()
        {
            Theme.ApplyForm(this);
            grid.BackColor = lblEmpty.BackColor = Theme.Dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
            lblEmpty.ForeColor = Theme.Dark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(110, 110, 110);
            foreach (Control c in grid.Controls) c.Invalidate();
        }

        void RefreshCards()
        {
            string q = search.Text.Trim();
            StickyNote keep = selected != null ? selected.Note : null;
            grid.SuspendLayout();
            foreach (Control c in grid.Controls.Cast<Control>().ToList()) { grid.Controls.Remove(c); c.Dispose(); }
            selected = null;
            var list = mgr.Notes.Where(n => q.Length == 0 ||
                    n.TitleText.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    n.Body.Text.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .OrderByDescending(n => n.ModifiedOn).ToList();
            foreach (var n in list)
            {
                var card = new NoteCard(n, this);
                grid.Controls.Add(card);
                if (n == keep) Select(card);
            }
            SizeCards();
            grid.ResumeLayout();
            lblEmpty.Text = mgr.Notes.Count == 0 ? "No notes yet.\nClick \"+ New Note\" to create one." : "No notes match \"" + q + "\".";
            lblEmpty.Visible = list.Count == 0;
            grid.Visible = list.Count > 0;
        }

        /// <summary>Two cards per row (one when narrow), each the same height.</summary>
        void SizeCards()
        {
            int avail = grid.ClientSize.Width - grid.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth;
            int cols = avail > Font.Height * 26 ? 2 : 1;
            int w = Math.Max(120, avail / cols - 12);
            foreach (Control c in grid.Controls) c.Size = new Size(w, Font.Height * 7);
        }

        internal void Select(NoteCard card)
        {
            if (selected != null && !selected.IsDisposed) { selected.Selected = false; selected.Invalidate(); }
            selected = card;
            if (card != null) { card.Selected = true; card.Invalidate(); grid.ScrollControlIntoView(card); }
        }

        internal void Open(NoteCard card)
        {
            if (card == null) return;
            mgr.ShowNote(card.Note);
            card.Invalidate();
        }

        internal ContextMenuStrip MenuFor(NoteCard card)
        {
            var n = card.Note;
            var cm = new ContextMenuStrip();
            cm.Items.Add(new ToolStripMenuItem("&Open", null, delegate { Open(card); }) { Font = new Font(cm.Font, FontStyle.Bold) });
            cm.Items.Add(new ToolStripMenuItem(n.WantsVisible ? "&Hide" : "&Show", null, delegate
            {
                if (n.WantsVisible) mgr.HideNote(n); else mgr.ShowNote(n);
                card.Invalidate();
            }));
            cm.Items.Add(new ToolStripMenuItem(n.TopMost ? "Un&pin" : "📌 &Pin on Top", null, delegate { n.SetPinned(!n.TopMost); card.Invalidate(); }));
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add(new ToolStripMenuItem("&Delete", null, delegate { mgr.Delete(n); }));
            return cm;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var cards = grid.Controls.Cast<NoteCard>().ToList();
            int i = selected != null ? cards.IndexOf(selected) : -1;
            int cols = cards.Count > 1 && cards[1].Top == cards[0].Top ? 2 : 1;
            switch (keyData)
            {
                case Keys.Escape: Close(); return true;
                case Keys.Control | Keys.F: search.Focus(); search.SelectAll(); return true;
                case Keys.Control | Keys.N: btnNew.PerformClick(); return true;
                case Keys.Enter: if (selected != null) { Open(selected); return true; } if (cards.Count > 0) { Select(cards[0]); return true; } break;
                case Keys.Delete: if (selected != null && !search.Focused) { mgr.Delete(selected.Note); return true; } break;
                case Keys.Down: case Keys.Right: case Keys.Up: case Keys.Left:
                    if (search.Focused && (keyData == Keys.Left || keyData == Keys.Right)) break;
                    if (cards.Count == 0) break;
                    int step = keyData == Keys.Down ? cols : keyData == Keys.Up ? -cols : keyData == Keys.Right ? 1 : -1;
                    int next = i < 0 ? 0 : Math.Max(0, Math.Min(cards.Count - 1, i + step));
                    Select(cards[next]);
                    cards[next].Focus();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Closing just hides the palette so it reopens instantly where it was.
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
    }

    /// <summary>One note in the palette, painted in the note's own colors.</summary>
    sealed class NoteCard : Control
    {
        internal readonly StickyNote Note;
        readonly NotesListForm owner;
        internal bool Selected;
        bool hover;
        Bitmap designThumb;

        protected override void Dispose(bool disposing)
        {
            if (disposing && designThumb != null) designThumb.Dispose();
            base.Dispose(disposing);
        }

        public NoteCard(StickyNote note, NotesListForm owner)
        {
            Note = note;
            this.owner = owner;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Margin = new Padding(6);
            Cursor = Cursors.Hand;
            TabStop = true;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
            MouseDown += (s, e) =>
            {
                owner.Select(this);
                Focus();
                if (e.Button == MouseButtons.Right) owner.MenuFor(this).Show(this, e.Location);
            };
            DoubleClick += delegate { owner.Open(this); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            bool dark = Theme.Dark;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            var style = NoteDesigns.StyleOf(Note.Design);
            using (var path = Rounded(r, 6))
            {
                var old = g.Clip;
                g.SetClip(path);
                if (style != null)
                {
                    // Show the note's fall design (rendered once per card size).
                    if (designThumb == null || designThumb.Size != Size)
                    {
                        if (designThumb != null) designThumb.Dispose();
                        designThumb = NoteDesigns.Thumbnail(Note.Design, Width, Height);
                    }
                    g.DrawImage(designThumb, 0, 0);
                    // A light wash keeps the design recognizable while the card's text stays readable.
                    using (var veil = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillRectangle(veil, 0, 0, Width, Height);
                }
                else
                {
                    using (var fill = new SolidBrush(StickyNote.BodyColor(Note.ColorIndex, dark))) g.FillPath(fill, path);
                    // Header strip in the note's accent color.
                    using (var head = new SolidBrush(StickyNote.HeadColor(Note.ColorIndex, dark))) g.FillRectangle(head, 0, 0, Width, 6);
                }
                g.Clip = old;
                Color border = Selected ? (dark ? Color.FromArgb(155, 123, 255) : Color.FromArgb(124, 77, 255))
                             : hover ? Color.FromArgb(dark ? 110 : 90, dark ? 255 : 0, dark ? 255 : 0, dark ? 255 : 0)
                             : Color.FromArgb(dark ? 50 : 40, dark ? 255 : 0, dark ? 255 : 0, dark ? 255 : 0);
                using (var pen = new Pen(border, Selected ? 2.5f : 1f)) g.DrawPath(pen, path);
            }

            Color text = style != null ? style.Text : StickyNote.TextColor(dark);
            Color muted = style != null ? style.Muted : dark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(105, 105, 105);
            int pad = 10, y = 12;
            string date = Note.CreatedOn.Date == DateTime.Today ? Note.CreatedOn.ToShortTimeString()
                        : Note.CreatedOn.Year == DateTime.Today.Year ? Note.CreatedOn.ToString("MMM d") : Note.CreatedOn.ToString("MMM d, yyyy");
            string flags = (Note.TopMost ? "📌 " : "") + (Note.HasSketch ? "✏ " : "") + (Note.WantsVisible ? "" : "hidden · ") + date;
            using (var small = new Font(StickyNote.HandFont, 9.5f))
            {
                var sz = TextRenderer.MeasureText(g, flags, small);
                TextRenderer.DrawText(g, flags, small, new Point(Width - pad - sz.Width, y + 2), muted, TextFormatFlags.NoPadding);
                string title = Note.TitleText.Length > 0 ? Note.TitleText : "(untitled)";
                using (var tf = new Font(StickyNote.HandFont, 13f, StickyNote.HandFont == "Ink Free" ? FontStyle.Regular : FontStyle.Bold))
                {
                    var titleRect = new Rectangle(pad, y, Width - pad * 2 - sz.Width - 6, tf.Height + 2);
                    TextRenderer.DrawText(g, title, tf, titleRect, Note.TitleText.Length > 0 ? text : muted,
                        TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    y += tf.Height + 4;
                }
                // RichTextBox uses bare \n; TextRenderer only breaks lines on \r\n.
                string preview = Note.Body.Text.Replace("\r", "").Trim().Replace("\n", "\r\n");
                if (preview.Length > 400) preview = preview.Substring(0, 400);
                using (var bf = new Font(StickyNote.HandFont, 10.5f))
                    TextRenderer.DrawText(g, preview.Length > 0 ? preview : "Empty note", bf, new Rectangle(pad, y, Width - pad * 2, Height - y - 8),
                        preview.Length > 0 ? text : muted, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            }
            if (!Note.WantsVisible)
                using (var veil = new SolidBrush(Color.FromArgb(70, dark ? 0 : 255, dark ? 0 : 255, dark ? 0 : 255))) g.FillRectangle(veil, 1, 7, Width - 2, Height - 8);
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
