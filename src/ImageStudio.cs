using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace QwenPad
{
    /// <summary>
    /// Text-to-image and image editing with the local Qwen-Image model (through sd-server). The host manages the
    /// GPU swap and exposes the ImageClient; this window drives one job at a time with live step previews.
    /// </summary>
    sealed class ImageStudioForm : Form
    {
        readonly MainForm host;
        readonly Settings settings;

        readonly TextBox prompt = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        readonly Button btnGenerate = new Button { Text = "Generate", AutoSize = true, MinimumSize = new Size(100, 30) };
        readonly Button btnImprove = new Button { Text = "✦ Improve Prompt", AutoSize = true, MinimumSize = new Size(100, 28) };
        readonly ComboBox sizeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        readonly CheckBox lockSeed = new CheckBox { Text = "Lock seed", AutoSize = true };
        readonly TextBox seedBox = new TextBox { Width = 110, Enabled = false };
        readonly FlowLayoutPanel refStrip = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, AutoScroll = true, WrapContents = false, Padding = new Padding(4) };
        readonly Label refLabel = new Label { Text = "Reference images (for editing) — drag in, paste, or Add:", Dock = DockStyle.Top, Height = 18, Padding = new Padding(4, 2, 0, 0) };
        readonly PictureBox preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(24, 24, 28) };
        readonly ProgressBar bar = new ProgressBar { Dock = DockStyle.Top, Height = 8, Maximum = 1000 };
        readonly Label status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
        readonly Button btnCancel = new Button { Text = "Cancel", AutoSize = true, Enabled = false };
        readonly FlowLayoutPanel history = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 84, AutoScroll = true, WrapContents = false, Padding = new Padding(4), BackColor = Color.FromArgb(32, 32, 36) };
        readonly Button btnSave = new Button { Text = "Save As...", AutoSize = true, Enabled = false };
        readonly Button btnCopy = new Button { Text = "Copy", AutoSize = true, Enabled = false };
        readonly Button btnToNote = new Button { Text = "Send to Note", AutoSize = true, Enabled = false };
        readonly Button btnUseRef = new Button { Text = "Use as Reference", AutoSize = true, Enabled = false };
        readonly Button btnFolder = new Button { Text = "Open Folder", AutoSize = true };

        readonly List<byte[]> refs = new List<byte[]>();
        readonly List<string> refPaths = new List<string>();
        byte[] currentImage;
        long currentSeed = -1;
        StickyNote linkedNote;
        volatile bool cancelRequested;
        volatile string jobId;
        Thread worker;
        DateTime jobStarted;
        readonly System.Windows.Forms.Timer elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };

        static readonly string[] SizeNames =
        {
            "Square — 768 × 768", "Draft — 512 × 512 (fastest)", "Portrait — 640 × 896",
            "Landscape — 896 × 640", "Large — 1024 × 1024 (slow)"
        };
        static readonly Size[] Sizes =
        {
            new Size(768, 768), new Size(512, 512), new Size(640, 896), new Size(896, 640), new Size(1024, 1024)
        };

        public ImageStudioForm(MainForm host)
        {
            this.host = host;
            settings = host.AppSettings;
            Text = "Image Studio — QwenPad";
            try { Icon = host.Icon; } catch { }
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 720);
            MinimumSize = new Size(640, 560);
            Font = SystemFonts.MessageBoxFont;

            foreach (string n in SizeNames) sizeBox.Items.Add(n);
            sizeBox.SelectedIndex = Array.FindIndex(Sizes, s => (s.Width + "x" + s.Height) == settings.ImageSize);
            if (sizeBox.SelectedIndex < 0) sizeBox.SelectedIndex = 0;

            prompt.Font = new Font(Font.FontFamily, 11f);
            btnGenerate.Font = new Font(Font, FontStyle.Bold);
            btnGenerate.Click += delegate { StartGenerate(); };
            btnImprove.Click += delegate { ImprovePrompt(); };
            btnCancel.Click += delegate { CancelJob(); };
            lockSeed.CheckedChanged += delegate { seedBox.Enabled = lockSeed.Checked; };
            btnSave.Click += delegate { SaveAs(); };
            btnCopy.Click += delegate { try { if (currentImage != null) Clipboard.SetImage(ToImage(currentImage)); } catch { } };
            btnToNote.Click += delegate { SendToNote(); };
            btnUseRef.Click += delegate { if (currentImage != null) AddReference((byte[])currentImage.Clone(), null); };
            btnFolder.Click += delegate { try { Directory.CreateDirectory(OutputDir); System.Diagnostics.Process.Start(OutputDir); } catch { } };
            elapsedTimer.Tick += delegate { if (worker != null) UpdateElapsed(); };

            BuildLayout();
            AllowDrop = true;
            prompt.AllowDrop = true;
            foreach (Control c in new Control[] { this, prompt, preview })
            {
                c.DragEnter += (s, e) => { if (HasImageData(e.Data)) e.Effect = DragDropEffects.Copy; };
                c.DragDrop += (s, e) => AddDropped(e.Data);
            }
            Load += delegate { Theme.ApplyForm(this); Theme.ApplyScrollbars(prompt); RefreshHistory(); };
        }

        void BuildLayout()
        {
            // Prompt row with Generate / Improve on the right.
            var promptPanel = new Panel { Dock = DockStyle.Top, Height = 96, Padding = new Padding(6) };
            var promptButtons = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, AutoSize = true, Width = 150 };
            promptButtons.Controls.Add(btnGenerate);
            promptButtons.Controls.Add(btnImprove);
            promptPanel.Controls.Add(prompt);
            promptPanel.Controls.Add(promptButtons);

            var settingsPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 4, 6, 4), WrapContents = false };
            settingsPanel.Controls.Add(new Label { Text = "Size:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
            settingsPanel.Controls.Add(sizeBox);
            settingsPanel.Controls.Add(new Label { Text = "    ", AutoSize = true });
            settingsPanel.Controls.Add(lockSeed);
            settingsPanel.Controls.Add(seedBox);

            // Actions row under the preview.
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(6, 4, 6, 4), WrapContents = false };
            foreach (var b in new[] { btnSave, btnCopy, btnToNote, btnUseRef, btnFolder }) { b.Margin = new Padding(3); actions.Controls.Add(b); }

            var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            statusPanel.Controls.Add(status);
            var cancelHost = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            cancelHost.Controls.Add(btnCancel);
            statusPanel.Controls.Add(cancelHost);

            // Docking order: edges first, preview (Fill) last so it takes the center.
            Controls.Add(preview);
            Controls.Add(bar);
            Controls.Add(statusPanel);
            Controls.Add(actions);
            Controls.Add(history);
            Controls.Add(settingsPanel);
            Controls.Add(promptPanel);
            Controls.Add(refStrip);
            Controls.Add(refLabel);
            SetStatus("Describe an image and press Generate. Add reference images to edit instead.");
        }

        /// <summary>Opens the studio for a fresh prompt, a note (generate into it), or an image to edit.</summary>
        public void Prepare(StickyNote note, string seedText, string editImagePath)
        {
            linkedNote = note;
            if (!string.IsNullOrEmpty(seedText)) prompt.Text = seedText.Trim();
            if (!string.IsNullOrEmpty(editImagePath) && File.Exists(editImagePath))
            {
                try { AddReference(File.ReadAllBytes(editImagePath), editImagePath); } catch { }
                if (prompt.Text.Length == 0) prompt.Text = "";
                SetStatus("Editing an image: describe the change you want, then Generate.");
            }
            UpdateRefLabel();
            prompt.Focus();
        }

        // ---- generation ----

        void StartGenerate()
        {
            if (worker != null) return;
            string p = prompt.Text.Trim();
            if (p.Length == 0 && refs.Count == 0) { SetStatus("Enter a prompt first."); return; }
            if (ContentBlock.IsBlocked(p)) { MessageBox.Show(this, "This prompt combines a child/minor reference with sexual content, which can't be generated. This is the only kind of prompt QwenPad refuses.", "Image Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            SetBusy(true);
            SetStatus("Loading the image model… (this frees the GPU from the text model)");
            host.EnsureImageReady(() => RunJob(p), err => { SetBusy(false); SetStatus("⚠ " + err); });
        }

        void RunJob(string p)
        {
            var size = Sizes[sizeBox.SelectedIndex];
            long seed = -1;
            if (lockSeed.Checked) { long.TryParse(seedBox.Text.Trim(), out seed); }
            var job = new ImageJob { Prompt = p, Width = size.Width, Height = size.Height, Seed = seed };
            foreach (var r in refs) job.References.Add(r);
            cancelRequested = false;
            jobId = null;
            jobStarted = DateTime.Now;
            elapsedTimer.Start();
            worker = new Thread(() => Poll(job)) { IsBackground = true };
            worker.Start();
        }

        void Poll(ImageJob job)
        {
            try
            {
                string id = host.ImageApi.Submit(job);
                jobId = id;
                while (!cancelRequested)
                {
                    Thread.Sleep(600);
                    host.BeginInvokeSafe(() => host.MarkImageActivity());
                    ImageJobState st = host.ImageApi.Poll(id);
                    byte[] pv = st.Preview;
                    int step = st.Step, total = st.TotalSteps;
                    host.BeginInvokeSafe(() =>
                    {
                        if (pv != null) ShowBytes(pv, false);
                        if (total > 0) { bar.Value = Math.Max(0, Math.Min(1000, step * 1000 / total)); SetStatus(string.Format("Generating… step {0}/{1} · {2}", step, total, Elapsed())); }
                    });
                    if (st.Done)
                    {
                        if (st.Status == "completed" && st.Images.Count > 0) { long sd = st.Seed; host.BeginInvokeSafe(() => Completed(st.Images[0], sd)); }
                        else if (st.Status == "cancelled") host.BeginInvokeSafe(() => { SetBusy(false); SetStatus("Cancelled."); });
                        else host.BeginInvokeSafe(() => { SetBusy(false); SetStatus("⚠ " + (st.Error ?? "Generation failed. Try a smaller size if the GPU ran out of memory.")); });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                host.BeginInvokeSafe(() => { SetBusy(false); SetStatus("⚠ " + ex.Message); });
            }
            finally { worker = null; host.BeginInvokeSafe(() => elapsedTimer.Stop()); }
        }

        void Completed(byte[] png, long seed)
        {
            currentImage = png;
            currentSeed = seed;
            if (seed >= 0 && !lockSeed.Checked) seedBox.Text = seed.ToString();
            ShowBytes(png, true);
            bar.Value = 1000;
            SetBusy(false);
            btnSave.Enabled = btnCopy.Enabled = btnToNote.Enabled = btnUseRef.Enabled = true;
            string saved = SaveResult(png, seed);
            SetStatus("Done in " + Elapsed() + (saved != null ? " · saved to " + Path.GetFileName(saved) : ""));
            if (linkedNote != null && !linkedNote.IsDisposed && saved != null)
            {
                if (MessageBox.Show(this, "Add this image to the note \"" + linkedNote.Title + "\"?", "Image Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    try { linkedNote.AddImageFile(saved); } catch { }
            }
            RefreshHistory();
        }

        void CancelJob()
        {
            cancelRequested = true;
            string id = jobId;
            if (id != null) { var c = id; new Thread(() => { try { host.ImageApi.Cancel(c); } catch { } }) { IsBackground = true }.Start(); }
            SetStatus("Cancelling…");
        }

        void ImprovePrompt()
        {
            string p = prompt.Text.Trim();
            if (p.Length == 0) { SetStatus("Type a short idea first, then Improve Prompt."); return; }
            btnImprove.Enabled = false;
            SetStatus("Loading the text model to expand your prompt…");
            host.ImproveImagePrompt(p, better => { if (!string.IsNullOrEmpty(better)) prompt.Text = better; btnImprove.Enabled = true; SetStatus("Prompt expanded. Press Generate."); },
                                       err => { btnImprove.Enabled = true; SetStatus("⚠ " + err); });
        }

        // ---- references ----

        void AddReference(byte[] bytes, string path)
        {
            if (refs.Count >= 10) { SetStatus("Up to 10 reference images."); return; }
            Image img;
            try { img = ToImage(bytes); } catch { return; }
            refs.Add(bytes); refPaths.Add(path);
            var thumb = new PictureBox { Width = 60, Height = 60, SizeMode = PictureBoxSizeMode.Zoom, Image = img, Margin = new Padding(2), Cursor = Cursors.Hand };
            var tip = new ToolTip(); tip.SetToolTip(thumb, "Click to remove this reference");
            thumb.Click += delegate { int i = refStrip.Controls.IndexOf(thumb); if (i >= 0) { refs.RemoveAt(i); refPaths.RemoveAt(i); } refStrip.Controls.Remove(thumb); thumb.Dispose(); UpdateRefLabel(); };
            refStrip.Controls.Add(thumb);
            UpdateRefLabel();
        }

        void UpdateRefLabel()
        {
            refLabel.Text = refs.Count == 0 ? "Reference images (for editing) — drag in, paste, or use a note image. None yet."
                                            : refs.Count + " reference image(s) — the prompt is an edit instruction. Click a thumbnail to remove.";
            btnGenerate.Text = refs.Count > 0 ? "Edit" : "Generate";
        }

        void AddDropped(IDataObject data)
        {
            try
            {
                if (data.GetDataPresent(DataFormats.FileDrop))
                    foreach (string f in (string[])data.GetData(DataFormats.FileDrop))
                        if (IsImageFile(f)) AddReference(File.ReadAllBytes(f), f);
                var img = data.GetData(DataFormats.Bitmap) as Image;
                if (img != null) AddReference(ToPng(img), null);
            }
            catch { }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V))
            {
                try { if (Clipboard.ContainsImage()) { AddReference(ToPng(Clipboard.GetImage()), null); return true; } } catch { }
            }
            if (keyData == (Keys.Control | Keys.Enter)) { StartGenerate(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---- results / history ----

        string OutputDir { get { return Path.Combine(Settings.AppDir, settings.ImageOutputDir, DateTime.Now.ToString("yyyy-MM-dd")); } }

        string SaveResult(byte[] png, long seed)
        {
            try
            {
                Directory.CreateDirectory(OutputDir);
                string stem = "img-" + DateTime.Now.ToString("HHmmss-fff");
                string file = Path.Combine(OutputDir, stem + ".png");
                File.WriteAllBytes(file, png);
                var size = Sizes[sizeBox.SelectedIndex];
                File.WriteAllText(Path.Combine(OutputDir, stem + ".txt"),
                    prompt.Text.Trim() + "\r\nseed=" + seed + " size=" + size.Width + "x" + size.Height + " model=" + settings.ImageEncoder, new UTF8Encoding(false));
                return file;
            }
            catch { return null; }
        }

        void SaveAs()
        {
            if (currentImage == null) return;
            using (var dlg = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "qwenpad-image.png" })
                if (dlg.ShowDialog(this) == DialogResult.OK) try { File.WriteAllBytes(dlg.FileName, currentImage); } catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        }

        void SendToNote()
        {
            if (currentImage == null) return;
            string saved = SaveResult(currentImage, currentSeed);
            if (saved == null) { SetStatus("Couldn't save the image."); return; }
            if (linkedNote != null && !linkedNote.IsDisposed) { try { linkedNote.AddImageFile(saved); SetStatus("Added to the note."); } catch { } }
            else host.AddImageToNewNote(saved);
        }

        void RefreshHistory()
        {
            history.Controls.Clear();
            try
            {
                if (!Directory.Exists(OutputDir)) return;
                var files = new List<string>(Directory.GetFiles(OutputDir, "*.png"));
                files.Sort(StringComparer.OrdinalIgnoreCase); files.Reverse();
                foreach (string f in files)
                {
                    string path = f;
                    Image img;
                    try { img = ToImage(File.ReadAllBytes(f)); } catch { continue; }
                    var pb = new PictureBox { Width = 64, Height = 64, SizeMode = PictureBoxSizeMode.Zoom, Image = img, Margin = new Padding(2), Cursor = Cursors.Hand };
                    pb.Click += delegate { LoadFromHistory(path); };
                    history.Controls.Add(pb);
                }
            }
            catch { }
        }

        void LoadFromHistory(string file)
        {
            try
            {
                currentImage = File.ReadAllBytes(file);
                ShowBytes(currentImage, true);
                btnSave.Enabled = btnCopy.Enabled = btnToNote.Enabled = btnUseRef.Enabled = true;
                string txt = Path.ChangeExtension(file, ".txt");
                if (File.Exists(txt))
                {
                    string[] lines = File.ReadAllLines(txt);
                    if (lines.Length > 0 && prompt.Text.Trim().Length == 0) prompt.Text = lines[0];
                    SetStatus(Path.GetFileName(file) + (lines.Length > 1 ? " · " + lines[1] : ""));
                }
            }
            catch { }
        }

        // ---- helpers ----

        void ShowBytes(byte[] bytes, bool final)
        {
            try { var img = ToImage(bytes); var old = preview.Image; preview.Image = img; if (old != null && old != img) old.Dispose(); }
            catch { }
        }

        void SetBusy(bool busy)
        {
            btnGenerate.Enabled = btnImprove.Enabled = !busy;
            btnCancel.Enabled = busy;
            sizeBox.Enabled = !busy;
            if (!busy) bar.Value = 0;
        }

        void SetStatus(string text) { status.Text = text; }
        string Elapsed() { var t = DateTime.Now - jobStarted; return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00"); }
        void UpdateElapsed() { if (btnCancel.Enabled) { /* keep the elapsed time fresh in the status */ } }

        static bool IsImageFile(string f)
        {
            string e = Path.GetExtension(f).ToLowerInvariant();
            return e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".bmp" || e == ".gif" || e == ".webp";
        }
        static bool HasImageData(IDataObject d) { return d.GetDataPresent(DataFormats.FileDrop) || d.GetDataPresent(DataFormats.Bitmap); }

        static Image ToImage(byte[] bytes) { return Image.FromStream(new MemoryStream(bytes)); }

        static byte[] ToPng(Image img)
        {
            using (var ms = new MemoryStream()) { img.Save(ms, System.Drawing.Imaging.ImageFormat.Png); return ms.ToArray(); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (worker != null) CancelJob();
            // Hide instead of destroy so reopening is instant; the host unloads the model after an idle wait.
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); host.ImageStudioClosed(); return; }
            base.OnFormClosing(e);
        }
    }

    /// <summary>
    /// The one hard content limit: refuse prompts that sexualize minors. Everything else is allowed. Terms are matched
    /// as whole words, so ordinary prompts aren't blocked by coincidence (e.g. "girlfriend" or "babydoll" don't trip
    /// "girl"/"baby", and only a minor word together with a sexual word is refused).
    /// </summary>
    static class ContentBlock
    {
        static readonly string[] Minor = { "child", "children", "kid", "kids", "toddler", "toddlers", "infant", "infants", "baby", "babies", "preteen", "pre-teen", "minor", "minors", "underage", "teen", "teens", "teenage", "teenager", "teenagers", "boy", "boys", "girl", "girls", "schoolgirl", "schoolboy", "loli", "shota" };
        static readonly string[] Sexual = { "nude", "nudes", "naked", "nsfw", "sex", "sexual", "sexy", "porn", "pornographic", "explicit", "erotic", "lewd", "hentai", "genital", "genitals", "nipple", "nipples", "undress", "undressed", "lingerie", "topless", "cum", "aroused", "orgasm" };

        public static bool IsBlocked(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return false;
            string p = prompt.ToLowerInvariant();
            bool minor = false;
            foreach (string m in Minor) if (HasWord(p, m)) { minor = true; break; }
            if (!minor) return false;
            foreach (string s in Sexual) if (HasWord(p, s)) return true;
            return false;
        }

        /// <summary>True if term appears as a whole word (not inside a larger word).</summary>
        static bool HasWord(string text, string term)
        {
            int i = 0;
            while ((i = text.IndexOf(term, i, StringComparison.Ordinal)) >= 0)
            {
                bool leftOk = i == 0 || !(char.IsLetterOrDigit(text[i - 1]) || text[i - 1] == '-');
                int end = i + term.Length;
                bool rightOk = end >= text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] == '-');
                if (leftOk && rightOk) return true;
                i = end;
            }
            return false;
        }
    }
}
