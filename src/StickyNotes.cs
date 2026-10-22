using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
        readonly EdgeTitleBox txtTitle = new EdgeTitleBox();
        readonly Label lblDate = new EdgeLabel();
        readonly ToolTip dateTip = new ToolTip();
        readonly Panel formatBar = new EdgePanel();
        readonly FlowLayoutPanel gallery = new EdgeFlowPanel();
        readonly List<string> images = new List<string>();
        readonly Dictionary<PictureBox, Stream> imageStreams = new Dictionary<PictureBox, Stream>();
        readonly Dictionary<string, int> imageWidths = new Dictionary<string, int>();   // user-chosen widths (0/absent = fit)

        /// <summary>When false (the default), left-clicking an image doesn't open it; right-click > Open still does.</summary>
        internal static bool ClickableImages;

        // Seasonal designs (see NoteDesigns): 0 = plain color.
        internal int Design { get; private set; }
        Bitmap designCache;
        string designKey;
        readonly Dictionary<int, Label> formatButtons = new Dictionary<int, Label>();
        readonly System.Windows.Forms.Timer titleTimer = new System.Windows.Forms.Timer { Interval = 2500 };
        readonly System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        // Free-draw sketch area (pen / brush / eraser), saved as notes\<id>\sketch.png.
        readonly EdgeSketchCanvas sketch = new EdgeSketchCanvas();
        readonly Panel drawBar = new EdgePanel();
        readonly Dictionary<SketchTool, Label> toolButtons = new Dictionary<SketchTool, Label>();
        readonly List<Label> colorButtons = new List<Label>();
        Label sizeButton;
        bool drawMode;
        int sketchHeight = 220, inkChoice;
        static readonly Color[] InkColors = { Color.Empty, Color.FromArgb(220, 50, 47), Color.FromArgb(38, 110, 220), Color.FromArgb(40, 160, 70), Color.FromArgb(255, 200, 0) };
        static readonly string[] InkNames = { "Ink (black / white in dark mode)", "Red", "Blue", "Green", "Highlighter yellow" };
        bool sketchOn;   // own flag: a child's Visible reads false while the note itself is hidden
        internal bool HasSketch { get { return sketchOn; } }

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
        Label colorButton;
        Color shownFontColor = Color.Black;
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
            header.Controls.Add(HeaderButton("✕", "Hide note (Ctrl+W). It stays saved: open it again from the Notes List or the Notes menu.", DockStyle.Right, delegate { mgr.HideNote(this); }));
            header.Controls.Add(HeaderButton("+", "New note (Ctrl+N)", DockStyle.Left, delegate { mgr.New("", Bounds, like: this); }));
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
            txtTitle.Multiline = false;
            txtTitle.DetectUrls = false;
            txtTitle.ScrollBars = RichTextBoxScrollBars.None;
            txtTitle.Placeholder = "Title";
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
            // Docked Left in reverse, so they read B I U S A • 🖼 ✏ from the left.
            formatBar.Controls.Add(FormatButton(-3, "✏", "Draw: pen, brush and eraser", new Font("Segoe UI Symbol", 10.5f), delegate { SetDrawMode(true); }));
            formatBar.Controls.Add(FormatButton(-2, "🖼", "Add an image or GIF", new Font("Segoe UI Symbol", 10f), delegate { ChooseImage(); }));
            formatBar.Controls.Add(FormatButton(-1, "•", "Bullets (Ctrl+Shift+L)", new Font("Segoe UI", 12f, FontStyle.Bold), delegate { ToggleBullets(); }));
            colorButton = FormatButton(-4, "A", "Font color", new Font("Segoe UI", 10f, FontStyle.Bold), delegate { ShowFontColors(); });
            colorButton.Padding = new Padding(0, 0, 0, 3);
            colorButton.Paint += (s, e) =>
            {
                using (var b = new SolidBrush(shownFontColor)) e.Graphics.FillRectangle(b, colorButton.Width / 2 - 6, colorButton.Height - 6, 12, 3);
            };
            formatBar.Controls.Add(colorButton);
            formatBar.Controls.Add(FormatButton(CFM_STRIKEOUT, "S", "Strikethrough (Ctrl+T)", new Font("Segoe UI", 10f, FontStyle.Strikeout), delegate { ToggleEffect(CFM_STRIKEOUT); }));
            formatBar.Controls.Add(FormatButton(CFM_UNDERLINE, "U", "Underline (Ctrl+U)", new Font("Segoe UI", 10f, FontStyle.Underline), delegate { ToggleEffect(CFM_UNDERLINE); }));
            formatBar.Controls.Add(FormatButton(CFM_ITALIC, "I", "Italic (Ctrl+I)", new Font("Georgia", 10.5f, FontStyle.Italic), delegate { ToggleEffect(CFM_ITALIC); }));
            formatBar.Controls.Add(FormatButton(CFM_BOLD, "B", "Bold (Ctrl+B)", new Font("Segoe UI", 10f, FontStyle.Bold), delegate { ToggleEffect(CFM_BOLD); }));

            // Image strip (animated GIFs play) between the text and the format bar.
            gallery.Dock = DockStyle.Bottom;
            gallery.FlowDirection = FlowDirection.LeftToRight;
            gallery.WrapContents = true;
            gallery.AutoScroll = true;
            gallery.Visible = false;
            gallery.Padding = new Padding(0, 2, 0, 0);

            // Sketch canvas (between the text and the images) and the drawing toolbar (replaces the format bar).
            sketch.Dock = DockStyle.Bottom;
            sketch.Height = sketchHeight;
            sketch.Visible = false;
            sketch.Changed += delegate { Edited(false); };
            BuildDrawBar();

            // Docking order: later-added controls dock further out.
            Controls.Add(Body);
            Controls.Add(sketch);
            Controls.Add(gallery);
            Controls.Add(formatBar);
            Controls.Add(drawBar);
            Controls.Add(titleRow);
            // The design's ruled lines follow the text area, so repaint when it moves or resizes.
            EventHandler relayout = delegate { if (Design > 0) Invalidate(true); };
            Body.Resize += relayout;
            Body.Move += relayout;
            Controls.Add(header);

            for (int i = 0; i < ColorNames.Length; i++)
            {
                int c = i;
                menu.Items.Add(new ToolStripMenuItem(ColorNames[i], Swatch(i), delegate { SetDesign(0); SetColor(c); mgr.MarkDirty(this); }));
            }
            miTop = new ToolStripMenuItem("📌 &Pin on Top of All Windows", null, delegate { SetPinned(!TopMost); }) { ShortcutKeyDisplayString = "Ctrl+P" };
            // Seasonal designs sit right under the colors, so colors and designs read as one palette (fall, then winter).
            var designItems = new List<ToolStripMenuItem>();
            for (int d = 1; d < NoteDesigns.Count; d++)
            {
                int dd = d;
                if (d == 1 || NoteDesigns.Season(d) != NoteDesigns.Season(d - 1)) menu.Items.Add(new ToolStripSeparator());
                var item = new ToolStripMenuItem(NoteDesigns.Icon(d) + " " + NoteDesigns.Names[d], NoteDesigns.Thumbnail(d, 20, 20), delegate { SetDesign(dd); })
                {
                    Tag = d,
                    ImageScaling = ToolStripItemImageScaling.None,
                    ToolTipText = NoteDesigns.Season(d) + " design"
                };
                designItems.Add(item);
                menu.Items.Add(item);
            }
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
                new ToolStripMenuItem("✏ &Draw (pen, brush, eraser)", null, delegate { SetDrawMode(!drawMode); }),
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
                foreach (var o in designItems) o.Checked = (int)o.Tag == Design;
                for (int i = 0; i < ColorNames.Length; i++) ((ToolStripMenuItem)menu.Items[i]).Checked = Design == 0 && i == ColorIndex;
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
                : HeaderIsDark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(55, 0, 0, 0);
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
            // Today's notes show just the time, leaving room for the title (hover for the full dates).
            lblDate.Text = d.Date == DateTime.Today ? d.ToShortTimeString()
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
            if (images.Count > 0) sb.Append("#images ").Append(ImageList()).Append("\r\n");
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
            // Setting ForeColor recolors every character, so take the text first and put its chosen colors back after.
            string before = Body.IsHandleCreated || Body.TextLength > 0 ? Body.Rtf : null;
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
            sketch.BackColor = drawBar.BackColor = Body.BackColor;
            sketch.ForeColor = TextColor(dark);
            txtTitle.PlaceholderColor = lblDate.ForeColor;
            Padding = new Padding(Grip, 0, Grip, GripBottom);

            var st = NoteDesigns.StyleOf(Design);
            if (st != null)
            {
                // Designs paint the whole window; the panels become see-through and take the design's colors.
                foreach (Control c in new Control[] { header, titleRow, lblDate, lblStatus, formatBar, drawBar, gallery, sketch })
                    c.BackColor = Color.Transparent;
                Body.ForeColor = txtTitle.ForeColor = sketch.ForeColor = st.Text;
                lblDate.ForeColor = lblStatus.ForeColor = txtTitle.PlaceholderColor = st.Muted;
                foreach (var b in headerButtons) b.ForeColor = st.HeaderFore;
                foreach (var b in formatButtons.Values) b.ForeColor = st.Text;
                Padding = new Padding(Math.Max(Grip, st.SideInset), 0, Math.Max(Grip, st.SideInset), Math.Max(GripBottom, st.BottomInset));
            }
            ApplyInkColor();
            UpdateDrawButtons();
            sketch.Invalidate();
            RecolorText(before);
            UpdatePinButton();
            UpdateFormatButtons();
            designKey = null;
            Invalidate(true);
        }

        /// <summary>Applies one of the fall designs (0 = plain note color).</summary>
        internal void SetDesign(int design)
        {
            Design = Math.Max(0, Math.Min(NoteDesigns.Count - 1, design));
            if (designCache != null) { designCache.Dispose(); designCache = null; }
            SetColor(ColorIndex);
            LayoutGallery();
            AutoFit();
            mgr.MarkDirty(this);
        }

        /// <summary>True when the header sits on a dark part of the window (dark mode or a dark design frame).</summary>
        bool HeaderIsDark { get { var st = NoteDesigns.StyleOf(Design); return st != null ? st.DarkHeader : Theme.Dark; } }

        Color HoverColor(bool dark) { return dark ? Color.FromArgb(55, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0); }

        /// <summary>True when the writing area is dark (dark mode with no design; every design has light paper).</summary>
        bool PaperIsDark { get { return Design == 0 && Theme.Dark; } }

        /// <summary>Default ink: the design's text color, else black / white for light / dark mode.</summary>
        Color InkDefault { get { var st = NoteDesigns.StyleOf(Design); return st != null ? st.Text : TextColor(Theme.Dark); } }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Design == 0) { base.OnPaintBackground(e); return; }
            // Re-render only when the size, text area or line spacing changes (ruled lines follow the text).
            var body = Body.Bounds;
            int lineH = TextRenderer.MeasureText("Ag", Body.Font).Height;
            string key = Design + "|" + ClientSize + "|" + body + "|" + lineH;
            if (designCache == null || key != designKey)
            {
                if (designCache != null) designCache.Dispose();
                designCache = NoteDesigns.Render(Design, ClientSize, header.Height, body, lineH);
                designKey = key;
            }
            e.Graphics.DrawImage(designCache, 0, 0);
        }

        /// <summary>
        /// Gives text in an automatic color (black, dark-mode white or a design's ink) the current text color, so it
        /// follows the theme and design; colors picked with the A button are kept. Works on the RTF color table.
        /// </summary>
        void RecolorText(string rtf = null)
        {
            if (!Body.IsHandleCreated && Body.TextLength == 0) return;
            if (rtf == null) rtf = Body.Rtf;
            string recolored = AutoColorsTo(rtf, Body.ForeColor);
            if (recolored == Body.Rtf) return;
            bool wasLoading = loadingContent;
            loadingContent = true;
            int ss = Body.SelectionStart, sl = Body.SelectionLength;
            SendMessage(Body.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                Body.Rtf = recolored;
                Body.Select(ss, sl);
            }
            catch { }
            finally
            {
                SendMessage(Body.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                Body.Invalidate();
                loadingContent = wasLoading;
            }
        }

        static string AutoColorsTo(string rtf, Color target)
        {
            int start = rtf.IndexOf("{\\colortbl", StringComparison.Ordinal);
            int end = start < 0 ? -1 : rtf.IndexOf('}', start);
            if (end < 0) return rtf;
            string to = string.Format(CultureInfo.InvariantCulture, "\\red{0}\\green{1}\\blue{2}", target.R, target.G, target.B);
            string table = Regex.Replace(rtf.Substring(start, end - start), @"\\red(\d+)\\green(\d+)\\blue(\d+)", m =>
                IsAutoColor(Color.FromArgb(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value))) ? to : m.Value);
            return rtf.Substring(0, start) + table + rtf.Substring(end);
        }

        /// <summary>The text colors QwenPad sets by itself (as opposed to ones picked with the A button).</summary>
        static bool IsAutoColor(Color c)
        {
            if (SameRgb(c, TextColor(false)) || SameRgb(c, TextColor(true))) return true;
            for (int d = 1; d < NoteDesigns.Count; d++) if (SameRgb(c, NoteDesigns.StyleOf(d).Text)) return true;
            return false;
        }

        static bool SameRgb(Color a, Color b) { return a.R == b.R && a.G == b.G && a.B == b.B; }

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
            b.MouseEnter += delegate { b.BackColor = HoverColor(PaperIsDark); };
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

        static readonly string[] FontColorNames = { "Red", "Orange", "Green", "Teal", "Blue", "Purple", "Pink", "Brown", "Gray" };
        static readonly int[] FontColorRgb = { 0xDC2626, 0xEA580C, 0x16A34A, 0x0D9488, 0x2563EB, 0x9333EA, 0xDB2777, 0x92400E, 0x6B7280 };

        /// <summary>The A button's palette: Automatic (follows the theme and design), nine colors, More Colors.</summary>
        void ShowFontColors()
        {
            if (Busy) return;
            var m = new ContextMenuStrip();
            m.Items.Add(new ToolStripMenuItem("&Automatic", ColorChip(Body.ForeColor), delegate { ApplyFontColor(Body.ForeColor); })
                { ToolTipText = "Follows light/dark mode and the note's design" });
            m.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < FontColorNames.Length; i++)
            {
                Color c = FromRgb(FontColorRgb[i]);
                m.Items.Add(new ToolStripMenuItem(FontColorNames[i], ColorChip(c), delegate { ApplyFontColor(c); }));
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("&More Colors...", null, delegate
            {
                using (var dlg = new ColorDialog { FullOpen = true, AnyColor = true, Color = shownFontColor })
                    if (dlg.ShowDialog(this) == DialogResult.OK) ApplyFontColor(dlg.Color);
            }));
            m.Closed += delegate { BeginInvoke((Action)(() => m.Dispose())); };
            m.Show(colorButton, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
        }

        /// <summary>Colors the selection (or what's typed next, when nothing is selected).</summary>
        void ApplyFontColor(Color c)
        {
            if (Busy || IsDisposed) return;
            var set = new CHARFORMAT { cbSize = Marshal.SizeOf(typeof(CHARFORMAT)), dwMask = CFM_COLOR, crTextColor = ColorTranslator.ToWin32(c) };
            SendMessage(Body.Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, ref set);
            Body.Focus();
            UpdateFormatButtons();
            Edited(true);
        }

        static Image ColorChip(Color c)
        {
            var bmp = new Bitmap(14, 14);
            using (var g = Graphics.FromImage(bmp))
            using (var fill = new SolidBrush(c))
            using (var pen = new Pen(Color.FromArgb(120, 0, 0, 0)))
            {
                g.FillRectangle(fill, 1, 1, 12, 12);
                g.DrawRectangle(pen, 1, 1, 11, 11);
            }
            return bmp;
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
            Color on = PaperIsDark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(50, 0, 0, 0);
            foreach (var kv in formatButtons)
            {
                bool active = kv.Key > 0 ? (cf.dwMask & kv.Key) != 0 && (cf.dwEffects & kv.Key) != 0
                            : kv.Key == -1 && Body.SelectionBullet;
                kv.Value.BackColor = active ? on : Color.Transparent;
            }
            // The A button's bar shows the selection's color (automatic text shows the current text color).
            Color sel = (cf.dwMask & CFM_COLOR) != 0 && (cf.dwEffects & CFE_AUTOCOLOR) == 0 ? ColorTranslator.FromWin32(cf.crTextColor) : Body.ForeColor;
            if (IsAutoColor(sel)) sel = Body.ForeColor;
            if (colorButton != null && sel.ToArgb() != shownFontColor.ToArgb()) { shownFontColor = sel; colorButton.Invalidate(); }
        }

        #endregion

        #region Free draw

        void BuildDrawBar()
        {
            drawBar.Dock = DockStyle.Bottom;
            drawBar.Height = Font.Height + 8;
            drawBar.Visible = false;
            int w = drawBar.Height - 2;
            Func<string, EventHandler, Label> button = (tip, click) =>
            {
                var b = new EdgeLabel { Dock = DockStyle.Left, Width = w, Cursor = Cursors.Hand, ImageAlign = ContentAlignment.MiddleCenter, TextAlign = ContentAlignment.MiddleCenter };
                headerTip.SetToolTip(b, tip);
                b.MouseEnter += delegate { b.BackColor = HoverColor(PaperIsDark); };
                b.MouseLeave += delegate { UpdateDrawButtons(); };
                b.Click += click;
                return b;
            };
            // Right side (docked Right, added outermost-last): Done, Clear, Undo.
            var done = button("Done drawing (Esc)", delegate { SetDrawMode(false); });
            done.Dock = DockStyle.Right; done.Text = "✓"; done.Font = new Font("Segoe UI Symbol", 11f, FontStyle.Bold);
            var clear = button("Clear the sketch", delegate { if (sketch.HasInk) sketch.Clear(); });
            clear.Dock = DockStyle.Right; clear.Text = "🗑"; clear.Font = new Font("Segoe UI Symbol", 10f);
            var undoB = button("Undo stroke (Ctrl+Z)", delegate { sketch.Undo(); });
            undoB.Dock = DockStyle.Right; undoB.Text = "↶"; undoB.Font = new Font("Segoe UI Symbol", 11f);

            // Left side, added in reverse so it reads: pen, brush, eraser, colors, size.
            sizeButton = button("Size: small / medium / large", delegate { sketch.SizeLevel = (sketch.SizeLevel + 1) % 3; UpdateDrawButtons(); });
            drawBar.Controls.Add(sizeButton);
            for (int i = InkColors.Length - 1; i >= 0; i--)
            {
                int ci = i;
                var c = button(InkNames[i], delegate { inkChoice = ci; ApplyInkColor(); UpdateDrawButtons(); });
                c.Width = w - 6;
                colorButtons.Insert(0, c);
                drawBar.Controls.Add(c);
            }
            foreach (var t in new[] { SketchTool.Eraser, SketchTool.Brush, SketchTool.Pen })
            {
                var tool = t;
                var b = button(t == SketchTool.Pen ? "Pen (P)" : t == SketchTool.Brush ? "Paint brush (B)" : "Eraser (E)",
                    delegate { sketch.Tool = tool; UpdateDrawButtons(); });
                toolButtons[t] = b;
                drawBar.Controls.Add(b);
            }
            drawBar.Controls.Add(undoB);
            drawBar.Controls.Add(clear);
            drawBar.Controls.Add(done);
        }

        /// <summary>Turns free-draw mode on (shows the sketch area and drawing tools) or off.</summary>
        internal void SetDrawMode(bool on)
        {
            if (Busy && on) return;
            drawMode = on;
            if (on)
            {
                if (!sketchOn) { sketch.Height = sketchHeight; sketch.Visible = sketchOn = true; }
                if (Width < 310) Width = 310;   // room for the whole toolbar
            }
            else if (!sketch.HasInk)
            {
                sketch.Visible = sketchOn = false;   // nothing drawn: drop the empty sketch area
                sketch.Dirty = true;
            }
            sketch.Drawing = on;
            formatBar.Visible = !on;
            drawBar.Visible = on;
            ApplyInkColor();
            UpdateDrawButtons();
            sketch.Invalidate();
            AutoFit();
            if (on) sketch.Focus(); else Body.Focus();
            mgr.MarkDirty(this);
        }

        void ApplyInkColor()
        {
            sketch.InkColor = inkChoice == 0 ? InkDefault : InkColors[inkChoice];
        }

        void UpdateDrawButtons()
        {
            if (sizeButton == null) return;
            bool dark = PaperIsDark;
            var st = NoteDesigns.StyleOf(Design);
            Color fg = st != null ? st.Text : dark ? Color.FromArgb(215, 215, 215) : Color.FromArgb(60, 60, 60);
            Color on = dark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(50, 0, 0, 0);
            foreach (var kv in toolButtons)
            {
                if (kv.Value.Image != null) kv.Value.Image.Dispose();
                kv.Value.Image = kv.Key == SketchTool.Pen ? SketchIcons.Pen(fg) : kv.Key == SketchTool.Brush ? SketchIcons.Brush(fg) : SketchIcons.Eraser(fg);
                kv.Value.BackColor = sketch.Tool == kv.Key ? on : Color.Transparent;
            }
            for (int i = 0; i < colorButtons.Count; i++)
            {
                var b = colorButtons[i];
                if (b.Image != null) b.Image.Dispose();
                b.Image = SketchIcons.Swatch(i == 0 ? InkDefault : InkColors[i], Color.FromArgb(120, fg));
                b.BackColor = inkChoice == i ? on : Color.Transparent;
            }
            if (sizeButton.Image != null) sizeButton.Image.Dispose();
            sizeButton.Image = SketchIcons.Dot(fg, new[] { 4, 8, 13 }[sketch.SizeLevel]);
            foreach (Control c in drawBar.Controls)
                if (!toolButtons.ContainsValue(c as Label) && !colorButtons.Contains(c as Label) && c != sizeButton) { c.ForeColor = fg; c.BackColor = Color.Transparent; }
        }

        void LoadSketch(int height)
        {
            string path = Path.Combine(ImageDir, "sketch.png");
            if (!File.Exists(path)) return;
            if (height > 40) sketchHeight = height;
            sketch.Height = sketchHeight;
            sketch.Visible = sketchOn = true;
            try { sketch.LoadFrom(path); } catch { sketch.Visible = sketchOn = false; }
            sketch.Dirty = false;
        }

        /// <summary>Writes (or removes) the sketch PNG when it changed. Called when the note is saved.</summary>
        internal void SaveSketchIfDirty()
        {
            if (!sketch.Dirty) return;
            string path = Path.Combine(ImageDir, "sketch.png");
            try
            {
                if (sketchOn && sketch.HasInk) sketch.SaveTo(path);
                else { if (File.Exists(path)) File.Delete(path); sketch.Dirty = false; }
            }
            catch { }
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

        /// <summary>Loads "name" or "name:width" entries from the note file.</summary>
        internal void LoadImages(IEnumerable<string> entries)
        {
            foreach (string entry in entries)
            {
                string n = entry;
                int colon = entry.LastIndexOf(':'), w;
                if (colon > 0 && int.TryParse(entry.Substring(colon + 1), out w)) { n = entry.Substring(0, colon); imageWidths[n] = w; }
                if (n.Length > 0 && File.Exists(Path.Combine(ImageDir, n)) && !images.Contains(n)) { images.Add(n); AddThumb(n); }
            }
        }

        /// <summary>"#images" value: each file name, with ":width" when the user resized it.</summary>
        string ImageList()
        {
            var parts = new List<string>();
            foreach (string n in images)
            {
                int w;
                parts.Add(imageWidths.TryGetValue(n, out w) && w > 0 ? n + ":" + w : n);
            }
            return string.Join("|", parts.ToArray());
        }

        /// <summary>Applies the Clickable Images setting (cursor and tooltip) to every picture in this note.</summary>
        internal void UpdateImageMode()
        {
            foreach (Control c in gallery.Controls)
            {
                c.Cursor = ClickableImages ? Cursors.Hand : Cursors.Default;
                headerTip.SetToolTip(c, (ClickableImages ? "Click to open · " : "") + "drag the corner to resize · right-click for options");
            }
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
            var pb = new EdgePictureBox { Image = img, SizeMode = PictureBoxSizeMode.Zoom, Tag = name, Margin = new Padding(0, 0, 4, 4), BackColor = Color.Transparent };
            imageStreams[pb] = stream;

            // Drag the bottom-right corner to resize (aspect ratio kept); click opens only when Clickable Images is on.
            bool resizing = false, resized = false, hover = false;
            int startX = 0, startW = 0;
            Func<Point, bool> inGrip = p => p.X >= pb.Width - 16 && p.Y >= pb.Height - 16;
            pb.MouseEnter += delegate { hover = true; pb.Invalidate(); };
            pb.MouseLeave += delegate { hover = false; pb.Invalidate(); };
            pb.MouseDown += (s, e) =>
            {
                resized = false;
                if (e.Button != MouseButtons.Left || !inGrip(e.Location)) return;
                resizing = true;
                startX = e.X;          // the picture's left edge stays put, so its own coordinates work
                startW = pb.Width;
                pb.Capture = true;
            };
            pb.MouseMove += (s, e) =>
            {
                if (resizing)
                {
                    int w = Math.Max(40, Math.Min(MaxImageWidth, startW + e.X - startX));
                    pb.Size = new Size(w, ImageHeightFor(pb, w));
                    resized = true;
                    return;
                }
                pb.Cursor = inGrip(e.Location) ? Cursors.SizeNWSE : ClickableImages ? Cursors.Hand : Cursors.Default;
            };
            pb.MouseUp += (s, e) =>
            {
                if (!resizing) return;
                resizing = false;
                pb.Capture = false;
                if (!resized) return;
                SetImageWidth(pb, pb.Width >= MaxImageWidth - 2 ? 0 : pb.Width);
            };
            pb.Click += (s, e) =>
            {
                if (resized || ((MouseEventArgs)e).Button != MouseButtons.Left) return;
                if (ClickableImages) OpenImage(pb);
            };
            pb.Paint += (s, e) =>
            {
                if (!hover) return;
                // Resize grip in the picture's corner.
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var bg = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) e.Graphics.FillPolygon(bg, new[] { new Point(pb.Width, pb.Height - 16), new Point(pb.Width, pb.Height), new Point(pb.Width - 16, pb.Height) });
                using (var pen = new Pen(Color.FromArgb(160, 0, 0, 0), 1.2f))
                    for (int k = 1; k <= 3; k++) e.Graphics.DrawLine(pen, pb.Width - 2, pb.Height - 2 - k * 4, pb.Width - 2 - k * 4, pb.Height - 2);
            };

            var cm = new ContextMenuStrip();
            cm.Items.Add("&Open", null, delegate { OpenImage(pb); });
            cm.Items.Add("&Copy", null, delegate { try { Clipboard.SetImage(pb.Image); } catch { } });
            var size = new ToolStripMenuItem("&Size");
            size.DropDownItems.Add("&Small", null, delegate { SetImageWidth(pb, MaxImageWidth / 3); });
            size.DropDownItems.Add("&Medium", null, delegate { SetImageWidth(pb, MaxImageWidth / 2); });
            size.DropDownItems.Add("&Large", null, delegate { SetImageWidth(pb, MaxImageWidth * 3 / 4); });
            size.DropDownItems.Add("&Full Width", null, delegate { SetImageWidth(pb, 0); });
            cm.Items.Add(size);
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add("&Remove from Note", null, delegate { RemoveImage(pb); });
            pb.ContextMenuStrip = cm;
            gallery.Controls.Add(pb);
            gallery.Visible = true;
            UpdateImageMode();
            LayoutGallery();
            AutoFit();
        }

        int MaxImageWidth { get { return Math.Max(40, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 6); } }

        static int ImageHeightFor(PictureBox pb, int width)
        {
            if (pb.Image == null || pb.Image.Width == 0) return 60;
            return Math.Max(24, (int)(width * (pb.Image.Height / (double)pb.Image.Width)));
        }

        /// <summary>Sets a picture's width (0 = fit the note) and saves it.</summary>
        void SetImageWidth(PictureBox pb, int width)
        {
            string name = (string)pb.Tag;
            if (width <= 0) imageWidths.Remove(name); else imageWidths[name] = width;
            LayoutGallery();
            AutoFit();
            Edited(false);
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
            imageWidths.Remove(name);
            try { File.Delete(Path.Combine(ImageDir, name)); } catch { }
            gallery.Visible = images.Count > 0;
            LayoutGallery();
            AutoFit();
            Edited(false);
        }

        /// <summary>
        /// Sizes each picture: its saved width (capped to the note) or the full width, keeping the aspect ratio.
        /// Smaller pictures flow side by side; the strip grows to fit, up to a limit, then scrolls.
        /// </summary>
        void LayoutGallery()
        {
            if (gallery.Controls.Count == 0) { gallery.Height = 0; return; }
            int full = MaxImageWidth;
            foreach (Control c in gallery.Controls)
            {
                var pb = (PictureBox)c;
                int w;
                w = imageWidths.TryGetValue((string)pb.Tag, out w) && w > 0 ? Math.Min(w, full) : full;
                pb.Size = new Size(w, ImageHeightFor(pb, w));
            }
            int needed = gallery.GetPreferredSize(new Size(full + SystemInformation.VerticalScrollBarWidth, 0)).Height;
            gallery.Height = Math.Min(needed, 420);
        }

        /// <summary>Lets go of the note's image files so its folder can be moved to the trash.</summary>
        internal void ReleaseFiles()
        {
            foreach (var kv in imageStreams) { try { kv.Key.Image = null; kv.Value.Dispose(); } catch { } }
        }

        /// <summary>True when deleting the note would lose something: text, a title, images or a drawing.</summary>
        internal bool HasContent { get { return Body.Text.Trim().Length > 0 || TitleText.Length > 0 || images.Count > 0 || (sketchOn && sketch.HasInk); } }

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
            return header.Height + titleRow.Height + formatBar.Height + (images.Count > 0 ? gallery.Height : 0) + (sketchOn ? sketch.Height : 0)
                 + GripBottom + text + Body.Font.Height / 2 + 6;
        }

        #endregion

        #region Persistence

        internal string Meta()
        {
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            return string.Format(CultureInfo.InvariantCulture, "#note x={0} y={1} w={2} h={3} bh={4} color={5} top={6} visible={7} op={8} at={9} sk={10} sh={11} design={12}",
                b.X, b.Y, b.Width, b.Height, baseHeight > 0 ? baseHeight : b.Height, ColorIndex, TopMost ? 1 : 0,
                WantsVisible ? 1 : 0, OpacityPercent, AutoTitleTried ? 1 : 0, sketchOn && sketch.HasInk ? 1 : 0, sketchHeight, Design);
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
            int x, y, w, h, bh, c, top, vis, op, at, sk, sh;
            if (v.TryGetValue("sk", out sk) && sk == 1) LoadSketch(v.TryGetValue("sh", out sh) ? sh : 0);
            if (v.TryGetValue("w", out w) && v.TryGetValue("h", out h)) Size = new Size(w, h);
            baseHeight = v.TryGetValue("bh", out bh) && bh > 0 ? bh : Height;
            if (v.TryGetValue("x", out x) && v.TryGetValue("y", out y)) Location = new Point(x, y);
            if (v.TryGetValue("color", out c)) SetColor(c);
            if (v.TryGetValue("top", out top)) TopMost = top == 1;
            UpdatePinButton();
            if (v.TryGetValue("visible", out vis)) WantsVisible = vis == 1;
            if (v.TryGetValue("op", out op)) SetOpacity(op);
            if (v.TryGetValue("at", out at)) AutoTitleTried = at == 1;
            int design;
            if (v.TryGetValue("design", out design) && design > 0) SetDesign(design);
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
            b.MouseEnter += delegate { b.BackColor = HoverColor(HeaderIsDark); };
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
            if (drawMode)
            {
                bool onCanvas = sketch.Focused || ActiveControl == sketch;
                switch (keyData)
                {
                    case Keys.Escape: SetDrawMode(false); return true;
                    case Keys.Control | Keys.Z: sketch.Undo(); return true;
                    case Keys.P: if (onCanvas) { sketch.Tool = SketchTool.Pen; UpdateDrawButtons(); return true; } break;
                    case Keys.B: if (onCanvas) { sketch.Tool = SketchTool.Brush; UpdateDrawButtons(); return true; } break;
                    case Keys.E: if (onCanvas) { sketch.Tool = SketchTool.Eraser; UpdateDrawButtons(); return true; } break;
                }
            }
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
                case Keys.Control | Keys.N: mgr.New("", Bounds, like: this); return true;
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
            // A bare WM_CLOSE from another program (which WinForms reports as TaskManagerClosing) hides it too, rather
            // than destroying the window while QwenPad keeps running.
            if (!AllowClose && (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.TaskManagerClosing || e.CloseReason == CloseReason.None))
            {
                e.Cancel = true;
                mgr.HideNote(this);
                mgr.HiddenByClose(this);
            }
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
        const int CFM_BOLD = 1, CFM_ITALIC = 2, CFM_UNDERLINE = 4, CFM_STRIKEOUT = 8, CFM_COLOR = 0x40000000, CFE_AUTOCOLOR = 0x40000000;

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

    /// <summary>
    /// A RichTextBox on RichEdit 5.0 (msftedit.dll) with a transparent background, so a note's design shows through
    /// behind the text. Falls back to the standard control if msftedit isn't available.
    /// </summary>
    class TransparentRichTextBox : RichTextBox
    {
        static int msftedit;   // 0 unknown, 1 loaded, -1 unavailable

        protected override CreateParams CreateParams
        {
            get
            {
                if (msftedit == 0) msftedit = LoadLibrary("msftedit.dll") != IntPtr.Zero ? 1 : -1;
                var cp = base.CreateParams;
                if (msftedit == 1)
                {
                    cp.ClassName = "RICHEDIT50W";
                    cp.ExStyle |= 0x20;   // WS_EX_TRANSPARENT
                }
                return cp;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string file);
    }

    sealed class EdgeRichTextBox : TransparentRichTextBox
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    /// <summary>The note's single-line title: transparent rich edit with a "Title" placeholder (RichEdit has no cue banner).</summary>
    sealed class EdgeTitleBox : TransparentRichTextBox
    {
        public string Placeholder = "";
        public Color PlaceholderColor = Color.Gray;

        public EdgeTitleBox()
        {
            GotFocus += delegate { Invalidate(); };
            LostFocus += delegate { Invalidate(); };
            TextChanged += delegate { Invalidate(); };
        }

        protected override void WndProc(ref Message m)
        {
            if (StickyNote.PassNoteMessage(this, ref m)) return;
            base.WndProc(ref m);
            if (m.Msg == 0x000F && TextLength == 0 && !Focused && Placeholder.Length > 0)   // WM_PAINT
                using (var g = CreateGraphics())
                    TextRenderer.DrawText(g, Placeholder, Font, new Point(1, 0), PlaceholderColor, TextFormatFlags.NoPadding);
        }
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
            AutoBackup();
            PurgeTrash();
            loading = true;
            var recovered = new List<StickyNote>();
            try
            {
                // Each note is <id>.txt; <id>.bak is its previous save, used if the .txt is missing or damaged.
                var ids = new List<string>();
                foreach (string pattern in new[] { "*.txt", "*.bak" })
                    foreach (string path in Directory.GetFiles(Dir, pattern))
                    {
                        string id = Path.GetFileNameWithoutExtension(path);
                        if (!ids.Contains(id)) ids.Add(id);
                    }
                ids.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string id in ids)
                {
                    StickyNote note = null;
                    foreach (string ext in new[] { ".txt", ".bak" })
                    {
                        string path = Path.Combine(Dir, id + ext);
                        if (!File.Exists(path)) continue;
                        try { note = LoadFile(path, id); }
                        catch (Exception ex) { LogError(path, ex); }
                        if (note != null) { if (ext == ".bak") recovered.Add(note); break; }
                    }
                    if (note == null) continue;
                    notes.Add(note);
                    if (note.WantsVisible) note.Show();
                }
            }
            finally { loading = false; }
            foreach (var n in recovered) MarkDirty(n);   // rewrite the .txt from the backup copy
        }

        /// <summary>Reads one note file; null if it isn't a note (or is empty, e.g. cut short by a crash).</summary>
        StickyNote LoadFile(string path, string id)
        {
            string all = File.ReadAllText(path, Encoding.UTF8);
            int nl = all.IndexOf('\n');
            string head = (nl >= 0 ? all.Substring(0, nl) : all).TrimEnd('\r');
            if (!head.StartsWith("#note")) return null;
            var note = new StickyNote(this, id);
            try
            {
                note.ApplyMeta(head);
                // Optional header lines (1.3+): #title, #created, #modified, #images, then #format last. Older files
                // have none, so their dates fall back to the file's own timestamps and the body is plain text.
                // When a #format line follows, any other "#key value" lines before it come from a newer version and
                // are skipped, so a note saved by a later QwenPad still opens here.
                string rest = nl >= 0 ? all.Substring(nl + 1) : "", title = "";
                bool hasFormat = rest.StartsWith("#format ") || rest.Contains("\n#format ");
                DateTime created = File.GetCreationTime(path), modified = File.GetLastWriteTime(path);
                bool rtf = false;
                string[] imgs = new string[0];
                while (rest.StartsWith("#title ") || rest.StartsWith("#created ") || rest.StartsWith("#modified ") ||
                       rest.StartsWith("#images ") || rest.StartsWith("#format ") || (hasFormat && Regex.IsMatch(rest, @"^#[a-z]+ ")))
                {
                    int end = rest.IndexOf('\n');
                    string line = (end >= 0 ? rest.Substring(0, end) : rest).TrimEnd('\r');
                    rest = end >= 0 ? rest.Substring(end + 1) : "";
                    int sp = line.IndexOf(' ');
                    string key = line.Substring(1, sp - 1), value = line.Substring(sp + 1);
                    DateTime d;
                    if (key == "title") title = value;
                    else if (key == "format") { rtf = value.Trim() == "rtf"; break; }
                    else if (key == "images") imgs = value.Split('|');
                    else if ((key == "created" || key == "modified") &&
                             DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out d))
                    {
                        if (key == "created") created = d; else modified = d;
                    }
                }
                note.LoadContent(title, rtf ? rest : Crlf(rest), rtf, created, modified);
                note.LoadImages(imgs);
                return note;
            }
            catch
            {
                note.AllowClose = true;
                note.Dispose();
                throw;
            }
        }

        static void LogError(string path, Exception ex)
        {
            // Skip a damaged note, but leave a trace so it can be diagnosed.
            try
            {
                string log = Path.Combine(Settings.AppDir, "logs");
                Directory.CreateDirectory(log);
                File.AppendAllText(Path.Combine(log, "notes-errors.log"), DateTime.Now.ToString("s") + " " + Path.GetFileName(path) + ": " + ex + "\r\n");
            }
            catch { }
        }

        /// <summary>Creates a note next to the given window bounds.</summary>
        /// <param name="like">When created from a note (Ctrl+N, +): copy its color or design, opacity and pin, and place it beside that note.</param>
        public StickyNote New(string text, Rectangle near, string title = "", int color = 0, StickyNote like = null)
        {
            if (!Available) return null;
            string id = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            while (File.Exists(Path.Combine(Dir, id + ".txt"))) id += "x";
            var note = new StickyNote(this, id);
            note.Location = FreeSpot(near, note.Size, near.Width > note.Width * 2);
            if (like != null)
            {
                note.SetColor(like.ColorIndex);
                if (like.Design > 0) note.SetDesign(like.Design);
                if (like.OpacityPercent < 100) note.SetOpacity(like.OpacityPercent);
            }
            else if (color != 0) note.SetColor(color);
            note.LoadContent(title ?? "", Crlf(text ?? ""), false, DateTime.Now, DateTime.Now);
            if (!string.IsNullOrEmpty(title)) note.AutoTitleTried = true;
            notes.Add(note);
            note.Show();
            // A note made from a pinned note is pinned too; otherwise it would open hidden behind it.
            if (like != null && like.TopMost) note.SetPinned(true);
            else note.BringToFront();
            note.Activate();
            note.Body.Focus();
            note.Body.Select(note.Body.TextLength, 0);
            if (note.Body.TextLength > 0) note.ScheduleAutoTitle();
            MarkDirty(note);
            return note;
        }

        /// <summary>
        /// Picks where a new note goes. Beside a note: right, left, below or above it, whichever fits on screen without
        /// covering another note. Next to the main window: its top-right corner. Either way it never lands exactly on
        /// top of another note (it cascades instead).
        /// </summary>
        Point FreeSpot(Rectangle near, Size size, bool nearMainWindow)
        {
            var area = Screen.FromRectangle(near).WorkingArea;
            Func<Point, Point> clamp = p => new Point(Math.Max(area.Left, Math.Min(p.X, area.Right - size.Width)), Math.Max(area.Top, Math.Min(p.Y, area.Bottom - size.Height)));
            Func<Point, bool> fits = p => area.Contains(new Rectangle(p, size));
            Func<Point, bool> clear = p =>
            {
                var r = new Rectangle(p, size);
                foreach (var n in notes)
                {
                    if (!n.Visible) continue;
                    var hit = Rectangle.Intersect(r, n.Bounds);
                    if (hit.Width * hit.Height > size.Width * size.Height / 5) return false;   // covers more than a fifth
                }
                return true;
            };
            Func<Point, bool> taken = p =>
            {
                foreach (var n in notes) if (n.Visible && Math.Abs(n.Left - p.X) < 10 && Math.Abs(n.Top - p.Y) < 10) return true;
                return false;
            };

            Point start;
            if (nearMainWindow) start = new Point(near.Right - size.Width - 30, near.Top + 80);
            else
            {
                var candidates = new[]
                {
                    new Point(near.Right + 12, near.Top), new Point(near.Left - size.Width - 12, near.Top),
                    new Point(near.Left, near.Bottom + 12), new Point(near.Left, near.Top - size.Height - 12)
                };
                foreach (var c in candidates) if (fits(c) && clear(c)) return c;
                foreach (var c in candidates) if (fits(c)) { start = c; goto cascade; }
                start = new Point(near.Left + 28, near.Top + 28);
            }
        cascade:
            var pos = clamp(start);
            for (int i = 0; i < 12 && taken(pos); i++) pos = clamp(new Point(pos.X + (nearMainWindow ? -26 : 28), pos.Y + 28));
            return pos;
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

        // Notes hidden by a close message (Alt+F4, taskbar Close), with when. If QwenPad itself exits within a few
        // seconds (taskbar "Close all windows" closes every note and then the app), those notes weren't meant to be
        // hidden, so they're saved as visible and come back on the next launch.
        readonly Dictionary<StickyNote, DateTime> closeHidden = new Dictionary<StickyNote, DateTime>();
        DateTime exitSince = DateTime.MaxValue;

        internal void HiddenByClose(StickyNote note) { closeHidden[note] = DateTime.Now; }

        /// <summary>QwenPad is about to close (before any save prompt).</summary>
        public void ExitStarting()
        {
            if (exitSince == DateTime.MaxValue) exitSince = DateTime.Now.AddSeconds(-5);
            ReopenClosedDuringExit(true);
        }

        /// <summary>The user cancelled closing QwenPad: notes closed meanwhile come back too.</summary>
        public void ExitCanceled()
        {
            ReopenClosedDuringExit(true);
            exitSince = DateTime.MaxValue;
        }

        void ReopenClosedDuringExit(bool show)
        {
            foreach (var kv in closeHidden)
            {
                var n = kv.Key;
                if (kv.Value < exitSince || n.WantsVisible || !notes.Contains(n)) continue;
                n.WantsVisible = true;
                if (show && !n.IsDisposed) n.Show();
                MarkDirty(n);
            }
            if (!show) closeHidden.Clear();
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

        /// <summary>Moves a note to the trash (notes\.trash, kept 30 days; Notes > Restore Deleted Notes brings it back).</summary>
        public void Delete(StickyNote note)
        {
            if (note.HasContent &&
                MessageBox.Show(note.Visible ? (IWin32Window)note : listForm, "Delete this note?\n\nYou can bring it back for 30 days with Notes > Restore Deleted Notes.",
                                "Sticky Notes", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            // Save its latest text first, so the trashed copy is complete.
            Flush();
            notes.Remove(note);
            note.ReleaseFiles();
            note.AllowClose = true;
            note.Close();
            try
            {
                Directory.CreateDirectory(TrashDir);
                string id = FreeId(TrashDir, note.Id);
                string txt = PathOf(note);
                if (File.Exists(txt)) { File.Move(txt, Path.Combine(TrashDir, id + ".txt")); File.SetLastWriteTime(Path.Combine(TrashDir, id + ".txt"), DateTime.Now); }
                string bak = Path.Combine(Dir, note.Id + ".bak");
                if (File.Exists(bak)) File.Delete(bak);
                string files = Path.Combine(Dir, note.Id);
                if (Directory.Exists(files)) Directory.Move(files, Path.Combine(TrashDir, id));
            }
            catch (Exception ex) { LogError(PathOf(note), ex); }
            RaiseChanged();
        }

        static string TrashDir { get { return Path.Combine(Dir, ".trash"); } }

        /// <summary>An id that's free in the folder (the given one, or it with a suffix).</summary>
        static string FreeId(string dir, string id)
        {
            string free = id;
            for (int i = 2; File.Exists(Path.Combine(dir, free + ".txt")) || Directory.Exists(Path.Combine(dir, free)); i++) free = id + "-" + i;
            return free;
        }

        /// <summary>Empties the trash of notes deleted more than 30 days ago.</summary>
        static void PurgeTrash()
        {
            try
            {
                if (!Directory.Exists(TrashDir)) return;
                foreach (string f in Directory.GetFiles(TrashDir, "*.txt"))
                {
                    if (File.GetLastWriteTime(f) > DateTime.Now.AddDays(-30)) continue;
                    string files = Path.Combine(TrashDir, Path.GetFileNameWithoutExtension(f));
                    try { if (Directory.Exists(files)) Directory.Delete(files, true); File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        /// <summary>Deleted notes, newest first: (id, title, deleted on).</summary>
        public List<Tuple<string, string, DateTime>> TrashedNotes()
        {
            var list = new List<Tuple<string, string, DateTime>>();
            try
            {
                if (!Directory.Exists(TrashDir)) return list;
                foreach (string f in Directory.GetFiles(TrashDir, "*.txt"))
                    list.Add(Tuple.Create(Path.GetFileNameWithoutExtension(f), PeekTitle(f), File.GetLastWriteTime(f)));
            }
            catch { }
            list.Sort((a, b) => b.Item3.CompareTo(a.Item3));
            return list;
        }

        /// <summary>A note file's title, or its first line of text, without opening it as a note.</summary>
        static string PeekTitle(string path)
        {
            try
            {
                string all = File.ReadAllText(path, Encoding.UTF8);
                var m = Regex.Match(all, @"^#title (.+?)\r?$", RegexOptions.Multiline);
                if (m.Success && m.Groups[1].Value.Trim().Length > 0) return m.Groups[1].Value.Trim();
                int f = all.IndexOf("\n#format ", StringComparison.Ordinal);
                string body = f >= 0 ? all.Substring(all.IndexOf('\n', f + 1) + 1) : all.Substring(all.IndexOf('\n') + 1);
                if (body.StartsWith("{\\rtf"))
                    using (var rtb = new RichTextBox()) { try { rtb.Rtf = body; body = rtb.Text; } catch { } }
                foreach (string line in body.Split('\n'))
                {
                    string t = line.Trim().TrimStart('•', '-', '*', '#', ' ');
                    if (t.Length > 0) return t.Length > 50 ? t.Substring(0, 50) + "…" : t;
                }
            }
            catch { }
            return "(empty note)";
        }

        /// <summary>Moves a note back from the trash and opens it.</summary>
        public StickyNote Restore(string trashId)
        {
            if (!Available) return null;
            try
            {
                string src = Path.Combine(TrashDir, trashId + ".txt");
                if (!File.Exists(src)) return null;
                string id = FreeId(Dir, trashId);
                File.Move(src, Path.Combine(Dir, id + ".txt"));
                string files = Path.Combine(TrashDir, trashId);
                if (Directory.Exists(files)) Directory.Move(files, Path.Combine(Dir, id));
                return Open(Path.Combine(Dir, id + ".txt"), id);
            }
            catch (Exception ex) { LogError(trashId, ex); return null; }
        }

        StickyNote Open(string path, string id)
        {
            StickyNote note;
            loading = true;
            try { note = LoadFile(path, id); }
            finally { loading = false; }
            if (note == null) return null;
            notes.Add(note);
            ShowNote(note);
            RaiseChanged();
            return note;
        }

        /// <summary>
        /// Copies the notes from another QwenPad folder (its notes folder, the folder holding it, or a backup) into
        /// this one. Notes already here are skipped; a different note with the same id gets a new id. Returns how
        /// many were added.
        /// </summary>
        public int Import(string folder)
        {
            if (!Available) return 0;
            if (Directory.Exists(Path.Combine(folder, "notes"))) folder = Path.Combine(folder, "notes");
            if (string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), Path.GetFullPath(Dir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return 0;
            Flush();
            Directory.CreateDirectory(Dir);
            int added = 0;
            foreach (string src in Directory.GetFiles(folder, "*.txt"))
            {
                try
                {
                    string text = File.ReadAllText(src, Encoding.UTF8);
                    if (!text.StartsWith("#note")) continue;
                    string id = Path.GetFileNameWithoutExtension(src), dest = Path.Combine(Dir, id + ".txt");
                    if (File.Exists(dest))
                    {
                        if (SameNote(File.ReadAllText(dest, Encoding.UTF8), text)) continue;
                        id = FreeId(Dir, id);
                        dest = Path.Combine(Dir, id + ".txt");
                    }
                    else if (Directory.Exists(Path.Combine(Dir, id))) { id = FreeId(Dir, id); dest = Path.Combine(Dir, id + ".txt"); }
                    CopyDir(Path.Combine(folder, Path.GetFileNameWithoutExtension(src)), Path.Combine(Dir, id));
                    File.Copy(src, dest);
                    if (Open(dest, id) != null) added++;
                }
                catch (Exception ex) { LogError(src, ex); }
            }
            return added;
        }

        /// <summary>Same note content, ignoring the position/visibility line (which changes as notes move).</summary>
        static bool SameNote(string a, string b)
        {
            int ia = a.IndexOf('\n'), ib = b.IndexOf('\n');
            return ia >= 0 && ib >= 0 && a.Substring(ia) == b.Substring(ib);
        }

        static void CopyDir(string from, string to)
        {
            if (!Directory.Exists(from)) return;
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            foreach (string d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
        }

        public static string BackupRoot { get { return Path.Combine(Settings.AppDir, "backups"); } }

        /// <summary>
        /// Copies the notes folder to backups\notes-&lt;time&gt;-v&lt;version&gt; once a day and whenever QwenPad's version
        /// changes (so an update can never cost notes), keeping the newest 15.
        /// </summary>
        static void AutoBackup()
        {
            try
            {
                if (Directory.GetFiles(Dir, "*.txt").Length == 0) return;
                string ver = "v" + typeof(NoteManager).Assembly.GetName().Version.ToString(3);
                var old = Directory.Exists(BackupRoot) ? new List<string>(Directory.GetDirectories(BackupRoot, "notes-*")) : new List<string>();
                old.Sort(StringComparer.OrdinalIgnoreCase);   // names start with the time, so this is oldest first
                if (old.Count > 0)
                {
                    string last = old[old.Count - 1];
                    if (last.EndsWith("-" + ver, StringComparison.OrdinalIgnoreCase) && Directory.GetCreationTime(last) > DateTime.Now.AddDays(-1)) return;
                }
                string dest = Path.Combine(BackupRoot, "notes-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + ver);
                Directory.CreateDirectory(dest);
                foreach (string f in Directory.GetFiles(Dir))
                    if (!f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
                foreach (string d in Directory.GetDirectories(Dir))
                    if (Path.GetFileName(d) != ".trash") CopyDir(d, Path.Combine(dest, Path.GetFileName(d)));
                old.Add(dest);
                for (int i = 0; i < old.Count - 15; i++) try { Directory.Delete(old[i], true); } catch { }
            }
            catch (Exception ex) { LogError("backup", ex); }
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

        public void ApplyImageMode() { foreach (var n in notes) n.UpdateImageMode(); }

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
                note.SaveSketchIfDirty();
                // Write a temp file, then swap it in (keeping the previous save as <id>.bak), so a crash or power
                // cut mid-save never leaves a half-written note.
                string path = PathOf(note), tmp = Path.Combine(Dir, note.Id + ".tmp"), bak = Path.Combine(Dir, note.Id + ".bak");
                try
                {
                    File.WriteAllText(tmp, note.Meta() + "\r\n" + note.InfoLines() + note.BodyForSave(), new UTF8Encoding(false));
                    if (!File.Exists(path)) File.Move(tmp, path);
                    else
                    {
                        try { File.Replace(tmp, path, bak, true); }
                        catch (IOException) { File.Copy(path, bak, true); File.Copy(tmp, path, true); File.Delete(tmp); }   // e.g. drives without Replace
                    }
                }
                catch (Exception ex) { LogError(path, ex); }
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
            if (exitSince != DateTime.MaxValue) ReopenClosedDuringExit(false);
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
