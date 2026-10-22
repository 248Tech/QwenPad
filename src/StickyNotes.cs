using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        static readonly int[] OpacityLevels = { 100, 90, 80, 70, 60, 50, 40, 30 };
        internal static readonly string ImageFilter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.ico)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.ico";
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".ico" };

        // Visual padding around the text (the bottom strip holds the grip). The invisible resize zone is wider
        // and overlaps the child controls, which pass edge hits through to the note (see PassNoteMessage).
        const int Grip = 7, GripBottom = 14;
        const int EM_SETCUEBANNER = 0x1501;

        int baseHeight;        // height the user chose; auto-expansion never shrinks below it
        Size sizeAtResizeBegin;
        int contentHeight;     // height of the rich text content, from RichTextBox.ContentsResized

        readonly NoteManager mgr;
        internal readonly string Id;
        internal readonly RichTextBox Body = new EdgeRichTextBox();
        readonly Panel header = new EdgePanel();
        readonly Label lblStatus = new EdgeLabel();
        readonly Panel titleRow = new EdgePanel();
        readonly TextBox txtTitle = new EdgeTextBox();
        readonly Label lblDate = new EdgeLabel();
        readonly ToolTip dateTip = new ToolTip();
        readonly Panel formatBar = new EdgePanel();
        readonly FlowLayoutPanel gallery = new EdgeFlowPanel();
        readonly List<string> images = new List<string>();
        readonly Dictionary<PictureBox, Stream> imageStreams = new Dictionary<PictureBox, Stream>();
        readonly Dictionary<int, Label> formatButtons = new Dictionary<int, Label>();
        readonly System.Windows.Forms.Timer titleTimer = new System.Windows.Forms.Timer { Interval = 2500 };
        readonly System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        internal DateTime CreatedOn { get; set; }
        internal DateTime ModifiedOn { get; set; }
        internal bool AutoTitleTried;
        internal int OpacityPercent = 100;
        bool loadingContent;   // text being loaded from disk doesn't count as an edit

        // Handwritten look: Ink Free (Windows 10 1803+ / 11), else Segoe Print (Windows 7+), else Segoe UI.
        internal static readonly string HandFont = PickFont("Ink Free", "Segoe Print");
        static float BodySize { get { return HandFont == "Ink Free" ? 12.5f : 10.5f; } }
        static float TitleSize { get { return HandFont == "Ink Free" ? 17f : 13.5f; } }
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem miTop, miOpacity;
        readonly List<Label> headerButtons = new List<Label>();
        readonly ToolTip headerTip = new ToolTip();
        readonly Label btnPin;
        internal int ColorIndex { get; private set; }
        internal bool Busy { get; private set; }
        /// <summary>Whether the user wants this note shown. Saved instead of Form.Visible, which can drift from the real window.</summary>
        internal bool WantsVisible = true;
        internal bool AllowClose;

        internal StickyNote(NoteManager mgr, string id)
        {
            this.mgr = mgr;
            Id = id;
            CreatedOn = ModifiedOn = DateTime.Now;
            FormBorderStyle = FormBorderStyle.None;
            // Each note is its own top-level window with a taskbar button; being in the same process,
            // Windows groups them under the QwenPad icon.
            ShowInTaskbar = true;
            KeyPreview = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Segoe UI", 10.5f);
            Size = new Size(Font.Height * 15, Font.Height * 14);
            MinimumSize = new Size(Font.Height * 9, Font.Height * 7);
            Padding = new Padding(Grip, 0, Grip, GripBottom);
            Text = "Sticky Note";
            AllowDrop = true;

            header.Dock = DockStyle.Top;
            header.Height = Font.Height + 12;
            header.Controls.Add(lblStatus);
            // Later-added docked controls sit further out, so ✕ ends up at the far right.
            btnPin = HeaderButton("📌", "", DockStyle.Right, delegate { SetPinned(!TopMost); });
            header.Controls.Add(btnPin);
            header.Controls.Add(HeaderButton("⋯", "Menu", DockStyle.Right, (s, e) => menu.Show((Control)s, 0, ((Control)s).Height)));
            header.Controls.Add(HeaderButton("✕", "Hide note (Ctrl+W)", DockStyle.Right, delegate { mgr.HideNote(this); }));
            header.Controls.Add(HeaderButton("+", "New note (Ctrl+N)", DockStyle.Left, delegate { mgr.New("", Bounds); }));
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Font = new Font("Segoe UI", 9f);
            foreach (Control c in new Control[] { header, lblStatus })
            {
                c.MouseDown += DragStart;
                c.MouseMove += DragMove;
                c.MouseUp += DragEnd;
            }

            // Title row: an editable handwritten title with the note's date beside it.
            txtTitle.BorderStyle = BorderStyle.None;
            txtTitle.Font = new Font(HandFont, TitleSize, HandFont == "Ink Free" ? FontStyle.Regular : FontStyle.Bold);
            txtTitle.Dock = DockStyle.Fill;
            txtTitle.MaxLength = 120;
            txtTitle.TabIndex = 1;
            txtTitle.TextChanged += delegate { Edited(false); UpdateCaption(); };
            txtTitle.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Down) { e.Handled = e.SuppressKeyPress = true; Body.Focus(); Body.Select(0, 0); }
            };
            txtTitle.HandleCreated += delegate { SendMessage(txtTitle.Handle, EM_SETCUEBANNER, (IntPtr)1, "Title"); };
            lblDate.Dock = DockStyle.Right;
            lblDate.AutoSize = true;
            lblDate.Font = new Font(HandFont, BodySize - 2f);
            lblDate.Padding = new Padding(4, 0, 2, 0);
            lblDate.TextAlign = ContentAlignment.MiddleRight;
            titleRow.Dock = DockStyle.Top;
            titleRow.Height = txtTitle.PreferredHeight + 2;
            titleRow.Padding = new Padding(1, 2, 0, 0);
            titleRow.Controls.Add(txtTitle);
            titleRow.Controls.Add(lblDate);

            // Rich text body: bold / italic / underline / strikethrough / bullets.
            Body.BorderStyle = BorderStyle.None;
            Body.Font = new Font(HandFont, BodySize);
            Body.TabIndex = 0;   // clicking or activating a note puts the cursor in its text
            Body.Dock = DockStyle.Fill;
            Body.WordWrap = true;
            Body.ScrollBars = RichTextBoxScrollBars.Vertical;   // a RichTextBox shows it only when needed
            Body.AcceptsTab = true;
            Body.DetectUrls = true;
            Body.BulletIndent = 14;
            Body.AllowDrop = true;
            Body.TextChanged += delegate { Edited(true); UpdateCaption(); AutoFit(); };
            Body.ContentsResized += (s, e) => { contentHeight = e.NewRectangle.Height; AutoFit(); };
            Body.SelectionChanged += delegate { UpdateFormatButtons(); };
            Body.LinkClicked += (s, e) => { try { Process.Start(e.LinkText); } catch { } };
            Body.KeyDown += Body_KeyDown;
            Body.HandleCreated += delegate { Theme.ApplyScrollbars(Body); };
            foreach (Control c in new Control[] { this, Body })
            {
                c.DragEnter += (s, e) => { if (DroppedImages(e.Data).Count > 0) e.Effect = DragDropEffects.Copy; };
                c.DragDrop += (s, e) => { foreach (string f in DroppedImages(e.Data)) AddImageFile(f); };
            }

            // Format bar along the bottom.
            formatBar.Dock = DockStyle.Bottom;
            formatBar.Height = Font.Height + 8;
            // Docked Left in reverse, so they read B I U S • 🖼 from the left.
            formatBar.Controls.Add(FormatButton(-2, "🖼", "Add an image or GIF", new Font("Segoe UI Symbol", 10f), delegate { ChooseImage(); }));
            formatBar.Controls.Add(FormatButton(-1, "•", "Bullets (Ctrl+Shift+L)", new Font("Segoe UI", 12f, FontStyle.Bold), delegate { ToggleBullets(); }));
            formatBar.Controls.Add(FormatButton(CFM_STRIKEOUT, "S", "Strikethrough (Ctrl+T)", new Font("Segoe UI", 10f, FontStyle.Strikeout), delegate { ToggleEffect(CFM_STRIKEOUT); }));
            formatBar.Controls.Add(FormatButton(CFM_UNDERLINE, "U", "Underline (Ctrl+U)", new Font("Segoe UI", 10f, FontStyle.Underline), delegate { ToggleEffect(CFM_UNDERLINE); }));
            formatBar.Controls.Add(FormatButton(CFM_ITALIC, "I", "Italic (Ctrl+I)", new Font("Georgia", 10.5f, FontStyle.Italic), delegate { ToggleEffect(CFM_ITALIC); }));
            formatBar.Controls.Add(FormatButton(CFM_BOLD, "B", "Bold (Ctrl+B)", new Font("Segoe UI", 10f, FontStyle.Bold), delegate { ToggleEffect(CFM_BOLD); }));

            // Image strip (animated GIFs play) between the text and the format bar.
            gallery.Dock = DockStyle.Bottom;
            gallery.FlowDirection = FlowDirection.TopDown;
            gallery.WrapContents = false;
            gallery.AutoScroll = true;
            gallery.Visible = false;
            gallery.Padding = new Padding(0, 2, 0, 0);

            // Docking order: later-added controls dock further out.
            Controls.Add(Body);
            Controls.Add(gallery);
            Controls.Add(formatBar);
            Controls.Add(titleRow);
            Controls.Add(header);

            for (int i = 0; i < ColorNames.Length; i++)
            {
                int c = i;
                menu.Items.Add(new ToolStripMenuItem(ColorNames[i], Swatch(i), delegate { SetColor(c); mgr.MarkDirty(this); }));
            }
            miTop = new ToolStripMenuItem("📌 &Pin on Top of All Windows", null, delegate { SetPinned(!TopMost); }) { ShortcutKeyDisplayString = "Ctrl+P" };
            miOpacity = new ToolStripMenuItem("&Opacity");
            foreach (int pct in OpacityLevels)
            {
                int p = pct;
                miOpacity.DropDownItems.Add(new ToolStripMenuItem(pct + "%", null, delegate { SetOpacity(p); mgr.MarkDirty(this); }) { Tag = pct });
            }
            var miLayer = new ToolStripMenuItem("&Layer");
            miLayer.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Bring to &Front", null, delegate { Layer(LayerOp.Front); }),
                new ToolStripMenuItem("Move &Forward", null, delegate { Layer(LayerOp.Forward); }) { ShortcutKeyDisplayString = "Ctrl+Scroll up" },
                new ToolStripMenuItem("Move &Back", null, delegate { Layer(LayerOp.Backward); }) { ShortcutKeyDisplayString = "Ctrl+Scroll down" },
                new ToolStripMenuItem("Send to Bac&k", null, delegate { Layer(LayerOp.Back); })
            });
            menu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripSeparator(),
                new ToolStripMenuItem("✦ &Ask Qwen...", null, delegate { mgr.Ask(this); }) { ShortcutKeyDisplayString = "Ctrl+K" },
                new ToolStripMenuItem("Add &Image or GIF...", null, delegate { ChooseImage(); }),
                new ToolStripSeparator(), miTop, miOpacity, miLayer,
                new ToolStripSeparator(),
                new ToolStripMenuItem("☰ Notes &List...", null, delegate { mgr.ShowList(this); }),
                new ToolStripMenuItem("&Insert into Document", null, delegate
                {
                    mgr.InsertIntoDocument(TitleText.Length > 0 ? TitleText + "\r\n" + Body.Text : Body.Text);
                }),
                new ToolStripSeparator(),
                new ToolStripMenuItem("&Delete Note", null, delegate { mgr.Delete(this); }) { ShortcutKeyDisplayString = "Ctrl+D" }
            });
            menu.Opening += delegate
            {
                miTop.Checked = TopMost;
                for (int i = 0; i < ColorNames.Length; i++) ((ToolStripMenuItem)menu.Items[i]).Checked = i == ColorIndex;
                foreach (ToolStripMenuItem o in miOpacity.DropDownItems) o.Checked = (int)o.Tag == OpacityPercent;
            };

            Move += delegate { mgr.MarkDirty(this); };
            Resize += delegate { mgr.MarkDirty(this); LayoutGallery(); Invalidate(); };
            Shown += delegate { AutoFit(); ApplyPin(); };
            VisibleChanged += delegate { if (Visible) ApplyPin(); };
            ResizeBegin += delegate { sizeAtResizeBegin = Size; };
            ResizeEnd += delegate
            {
                if (Size == sizeAtResizeBegin) return;   // it was a move, not a resize
                baseHeight = Height;                     // the user's chosen size becomes the new base
                mgr.MarkDirty(this);
            };
            // Only the user hides notes (✕, Ctrl+W, Hide All). If anything else hides one, show it again.
            VisibleChanged += delegate
            {
                if (!Visible && WantsVisible && !AllowClose && IsHandleCreated && !IsDisposed)
                    BeginInvoke((Action)(() => { if (WantsVisible && !AllowClose && !IsDisposed && !Visible) Show(); }));
            };
            titleTimer.Tick += delegate { titleTimer.Stop(); TryAutoTitle(); };
            statusTimer.Tick += delegate { statusTimer.Stop(); if (!Busy) lblStatus.Text = ""; };
            SetColor(0);
        }

        #region Pin, opacity, layers

        /// <summary>Pins the note above all other windows (Windows "topmost"), or unpins it.</summary>
        internal void SetPinned(bool pinned)
        {
            TopMost = pinned;
            UpdatePinButton();   // before ApplyPin: updating the tooltip can reshuffle the note's z-order
            ApplyPin();
            mgr.MarkDirty(this);
            // Re-check a few times over the next second, in case anything moved the note back.
            pinChecks = 3;
            pinTimer.Start();
        }

        int pinChecks;
        System.Windows.Forms.Timer pinTimerField;
        System.Windows.Forms.Timer pinTimer
        {
            get
            {
                if (pinTimerField == null)
                {
                    pinTimerField = new System.Windows.Forms.Timer { Interval = 300 };
                    pinTimerField.Tick += delegate
                    {
                        ApplyPin();
                        if (--pinChecks <= 0) pinTimerField.Stop();
                    };
                }
                return pinTimerField;
            }
        }

        /// <summary>
        /// Applies the topmost flag directly. WinForms' TopMost setter alone doesn't reliably reach these
        /// custom-frame windows, so set the z-order ourselves (without activating the note).
        /// </summary>
        void ApplyPin()
        {
            if (!IsHandleCreated) return;
            // The first topmost request on a note often only moves its owned helper windows (tooltip, IME)
            // into the topmost band and leaves the note itself below them, so check and retry.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                SetWindowPos(Handle, TopMost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                if (((GetWindowLong(Handle, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0) == TopMost) break;
            }
        }

        /// <summary>Shows the pin button as pressed while the note is pinned.</summary>
        void UpdatePinButton()
        {
            if (btnPin == null) return;
            btnPin.BackColor = !TopMost ? Color.Transparent
                : Theme.Dark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(55, 0, 0, 0);
            headerTip.SetToolTip(btnPin, TopMost ? "Pinned on top of all windows. Click to unpin (Ctrl+P)"
                                                 : "Pin on top of all windows (Ctrl+P)");
        }

        internal void SetOpacity(int percent)
        {
            OpacityPercent = Math.Max(20, Math.Min(100, percent));
            Opacity = OpacityPercent / 100.0;
        }

        internal enum LayerOp { Front, Forward, Backward, Back }

        /// <summary>Reorders this note among the other notes (within the pinned or unpinned group).</summary>
        internal void Layer(LayerOp op)
        {
            if (!IsHandleCreated) return;
            var band = mgr.ZOrder(TopMost);   // top first, same pinned state, visible
            int i = band.IndexOf(this);
            if (i < 0) return;
            const uint flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;
            switch (op)
            {
                case LayerOp.Front:
                    SetWindowPos(Handle, TopMost ? HWND_TOPMOST : HWND_TOP, 0, 0, 0, 0, flags);
                    Flash("⤒ Brought to front");
                    break;
                case LayerOp.Forward:
                    if (i == 0) { Layer(LayerOp.Front); return; }
                    SetWindowPos(band[i - 1].Handle, Handle, 0, 0, 0, 0, flags);   // the note above goes just below this one
                    Flash("▲ Moved forward");
                    break;
                case LayerOp.Backward:
                    if (i >= band.Count - 1) { Flash("Already at the back"); return; }
                    SetWindowPos(Handle, band[i + 1].Handle, 0, 0, 0, 0, flags);   // go just below the note under this one
                    Flash("▼ Moved back");
                    break;
                case LayerOp.Back:
                    if (i < band.Count - 1) SetWindowPos(Handle, band[band.Count - 1].Handle, 0, 0, 0, 0, flags);
                    Flash("⤓ Sent to back");
                    break;
            }
        }

        /// <summary>Ctrl+mouse wheel over a note moves it one layer forward or back.</summary>
        internal void LayerByWheel(int delta) { Layer(delta > 0 ? LayerOp.Forward : LayerOp.Backward); }

        void Flash(string text)
        {
            if (Busy) return;
            lblStatus.Text = text;
            statusTimer.Stop();
            statusTimer.Start();
        }

        #endregion

        #region Title, dates, content

        /// <summary>The note's own title (may be empty).</summary>
        internal string TitleText
        {
            get { return txtTitle.Text.Trim(); }
            set { txtTitle.Text = (value ?? "").Replace("\r", "").Replace("\n", " ").Trim(); }
        }

        /// <summary>Sets title, text and dates without counting it as an edit (loading from disk / creating).</summary>
        internal void LoadContent(string title, string text, bool isRtf, DateTime created, DateTime modified)
        {
            loadingContent = true;
            try
            {
                TitleText = title;
                if (isRtf) { try { Body.Rtf = text; } catch { Body.Text = text; } }
                else Body.Text = text;
                CreatedOn = created;
                ModifiedOn = modified;
                RecolorText();
            }
            finally { loadingContent = false; }
            UpdateDate();
            UpdateCaption();
        }

        void Edited(bool bodyChanged)
        {
            mgr.MarkDirty(this);
            if (loadingContent) return;
            ModifiedOn = DateTime.Now;
            UpdateDate();
            if (bodyChanged) ScheduleAutoTitle();
        }

        void UpdateDate()
        {
            var d = CreatedOn;
            lblDate.Text = d.Date == DateTime.Today ? "Today, " + d.ToShortTimeString()
                : d.Year == DateTime.Today.Year ? d.ToString("MMM d") : d.ToString("MMM d, yyyy");
            dateTip.SetToolTip(lblDate, "Created " + CreatedOn.ToString("f") + "\nEdited " + ModifiedOn.ToString("f"));
        }

        /// <summary>Asks Qwen for a title ~2.5 s after typing stops, once, if the note has text but no title.</summary>
        internal void ScheduleAutoTitle()
        {
            if (AutoTitleTried || TitleText.Length > 0) return;
            titleTimer.Stop();
            titleTimer.Start();
        }

        void TryAutoTitle()
        {
            if (AutoTitleTried || TitleText.Length > 0 || Busy || IsDisposed) return;
            string text = Body.Text.Trim();
            if (text.Length < 12 || text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length < 2) return;
            if (mgr.RequestAutoTitle(this)) { AutoTitleTried = true; mgr.MarkDirty(this); }
        }

        /// <summary>Called with Qwen's suggested title; ignored if the user has typed one meanwhile.</summary>
        internal void ApplyAutoTitle(string title)
        {
            if (IsDisposed || TitleText.Length > 0 || string.IsNullOrEmpty(title)) return;
            TitleText = title;
            Flash("✦ Title by Qwen");
        }

        /// <summary>When a note has no title yet, moves its first line into the title (used after Summarize to Note).</summary>
        internal void TakeTitleFromFirstLine()
        {
            if (TitleText.Length > 0) return;
            string text = Body.Text.Replace("\r\n", "\n");
            int nl = text.IndexOf('\n');
            if (nl <= 0) return;
            string first = text.Substring(0, nl).Trim().TrimStart('#', '•', '-', '*', ' ').Trim();
            if (first.Length == 0 || first.Length > 80) return;
            AutoTitleTried = true;
            TitleText = first;
            Body.Text = text.Substring(nl + 1).TrimStart('\n').Replace("\n", "\r\n");
        }

        /// <summary>Title, dates, format and images as header lines for the note file.</summary>
        internal string InfoLines()
        {
            var sb = new StringBuilder();
            sb.Append("#title ").Append(TitleText).Append("\r\n");
            sb.Append("#created ").Append(CreatedOn.ToString("s", CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("#modified ").Append(ModifiedOn.ToString("s", CultureInfo.InvariantCulture)).Append("\r\n");
            if (images.Count > 0) sb.Append("#images ").Append(string.Join("|", images.ToArray())).Append("\r\n");
            sb.Append("#format rtf\r\n");
            return sb.ToString();
        }

        /// <summary>The body as saved: RTF, so formatting survives.</summary>
        internal string BodyForSave() { return Body.Rtf; }

        static string PickFont(params string[] names)
        {
            try
            {
                using (var fonts = new System.Drawing.Text.InstalledFontCollection())
                    foreach (string n in names)
                        foreach (var f in fonts.Families)
                            if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return n;
            }
            catch { }
            return "Segoe UI";
        }

        /// <summary>Taskbar / Alt+Tab caption: the note's title (or first line).</summary>
        void UpdateCaption()
        {
            string t = Title;
            string caption = t == "(empty note)" ? "Sticky Note" : t + " - Sticky Note";
            if (Text != caption) Text = caption;
        }

        /// <summary>The title, or else the first non-empty line; used in the Notes menu and taskbar.</summary>
        internal string Title
        {
            get
            {
                string own = TitleText;
                if (own.Length > 0) return own.Length > 40 ? own.Substring(0, 40) + "…" : own;
                foreach (string line in Body.Text.Split('\n'))
                {
                    string t = line.Trim().TrimStart('•', '-', '*', '#', ' ');
                    if (t.Length > 0) return t.Length > 40 ? t.Substring(0, 40) + "…" : t;
                }
                return "(empty note)";
            }
        }

        #endregion

        #region Colors and theme

        internal static Color BodyColor(int index, bool dark) { return FromRgb((dark ? DarkBodyRgb : BodyRgb)[index]); }
        internal static Color HeadColor(int index, bool dark) { return FromRgb((dark ? DarkHeadRgb : HeadRgb)[index]); }
        internal static Color TextColor(bool dark) { return dark ? Color.FromArgb(232, 232, 232) : Color.Black; }

        /// <summary>Re-applies colors after a light/dark switch.</summary>
        internal void ApplyTheme()
        {
            SetColor(ColorIndex);
            Theme.ApplyScrollbars(Body);
        }

        internal void SetColor(int index)
        {
            ColorIndex = Math.Max(0, Math.Min(ColorNames.Length - 1, index));
            bool dark = Theme.Dark;
            BackColor = Body.BackColor = BodyColor(ColorIndex, dark);
            header.BackColor = HeadColor(ColorIndex, dark);
            Body.ForeColor = TextColor(dark);
            titleRow.BackColor = txtTitle.BackColor = lblDate.BackColor = formatBar.BackColor = gallery.BackColor = Body.BackColor;
            txtTitle.ForeColor = dark ? Color.FromArgb(245, 245, 245) : Color.FromArgb(25, 25, 25);
            lblDate.ForeColor = dark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(110, 110, 110);
            lblStatus.ForeColor = dark ? Color.FromArgb(175, 175, 175) : Color.FromArgb(90, 90, 90);
            foreach (var b in headerButtons) b.ForeColor = dark ? Color.FromArgb(215, 215, 215) : Color.FromArgb(60, 60, 60);
            foreach (var b in formatButtons.Values) b.ForeColor = dark ? Color.FromArgb(200, 200, 200) : Color.FromArgb(70, 70, 70);
            RecolorText();
            UpdatePinButton();
            UpdateFormatButtons();
            Invalidate();
        }

        /// <summary>Makes all body text the theme's text color (formatting other than color is kept).</summary>
        void RecolorText()
        {
            if (!Body.IsHandleCreated && Body.TextLength == 0) return;
            bool wasLoading = loadingContent;
            loadingContent = true;
            int ss = Body.SelectionStart, sl = Body.SelectionLength;
            SendMessage(Body.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                Body.SelectAll();
                Body.SelectionColor = Body.ForeColor;
                Body.Select(ss, sl);
            }
            finally
            {
                SendMessage(Body.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                Body.Invalidate();
                loadingContent = wasLoading;
            }
        }

        #endregion

        #region Rich text formatting

        Label FormatButton(int key, string glyph, string tip, Font font, EventHandler onClick)
        {
            var b = new EdgeLabel
            {
                Text = glyph, Dock = DockStyle.Left, Width = formatBar.Height + 2, TextAlign = ContentAlignment.MiddleCenter,
                Font = font, Cursor = Cursors.Hand
            };
            headerTip.SetToolTip(b, tip);
            b.MouseEnter += delegate { b.BackColor = Theme.Dark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0); };
            b.MouseLeave += delegate { UpdateFormatButtons(); };
            b.Click += (s, e) => { onClick(s, e); Body.Focus(); };
            formatButtons[key] = b;
            return b;
        }

        /// <summary>Toggles bold/italic/underline/strikethrough on the selection without touching other formatting.</summary>
        void ToggleEffect(int mask)
        {
            if (Busy) return;
            var cf = GetCharFormat();
            bool allOn = (cf.dwMask & mask) != 0 && (cf.dwEffects & mask) != 0;
            var set = new CHARFORMAT { cbSize = Marshal.SizeOf(typeof(CHARFORMAT)), dwMask = mask, dwEffects = allOn ? 0 : mask };
            SendMessage(Body.Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, ref set);
            UpdateFormatButtons();
            Edited(true);
        }

        void ToggleBullets()
        {
            if (Busy) return;
            Body.SelectionBullet = !Body.SelectionBullet;
            UpdateFormatButtons();
            Edited(true);
        }

        CHARFORMAT GetCharFormat()
        {
            var cf = new CHARFORMAT { cbSize = Marshal.SizeOf(typeof(CHARFORMAT)) };
            if (Body.IsHandleCreated) SendMessage(Body.Handle, EM_GETCHARFORMAT, (IntPtr)SCF_SELECTION, ref cf);
            return cf;
        }

        /// <summary>Highlights the format buttons that apply to the current selection.</summary>
        void UpdateFormatButtons()
        {
            if (!Body.IsHandleCreated) return;
            var cf = GetCharFormat();
            Color on = Theme.Dark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(50, 0, 0, 0);
            foreach (var kv in formatButtons)
            {
                bool active = kv.Key > 0 ? (cf.dwMask & kv.Key) != 0 && (cf.dwEffects & kv.Key) != 0
                            : kv.Key == -1 && Body.SelectionBullet;
                kv.Value.BackColor = active ? on : Color.Transparent;
            }
        }

        #endregion

        #region Images

        string ImageDir { get { return Path.Combine(NoteManager.Dir, Id); } }

        void ChooseImage()
        {
            using (var dlg = new OpenFileDialog { Filter = ImageFilter + "|All files (*.*)|*.*", Multiselect = true, Title = "Add images to the note" })
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    foreach (string f in dlg.FileNames) AddImageFile(f);
        }

        static List<string> DroppedImages(IDataObject data)
        {
            var list = new List<string>();
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files != null)
                foreach (string f in files)
                    if (Array.IndexOf(ImageExtensions, Path.GetExtension(f).ToLowerInvariant()) >= 0) list.Add(f);
            return list;
        }

        /// <summary>Copies an image (GIFs included) into the note's folder and shows it in the image strip.</summary>
        internal void AddImageFile(string source)
        {
            try
            {
                using (var probe = Image.FromStream(new MemoryStream(File.ReadAllBytes(source)))) { }   // validate
                Directory.CreateDirectory(ImageDir);
                string name = Guid.NewGuid().ToString("N").Substring(0, 10) + Path.GetExtension(source).ToLowerInvariant();
                File.Copy(source, Path.Combine(ImageDir, name));
                images.Add(name);
                AddThumb(name);
                Edited(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't add that image:\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void AddClipboardImage(Image img)
        {
            try
            {
                Directory.CreateDirectory(ImageDir);
                string name = Guid.NewGuid().ToString("N").Substring(0, 10) + ".png";
                img.Save(Path.Combine(ImageDir, name), System.Drawing.Imaging.ImageFormat.Png);
                images.Add(name);
                AddThumb(name);
                Edited(false);
            }
            catch { }
        }

        internal void LoadImages(IEnumerable<string> names)
        {
            foreach (string n in names)
                if (n.Length > 0 && File.Exists(Path.Combine(ImageDir, n)) && !images.Contains(n)) { images.Add(n); AddThumb(n); }
        }

        void AddThumb(string name)
        {
            string path = Path.Combine(ImageDir, name);
            Stream stream;
            Image img;
            try
            {
                // Keep the stream open for the picture's lifetime: animated GIFs read frames from it.
                stream = new MemoryStream(File.ReadAllBytes(path));
                img = Image.FromStream(stream);
            }
            catch { return; }
            var pb = new EdgePictureBox { Image = img, SizeMode = PictureBoxSizeMode.Zoom, Tag = name, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 0, 4) };
            imageStreams[pb] = stream;
            headerTip.SetToolTip(pb, "Click to open · right-click for options");
            pb.Click += (s, e) => { if (((MouseEventArgs)e).Button == MouseButtons.Left) OpenImage(pb); };
            var cm = new ContextMenuStrip();
            cm.Items.Add("&Open", null, delegate { OpenImage(pb); });
            cm.Items.Add("&Copy", null, delegate { try { Clipboard.SetImage(pb.Image); } catch { } });
            cm.Items.Add("&Remove from Note", null, delegate { RemoveImage(pb); });
            pb.ContextMenuStrip = cm;
            gallery.Controls.Add(pb);
            gallery.Visible = true;
            LayoutGallery();
            AutoFit();
        }

        void OpenImage(PictureBox pb)
        {
            try { Process.Start(Path.Combine(ImageDir, (string)pb.Tag)); } catch { }
        }

        void RemoveImage(PictureBox pb)
        {
            string name = (string)pb.Tag;
            gallery.Controls.Remove(pb);
            if (pb.Image != null) pb.Image.Dispose();
            Stream s;
            if (imageStreams.TryGetValue(pb, out s)) { s.Dispose(); imageStreams.Remove(pb); }
            pb.Dispose();
            images.Remove(name);
            try { File.Delete(Path.Combine(ImageDir, name)); } catch { }
            gallery.Visible = images.Count > 0;
            LayoutGallery();
            AutoFit();
            Edited(false);
        }

        /// <summary>Sizes each picture to the note's width (keeping its aspect ratio) and the strip to fit, within limits.</summary>
        void LayoutGallery()
        {
            if (gallery.Controls.Count == 0) { gallery.Height = 0; return; }
            int width = Math.Max(40, ClientSize.Width - Padding.Horizontal - (gallery.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
            int total = gallery.Padding.Vertical;
            foreach (Control c in gallery.Controls)
            {
                var pb = (PictureBox)c;
                int h = pb.Image == null ? 60 : Math.Max(40, Math.Min(240, (int)(width * (pb.Image.Height / (double)pb.Image.Width))));
                pb.Size = new Size(width, h);
                total += h + pb.Margin.Vertical;
            }
            gallery.Height = Math.Min(total, 260);
        }

        internal void DeleteFiles()
        {
            foreach (var kv in imageStreams) { try { kv.Value.Dispose(); } catch { } }
            try { if (Directory.Exists(ImageDir)) Directory.Delete(ImageDir, true); } catch { }
        }

        #endregion

        #region Busy state and streaming

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

        /// <summary>Replaces the selection as one undoable edit (EM_REPLACESEL with undo).</summary>
        internal void ReplaceSelectionUndoable(string text)
        {
            bool ro = Body.ReadOnly;
            Body.ReadOnly = false;
            SendMessage(Body.Handle, 0x00C2, (IntPtr)1, text);
            Body.ReadOnly = ro;
        }

        /// <summary>Set by whoever is streaming into this note; Esc calls it.</summary>
        internal Action CancelStreaming;

        internal void SetStreamText(string text)
        {
            Body.Text = text;
            Body.Select(Body.TextLength, 0);
            Body.ScrollToCaret();
        }

        #endregion

        #region Auto-fit

        /// <summary>
        /// Grows the note to fit its content (down to the screen's bottom edge, at most 3/4 of the screen) and
        /// shrinks back toward the user's chosen height when content is removed; the text scrolls once capped.
        /// </summary>
        void AutoFit()
        {
            if (!IsHandleCreated || !Body.IsHandleCreated || WindowState != FormWindowState.Normal) return;
            if (baseHeight == 0) baseHeight = Height;
            int needed = NeededHeight();
            var wa = Screen.FromControl(this).WorkingArea;
            int cap = Math.Max(MinimumSize.Height, Math.Min(wa.Bottom - Top, wa.Height * 3 / 4));
            int target = Math.Max(baseHeight, Math.Min(needed, cap));
            if (target != Height) Height = target;   // programmatic: doesn't raise ResizeEnd, so baseHeight is kept
        }

        int NeededHeight()
        {
            int text = Math.Max(contentHeight, Body.Font.Height);
            return header.Height + titleRow.Height + formatBar.Height + (gallery.Visible ? gallery.Height : 0)
                 + GripBottom + text + Body.Font.Height / 2 + 6;
        }

        #endregion

        #region Persistence

        internal string Meta()
        {
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            return string.Format(CultureInfo.InvariantCulture, "#note x={0} y={1} w={2} h={3} bh={4} color={5} top={6} visible={7} op={8} at={9}",
                b.X, b.Y, b.Width, b.Height, baseHeight > 0 ? baseHeight : b.Height, ColorIndex, TopMost ? 1 : 0,
                WantsVisible ? 1 : 0, OpacityPercent, AutoTitleTried ? 1 : 0);
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
            int x, y, w, h, bh, c, top, vis, op, at;
            if (v.TryGetValue("w", out w) && v.TryGetValue("h", out h)) Size = new Size(w, h);
            baseHeight = v.TryGetValue("bh", out bh) && bh > 0 ? bh : Height;
            if (v.TryGetValue("x", out x) && v.TryGetValue("y", out y)) Location = new Point(x, y);
            if (v.TryGetValue("color", out c)) SetColor(c);
            if (v.TryGetValue("top", out top)) TopMost = top == 1;
            UpdatePinButton();
            if (v.TryGetValue("visible", out vis)) WantsVisible = vis == 1;
            if (v.TryGetValue("op", out op)) SetOpacity(op);
            if (v.TryGetValue("at", out at)) AutoTitleTried = at == 1;
            if (!OnScreen(Bounds)) Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - Width - 40, Screen.PrimaryScreen.WorkingArea.Top + 40);
        }

        internal static bool OnScreen(Rectangle r)
        {
            foreach (Screen s in Screen.AllScreens)
                if (s.WorkingArea.IntersectsWith(new Rectangle(r.X, r.Y, Math.Max(40, r.Width), 30))) return true;
            return false;
        }

        #endregion

        #region Header, keyboard, dragging

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
            var b = new EdgeLabel
            {
                Text = glyph, Dock = dock, Width = header.Height, TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Symbol", 10f), ForeColor = Color.FromArgb(60, 60, 60), Cursor = Cursors.Hand
            };
            headerTip.SetToolTip(b, tip);
            headerButtons.Add(b);
            b.MouseEnter += delegate { b.BackColor = Theme.Dark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0); };
            b.MouseLeave += delegate { b.BackColor = Color.Transparent; UpdatePinButton(); };
            b.Click += onClick;
            return b;
        }

        void Body_KeyDown(object sender, KeyEventArgs e)
        {
            // While Qwen is writing, block editing shortcuts (typing is blocked by read-only).
            if (Busy && e.Control && e.KeyCode != Keys.C && e.KeyCode != Keys.A) e.Handled = e.SuppressKeyPress = true;
        }

        /// <summary>Note shortcuts, handled at window level so they work wherever the focus is inside the note.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool inBody = Body.Focused || ActiveControl == Body;   // the text box, not the title
            switch (keyData)
            {
                case Keys.Escape:
                    if (Busy && CancelStreaming != null) { CancelStreaming(); return true; }
                    break;
                case Keys.Control | Keys.K:
                    if (Busy) SetStatus("✦ Qwen is still writing… (Esc to stop)");
                    else mgr.Ask(this);
                    return true;
                case Keys.Apps:
                case Keys.Shift | Keys.F10:
                    menu.Show(this, new Point(Width - menu.PreferredSize.Width - 4, header.Height));   // keyboard access to the ⋯ menu
                    return true;
                case Keys.Control | Keys.N: mgr.New("", Bounds); return true;
                case Keys.Control | Keys.D: if (!Busy) mgr.Delete(this); return true;
                case Keys.Control | Keys.P: SetPinned(!TopMost); return true;
                case Keys.Control | Keys.W: mgr.HideNote(this); return true;
                case Keys.Control | Keys.B: if (inBody) { ToggleEffect(CFM_BOLD); return true; } break;
                case Keys.Control | Keys.I: if (inBody) { ToggleEffect(CFM_ITALIC); return true; } break;
                case Keys.Control | Keys.U: if (inBody) { ToggleEffect(CFM_UNDERLINE); return true; } break;
                case Keys.Control | Keys.T: if (inBody) { ToggleEffect(CFM_STRIKEOUT); return true; } break;
                case Keys.Control | Keys.Shift | Keys.L: if (inBody) { ToggleBullets(); return true; } break;
                case Keys.Control | Keys.V:
                case Keys.Shift | Keys.Insert:
                    if (inBody && !Busy) { PasteIntoBody(); return true; }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Images (and copied image files) go to the image strip; text is pasted plain so fonts and colors match.</summary>
        void PasteIntoBody()
        {
            try
            {
                var files = Clipboard.ContainsFileDropList() ? DroppedImages(Clipboard.GetDataObject()) : new List<string>();
                if (files.Count > 0) { foreach (string f in files) AddImageFile(f); return; }
                if (Clipboard.ContainsImage()) { AddClipboardImage(Clipboard.GetImage()); return; }
                if (Clipboard.ContainsText()) Body.Paste(DataFormats.GetFormat(DataFormats.UnicodeText));
            }
            catch { }
        }

        // Moving by the header is done by hand rather than via HTCAPTION, because with a sizable frame
        // Windows would Aero-Snap a note dragged to a screen edge into a half-screen window.
        Point dragOffset;
        bool dragging;

        void DragStart(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
            ((Control)sender).Capture = true;
        }

        void DragMove(object sender, MouseEventArgs e)
        {
            if (dragging) Location = new Point(Cursor.Position.X - dragOffset.X, Cursor.Position.Y - dragOffset.Y);
        }

        void DragEnd(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            ((Control)sender).Capture = false;
            mgr.MarkDirty(this);
        }

        #endregion

        #region Window frame, hit-testing

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                // Lets the borderless note minimize/restore from its taskbar button and show the taskbar window menu.
                cp.Style |= WS_MINIMIZEBOX | WS_SYSMENU;
                // A sizable frame is what makes Windows honor the resize edges reported by WM_NCHITTEST.
                // WM_NCCALCSIZE below gives it zero thickness, so nothing about the note's look changes.
                cp.Style |= WS_THICKFRAME;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Dark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            // Resize grip in the bottom-right corner.
            int w = ClientSize.Width, h = ClientSize.Height;
            using (var grip = new Pen(Theme.Dark ? Color.FromArgb(110, 255, 255, 255) : Color.FromArgb(90, 0, 0, 0), 1.2f))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                for (int k = 1; k <= 3; k++) e.Graphics.DrawLine(grip, w - 3, h - 3 - k * 3, w - 3 - k * 3, h - 3);
            }
        }

        /// <summary>Width of the invisible resize border; about 10 px at 100% scaling, larger on high-DPI screens.</summary>
        int EdgeZone { get { return Math.Max(10, Font.Height / 2); } }

        /// <summary>Which resize edge (if any) a client point is on. Corners get double-size zones.</summary>
        internal int HitZone(Point p)
        {
            int w = ClientSize.Width, h = ClientSize.Height, z = EdgeZone, c = z * 2;
            int zTop = Math.Max(5, z / 2);   // thinner at the top so the header buttons stay clickable
            bool left = p.X < z, right = p.X >= w - z, top = p.Y < zTop, bottom = p.Y >= h - Math.Max(z, GripBottom);
            if ((top && p.X < c) || (left && p.Y < zTop + z)) return HTTOPLEFT;
            if ((top && p.X >= w - c) || (right && p.Y < zTop + z)) return HTTOPRIGHT;
            if ((bottom && p.X < c) || (left && p.Y >= h - c)) return HTBOTTOMLEFT;
            if ((bottom && p.X >= w - c) || (right && p.Y >= h - c)) return HTBOTTOMRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            return HTCLIENT;
        }

        static Point ScreenPoint(Message m)
        {
            long lp = m.LParam.ToInt64();
            return new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
        }

        /// <summary>
        /// Called by the note's child controls. In the resize border, answers HTTRANSPARENT so Windows asks the note
        /// itself (which reports the edge); Ctrl+wheel moves the note between layers. Returns true when handled.
        /// </summary>
        internal static bool PassNoteMessage(Control child, ref Message m)
        {
            if (m.Msg != WM_NCHITTEST && m.Msg != WM_MOUSEWHEEL) return false;
            var note = child.FindForm() as StickyNote;
            if (note == null) return false;
            if (m.Msg == WM_MOUSEWHEEL)
            {
                if (((long)m.WParam & MK_CONTROL) == 0) return false;
                note.LayerByWheel((short)(((long)m.WParam >> 16) & 0xFFFF));
                m.Result = IntPtr.Zero;
                return true;
            }
            if (note.WindowState != FormWindowState.Normal) return false;
            if (note.HitZone(note.PointToClient(ScreenPoint(m))) == HTCLIENT) return false;
            m.Result = (IntPtr)HTTRANSPARENT;
            return true;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCCALCSIZE:
                    if (m.WParam == IntPtr.Zero) break;
                    m.Result = IntPtr.Zero;   // the whole window is client area: no visible sizing border
                    return;
                case WM_NCPAINT:
                    return;                   // nothing non-client to draw
                case WM_NCACTIVATE:
                    m.Result = (IntPtr)1;     // accept the activation change without painting a classic caption
                    return;
                case WM_MOUSEWHEEL:
                    if (((long)m.WParam & MK_CONTROL) != 0) { LayerByWheel((short)(((long)m.WParam >> 16) & 0xFFFF)); return; }
                    break;
                case WM_SETTEXT:
                case WM_SETICON:
                    // DefWindowProc paints the old-style caption (icon + title) when these change on a window
                    // with a system menu; hide the window from it for the duration of the call.
                    int style = GetWindowLong(Handle, GWL_STYLE);
                    if ((style & WS_VISIBLE) != 0) SetWindowLong(Handle, GWL_STYLE, style & ~WS_VISIBLE);
                    base.WndProc(ref m);
                    if ((style & WS_VISIBLE) != 0) SetWindowLong(Handle, GWL_STYLE, style);
                    return;
            }
            base.WndProc(ref m);
            if (m.Msg != WM_NCHITTEST || (int)m.Result != HTCLIENT || WindowState != FormWindowState.Normal) return;
            int zone = HitZone(PointToClient(ScreenPoint(m)));
            if (zone != HTCLIENT) m.Result = (IntPtr)zone;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Alt+F4 on a note hides it; it is only really closed when deleted or when QwenPad exits.
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; mgr.HideNote(this); }
            base.OnFormClosing(e);
        }

        #endregion

        #region Win32

        const int WM_NCHITTEST = 0x84, HTCLIENT = 1, WM_MOUSEWHEEL = 0x20A, MK_CONTROL = 0x8, WM_SETREDRAW = 0x0B;
        const int HTTRANSPARENT = -1, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const int CS_DROPSHADOW = 0x20000, WS_MINIMIZEBOX = 0x20000, WS_SYSMENU = 0x80000, WS_THICKFRAME = 0x40000;
        const int WM_NCCALCSIZE = 0x83, WM_NCPAINT = 0x85, WM_NCACTIVATE = 0x86, WM_SETTEXT = 0x0C, WM_SETICON = 0x80;
        const int GWL_STYLE = -16, GWL_EXSTYLE = -20, WS_VISIBLE = 0x10000000, WS_EX_TOPMOST = 0x8;
        const int EM_GETCHARFORMAT = 0x43A, EM_SETCHARFORMAT = 0x444, SCF_SELECTION = 1;
        const int CFM_BOLD = 1, CFM_ITALIC = 2, CFM_UNDERLINE = 4, CFM_STRIKEOUT = 8;

        static readonly IntPtr HWND_TOP = IntPtr.Zero, HWND_TOPMOST = (IntPtr)(-1), HWND_NOTOPMOST = (IntPtr)(-2);
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CHARFORMAT
        {
            public int cbSize;
            public int dwMask;
            public int dwEffects;
            public int yHeight;
            public int yOffset;
            public int crTextColor;
            public byte bCharSet;
            public byte bPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szFaceName;
        }

        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref CHARFORMAT lParam);

        #endregion
    }

    // A note's child controls let clicks near the note's edge fall through to the note (so the resize border works
    // over the text and header like a normal window frame) and forward Ctrl+wheel for layer changes.
    sealed class EdgeTextBox : TextBox
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    sealed class EdgeRichTextBox : RichTextBox
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    sealed class EdgePanel : Panel
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    sealed class EdgeFlowPanel : FlowLayoutPanel
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    sealed class EdgeLabel : Label
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    sealed class EdgePictureBox : PictureBox
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    /// <summary>
    /// Loads, saves and tracks sticky notes. Each note is a .txt file in the notes folder next to the exe (header
    /// lines hold position, color, title and dates; the body is RTF), plus a folder for its images.
    /// Only one QwenPad process owns a notes folder at a time.
    /// </summary>
    sealed class NoteManager : IDisposable
    {
        readonly Action<string> insertIntoDocument;
        readonly Action<StickyNote> askQwen;
        readonly Func<StickyNote, bool> autoTitle;
        readonly List<StickyNote> notes = new List<StickyNote>();
        readonly HashSet<StickyNote> dirty = new HashSet<StickyNote>();
        readonly System.Windows.Forms.Timer saveTimer = new System.Windows.Forms.Timer { Interval = 800 };
        NotesListForm listForm;
        Mutex mutex;
        bool owner, loading;

        /// <summary>Raised (on the UI thread) when notes are added, removed or changed.</summary>
        public event EventHandler Changed;

        public static string Dir { get { return Path.Combine(Settings.AppDir, "notes"); } }

        public NoteManager(Action<string> insertIntoDocument, Action<StickyNote> askQwen, Func<StickyNote, bool> autoTitle)
        {
            this.insertIntoDocument = insertIntoDocument;
            this.askQwen = askQwen;
            this.autoTitle = autoTitle;
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
                        // Optional header lines (1.3+): #title, #created, #modified, #images, #format. Older files
                        // have none, so their dates fall back to the file's own timestamps and the body is plain text.
                        string rest = nl >= 0 ? all.Substring(nl + 1) : "", title = "";
                        DateTime created = File.GetCreationTime(path), modified = File.GetLastWriteTime(path);
                        bool rtf = false;
                        string[] imgs = new string[0];
                        while (rest.StartsWith("#title ") || rest.StartsWith("#created ") || rest.StartsWith("#modified ") ||
                               rest.StartsWith("#images ") || rest.StartsWith("#format "))
                        {
                            int end = rest.IndexOf('\n');
                            string line = (end >= 0 ? rest.Substring(0, end) : rest).TrimEnd('\r');
                            rest = end >= 0 ? rest.Substring(end + 1) : "";
                            int sp = line.IndexOf(' ');
                            string key = line.Substring(1, sp - 1), value = line.Substring(sp + 1);
                            DateTime d;
                            if (key == "title") title = value;
                            else if (key == "format") rtf = value.Trim() == "rtf";
                            else if (key == "images") imgs = value.Split('|');
                            else if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out d))
                            {
                                if (key == "created") created = d; else modified = d;
                            }
                        }
                        note.LoadContent(title, rtf ? rest : Crlf(rest), rtf, created, modified);
                        note.LoadImages(imgs);
                        notes.Add(note);
                        if (note.WantsVisible) note.Show();
                    }
                    catch { }
                }
            }
            finally { loading = false; }
        }

        /// <summary>Creates a note next to the given window bounds.</summary>
        public StickyNote New(string text, Rectangle near, string title = "", int color = 0)
        {
            if (!Available) return null;
            string id = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            while (File.Exists(Path.Combine(Dir, id + ".txt"))) id += "x";
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
            if (color != 0) note.SetColor(color);
            note.LoadContent(title ?? "", Crlf(text ?? ""), false, DateTime.Now, DateTime.Now);
            if (!string.IsNullOrEmpty(title)) note.AutoTitleTried = true;
            notes.Add(note);
            note.Show();
            note.Activate();
            note.Body.Select(note.Body.TextLength, 0);
            if (note.Body.TextLength > 0) note.ScheduleAutoTitle();
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
        public void PinAll(bool pinned) { foreach (var n in notes) n.SetPinned(pinned); }
        public int CountPinned() { int c = 0; foreach (var n in notes) if (n.TopMost) c++; return c; }
        public int CountVisible() { int c = 0; foreach (var n in notes) if (n.WantsVisible) c++; return c; }

        /// <summary>Visible notes with the given pinned state, in on-screen stacking order (top first).</summary>
        public List<StickyNote> ZOrder(bool pinned)
        {
            var byHandle = new Dictionary<IntPtr, StickyNote>();
            foreach (var n in notes) if (n.IsHandleCreated && n.Visible && n.TopMost == pinned) byHandle[n.Handle] = n;
            var list = new List<StickyNote>();
            EnumWindows((h, l) => { StickyNote n; if (byHandle.TryGetValue(h, out n)) list.Add(n); return true; }, IntPtr.Zero);
            return list;
        }

        public void Delete(StickyNote note)
        {
            if (note.Body.Text.Trim().Length > 0 &&
                MessageBox.Show(note.Visible ? (IWin32Window)note : listForm, "Delete this note?", "Sticky Notes", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            dirty.Remove(note);
            notes.Remove(note);
            try { File.Delete(PathOf(note)); } catch { }
            note.DeleteFiles();
            note.AllowClose = true;
            note.Close();
            RaiseChanged();
        }

        public void InsertIntoDocument(string text) { insertIntoDocument(text); }

        public void Ask(StickyNote note)
        {
            if (note.Busy) note.SetStatus("✦ Qwen is still writing… (Esc to stop)");
            else askQwen(note);
        }

        /// <summary>Asks the host to generate a title for the note; false if Qwen isn't available right now.</summary>
        public bool RequestAutoTitle(StickyNote note) { return autoTitle != null && autoTitle(note); }

        /// <summary>Opens (or brings back) the notes list palette.</summary>
        public void ShowList(Form near)
        {
            if (!Available) return;
            if (listForm == null || listForm.IsDisposed) listForm = new NotesListForm(this);
            listForm.ShowNear(near);
        }

        public void ApplyTheme()
        {
            foreach (var n in notes) n.ApplyTheme();
            if (listForm != null && !listForm.IsDisposed) listForm.ApplyTheme();
        }

        public void MarkDirty(StickyNote note)
        {
            if (loading || !notes.Contains(note)) return;
            dirty.Add(note);
            saveTimer.Stop();
            saveTimer.Start();
        }

        void RaiseChanged()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        void Flush()
        {
            saveTimer.Stop();
            if (dirty.Count == 0) return;
            try { Directory.CreateDirectory(Dir); } catch { return; }
            foreach (var note in dirty)
            {
                try { File.WriteAllText(PathOf(note), note.Meta() + "\r\n" + note.InfoLines() + note.BodyForSave(), new UTF8Encoding(false)); }
                catch { }
            }
            dirty.Clear();
            RaiseChanged();
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
            if (listForm != null && !listForm.IsDisposed) listForm.Close();
            foreach (var n in notes.ToArray()) { n.AllowClose = true; n.Close(); }
            notes.Clear();
            saveTimer.Dispose();
            if (mutex != null)
            {
                if (owner) try { mutex.ReleaseMutex(); } catch { }
                mutex.Dispose();
            }
        }

        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    }
}
