using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace QwenPad
{
    sealed class ModelFile
    {
        public string FileName, Url, Sha256;
        public long Size;
    }

    sealed class ModelOption
    {
        public string Name, Description, FileName, Url, Sha256, Key;
        public long Size;
        /// <summary>For a bundle of several files (image models); otherwise the single file above.</summary>
        public ModelFile[] Bundle;

        public ModelFile[] Files
        {
            get { return Bundle ?? new[] { new ModelFile { FileName = FileName, Url = Url, Sha256 = Sha256, Size = Size } }; }
        }

        public long TotalSize { get { long n = 0; foreach (var f in Files) n += f.Size; return n; } }

        public bool InstalledIn(string dir)
        {
            foreach (var f in Files) if (!File.Exists(Path.Combine(dir, f.FileName))) return false;
            return true;
        }
    }

    /// <summary>
    /// One-time model download from Hugging Face. URLs are pinned to a commit and every file is
    /// checked against its published SHA-256. Interrupted downloads resume from the .part file.
    /// </summary>
    sealed class ModelDownloadDialog : Form
    {
        // Image models: Qwen-Image-2.1 Turbo with a Qwen3-VL-8B text encoder, its vision projector (for editing) and the VAE.
        // The denoiser and VAE are shared by both bundles, so switching encoders downloads only the encoder.
        static readonly ModelFile TurboQ4 = new ModelFile
        {
            FileName = "Qwen-Image-2.1-Turbo-AD-Q4_K.gguf", Size = 4201694944L,
            Url = "https://huggingface.co/AtomicChat/Qwen-Image-2.1-Turbo-GGUF/resolve/f5996597b053664d0885cfaa0dcc88afb47f92f6/Qwen-Image-2.1-Turbo-AD-Q4_K.gguf",
            Sha256 = "4bb73c53cbe284bbd6d69b9fdc59539c531f0389dd2aa567663d777f8130bc59"
        };
        static readonly ModelFile ImageVae = new ModelFile
        {
            FileName = "qwen_image_2.1_vae_bf16.safetensors", Size = 675509688L,
            Url = "https://huggingface.co/Comfy-Org/Qwen-Image-2.1/resolve/0217efd1d9c9452e84f6448903d6e21bfa171ffd/vae/qwen_image_2.1_vae_bf16.safetensors",
            Sha256 = "bb21f7473051e1ac368515dd3f2e15cd44d7a11748ee8823e1ddca3e4876b7c9"
        };

        public static readonly ModelOption[] ImageOptions =
        {
            new ModelOption
            {
                Key = "uncensored",
                Name = "Qwen-Image-2.1 Turbo, uncensored encoder (recommended)",
                Description = "Fast 8-step model for creating and editing images. Its text encoder has refusal behavior removed (AtomicChat), so it follows every prompt literally.",
                Bundle = new[]
                {
                    TurboQ4,
                    new ModelFile
                    {
                        FileName = "Qwen-Image-2.1-Turbo-Uncensored-AD-Q4_K.gguf", Size = 5287517120L,
                        Url = "https://huggingface.co/AtomicChat/Qwen-Image-2.1-Turbo-Uncensored-GGUF/resolve/df96693dbdfb8738c0bf1bbd0337d149808db948/Qwen-Image-2.1-Turbo-Uncensored-AD-Q4_K.gguf",
                        Sha256 = "8123f36d2b3e562deef164dffdae33bdb1d152797ad10bb630827eaa7bce795d"
                    },
                    new ModelFile
                    {
                        FileName = "mmproj-Qwen-Image-2.1-Turbo-Uncensored-F16.gguf", Size = 1159029984L,
                        Url = "https://huggingface.co/AtomicChat/Qwen-Image-2.1-Turbo-Uncensored-GGUF/resolve/df96693dbdfb8738c0bf1bbd0337d149808db948/mmproj-Qwen-Image-2.1-Turbo-Uncensored-F16.gguf",
                        Sha256 = "d9870a1f168db3d814596216aaf35702154147b443572c7939acca1802621ac6"
                    },
                    ImageVae
                }
            },
            new ModelOption
            {
                Key = "standard",
                Name = "Qwen-Image-2.1 Turbo, standard encoder",
                Description = "The same image model with the official Qwen3-VL-8B text encoder.",
                Bundle = new[]
                {
                    TurboQ4,
                    new ModelFile
                    {
                        FileName = "Qwen3VL-8B-Instruct-Q4_K_M.gguf", Size = 5027784800L,
                        Url = "https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF/resolve/f982a07559d4a2f6c8744d840bf6fccab30eea96/Qwen3VL-8B-Instruct-Q4_K_M.gguf",
                        Sha256 = "67d1659bfe71b89d50b45a4ad1a9e5b997e5bb16ce5da66a6a6167abd569e9e2"
                    },
                    new ModelFile
                    {
                        FileName = "mmproj-Qwen3VL-8B-Instruct-F16.gguf", Size = 1159029824L,
                        Url = "https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF/resolve/f982a07559d4a2f6c8744d840bf6fccab30eea96/mmproj-Qwen3VL-8B-Instruct-F16.gguf",
                        Sha256 = "ca524100ebf825c9a870db1c580d03879e0da0ab2541697e2458e64891cf9d38"
                    },
                    ImageVae
                }
            }
        };

        public static readonly ModelOption[] Options =
        {
            new ModelOption
            {
                Name = "Qwen3.5 9B (recommended)",
                Description = "Official Qwen3.5 9B, 4-bit (unsloth Q4_K_M). Best quality. Needs about 8 GB of free RAM.",
                FileName = "Qwen3.5-9B-Q4_K_M.gguf",
                Url = "https://huggingface.co/unsloth/Qwen3.5-9B-GGUF/resolve/3885219b6810b007914f3a7950a8d1b469d598a5/Qwen3.5-9B-Q4_K_M.gguf",
                Sha256 = "03b74727a860a56338e042c4420bb3f04b2fec5734175f4cb9fa853daf52b7e8",
                Size = 5680522464
            },
            new ModelOption
            {
                Name = "Qwen3.5 9B Abliterated",
                Description = "Community variant of the same model with refusal behavior removed (huihui-ai, quantized by mradermacher).",
                FileName = "Qwen3.5-9B-Abliterated-Q4_K_M.gguf",
                Url = "https://huggingface.co/mradermacher/Huihui-Qwen3.5-9B-abliterated-GGUF/resolve/9f646d7eda193ddf2348134f3bff3d49eed7a2c6/Huihui-Qwen3.5-9B-abliterated.Q4_K_M.gguf",
                Sha256 = "ea1858ef4dc4b648b8dbb44612962a0333e945060dd0545ac0f28d7c4416e4b3",
                Size = 5627045248
            },
            new ModelOption
            {
                Name = "Qwen3.5 4B (lighter)",
                Description = "Official Qwen3.5 4B, 4-bit. About twice as fast, good for PCs with 8 GB of RAM or no GPU.",
                FileName = "Qwen3.5-4B-Q4_K_M.gguf",
                Url = "https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/resolve/e87f176479d0855a907a41277aca2f8ee7a09523/Qwen3.5-4B-Q4_K_M.gguf",
                Sha256 = "00fe7986ff5f6b463e62455821146049db6f9313603938a70800d1fb69ef11a4",
                Size = 2740937888
            }
        };

        readonly ModelOption[] options;
        readonly string modelsDir;
        readonly RadioButton[] radios;
        readonly ProgressBar bar = new ProgressBar { Width = 480, Height = 18, Maximum = 1000, Margin = new Padding(3, 10, 3, 3) };
        readonly Label lblStatus = new Label { AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
        readonly Button btnDownload = Ui.MakeButton("&Download", DialogResult.None);
        readonly Button btnCancel = Ui.MakeButton("Cancel", DialogResult.None);
        volatile bool cancel;
        volatile HttpWebRequest request;
        Thread worker;

        /// <summary>The downloaded file (for a bundle, its first file).</summary>
        public string DownloadedPath { get; private set; }
        /// <summary>The option that was downloaded.</summary>
        public ModelOption Downloaded { get; private set; }

        static string ModelsDir { get { return Path.Combine(Settings.AppDir, "models"); } }

        /// <summary>The language-model catalog (models\).</summary>
        public ModelDownloadDialog(bool firstRun)
            : this("Download a Model", (firstRun ? "QwenPad needs a language model for its AI features. " : "") +
                   "Pick one to download from Hugging Face. This is a one-time download; afterwards everything runs offline on this PC.",
                   Options, ModelsDir, null) { }

        /// <summary>Any catalog, into the given folder, with an optional note (e.g. a license) under the choices.</summary>
        public ModelDownloadDialog(string title, string intro, ModelOption[] options, string dir, string note)
        {
            this.options = options;
            modelsDir = dir;
            radios = new RadioButton[options.Length];
            Ui.MakeDialog(this, title);
            var t = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            t.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(500, 0), Margin = new Padding(3, 3, 3, 10), Text = intro });
            for (int i = 0; i < options.Length; i++)
            {
                var o = options[i];
                bool installed = o.InstalledIn(modelsDir);
                radios[i] = new RadioButton
                {
                    AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 6, 3, 0),
                    Text = string.Format("{0}  ·  {1:0.0} GB{2}", o.Name, o.TotalSize / 1073741824.0, installed ? "  ·  installed" : ""),
                    Enabled = !installed
                };
                t.Controls.Add(radios[i]);
                t.Controls.Add(new Label { Text = o.Description, AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(22, 0, 3, 2) });
            }
            foreach (var r in radios) if (r.Enabled) { r.Checked = true; break; }
            if (!string.IsNullOrEmpty(note))
                t.Controls.Add(new Label { Text = note, AutoSize = true, MaximumSize = new Size(500, 0), Margin = new Padding(3, 10, 3, 0), ForeColor = Theme.Dark ? Color.FromArgb(240, 200, 120) : Color.FromArgb(150, 90, 0) });
            t.Controls.Add(bar);
            t.Controls.Add(lblStatus);
            t.Controls.Add(Ui.ButtonRow(btnDownload, btnCancel));
            Controls.Add(t);
            AcceptButton = btnDownload;
            CancelButton = btnCancel;
            btnDownload.Enabled = Array.Exists(radios, r => r.Enabled);
            lblStatus.Text = btnDownload.Enabled ? "Downloads resume if interrupted. Files are verified with SHA-256." : "All models are already installed.";

            btnDownload.Click += delegate { StartDownload(); };
            btnCancel.Click += delegate { Close(); };
        }

        void StartDownload()
        {
            int idx = Array.FindIndex(radios, r => r.Checked);
            if (idx < 0) return;
            var opt = options[idx];
            long need = 0;
            foreach (var f in opt.Files) if (!File.Exists(Path.Combine(modelsDir, f.FileName))) need += f.Size;
            try
            {
                Directory.CreateDirectory(modelsDir);
                long free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(modelsDir))).AvailableFreeSpace;
                if (free < need + 256L * 1024 * 1024)
                {
                    MessageBox.Show(this, string.Format("Not enough disk space: {0:0.0} GB needed, {1:0.0} GB free.", (need + 268435456) / 1073741824.0, free / 1073741824.0),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch { }

            foreach (var r in radios) r.Enabled = false;
            btnDownload.Enabled = false;
            cancel = false;
            worker = new Thread(() => Download(opt)) { IsBackground = true };
            worker.Start();
        }

        /// <summary>Downloads each missing file of the option (resuming .part files), verifying every one.</summary>
        void Download(ModelOption opt)
        {
            string error = null;
            var files = opt.Files;
            long total = opt.TotalSize, before = 0;   // bytes of the bundle accounted for by earlier files
            try
            {
                for (int i = 0; i < files.Length && !cancel; i++)
                {
                    var f = files[i];
                    string final = Path.Combine(modelsDir, f.FileName), part = final + ".part";
                    string label = files.Length > 1 ? string.Format("{0} (file {1} of {2})", opt.Name, i + 1, files.Length) : opt.Name;
                    if (File.Exists(final)) { before += f.Size; continue; }   // already downloaded and verified
                    DownloadFile(f, part, label, before, total);
                    if (cancel) return;
                    if (new FileInfo(part).Length != f.Size) throw new IOException("The download is incomplete. Click Download to resume.");
                    Ui_(() => Progress(0, 1, "Verifying file integrity (SHA-256)…"));
                    if (!string.Equals(Sha256Of(part, f.Size), f.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(part);
                        throw new IOException(f.FileName + " failed its integrity check and was deleted. Please try again.");
                    }
                    if (File.Exists(final)) File.Delete(final);
                    File.Move(part, final);
                    before += f.Size;
                }
                if (!cancel)
                {
                    Downloaded = opt;
                    DownloadedPath = Path.Combine(modelsDir, files[0].FileName);
                }
            }
            catch (Exception ex)
            {
                if (!cancel) error = ex.Message;
            }
            finally { request = null; }

            Ui_(() =>
            {
                if (DownloadedPath != null) { DialogResult = DialogResult.OK; Close(); return; }
                if (cancel) return;
                lblStatus.Text = "⚠ " + error;
                btnDownload.Enabled = true;
                btnDownload.Text = "&Retry";
                for (int i = 0; i < radios.Length; i++) radios[i].Enabled = !options[i].InstalledIn(modelsDir);
            });
        }

        void DownloadFile(ModelFile f, string part, string label, long before, long total)
        {
            long have = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (have > f.Size) { File.Delete(part); have = 0; }
            if (have >= f.Size) return;
            var req = (HttpWebRequest)WebRequest.Create(f.Url);
            req.UserAgent = "QwenPad/" + Application.ProductVersion;
            req.Timeout = 60000;
            req.ReadWriteTimeout = 60000;
            if (have > 0) req.AddRange(have);
            request = req;
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                if (resp.StatusCode != HttpStatusCode.PartialContent) have = 0;   // server ignored the range: start over
                using (var src = resp.GetResponseStream())
                using (var dst = new FileStream(part, have > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                {
                    var buf = new byte[1 << 20];
                    long done = have, startDone = have;
                    DateTime started = DateTime.UtcNow, lastUi = DateTime.MinValue;
                    int n;
                    while (!cancel && (n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        dst.Write(buf, 0, n);
                        done += n;
                        if ((DateTime.UtcNow - lastUi).TotalMilliseconds > 250)
                        {
                            lastUi = DateTime.UtcNow;
                            double secs = (lastUi - started).TotalSeconds, rate = secs > 0 ? (done - startDone) / secs : 0;
                            double eta = rate > 0 ? (total - before - done) / rate : 0;
                            long d = before + done;
                            Ui_(() => Progress(d, total, string.Format("Downloading {0}: {1:0.00} / {2:0.00} GB · {3:0.0} MB/s · {4}",
                                label, d / 1073741824.0, total / 1073741824.0, rate / 1048576.0, FormatEta(eta))));
                        }
                    }
                }
            }
        }
        string Sha256Of(string path, long size)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            {
                var buf = new byte[1 << 20];
                long done = 0;
                int n;
                DateTime lastUi = DateTime.MinValue;
                while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancel) throw new OperationCanceledException();
                    sha.TransformBlock(buf, 0, n, null, 0);
                    done += n;
                    if ((DateTime.UtcNow - lastUi).TotalMilliseconds > 250)
                    {
                        lastUi = DateTime.UtcNow;
                        long d = done;
                        Ui_(() => Progress(d, size, string.Format("Verifying file integrity (SHA-256)… {0:0}%", 100.0 * d / size)));
                    }
                }
                sha.TransformFinalBlock(buf, 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "");
            }
        }

        void Progress(long done, long total, string text)
        {
            bar.Value = (int)Math.Max(0, Math.Min(1000, total > 0 ? done * 1000 / total : 0));
            lblStatus.Text = text;
        }

        static string FormatEta(double secs)
        {
            if (secs <= 0 || double.IsInfinity(secs)) return "estimating…";
            var t = TimeSpan.FromSeconds(secs);
            return t.TotalHours >= 1 ? string.Format("{0}h {1:00}m left", (int)t.TotalHours, t.Minutes) : string.Format("{0}m {1:00}s left", t.Minutes, t.Seconds);
        }

        void Ui_(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (worker != null && worker.IsAlive && DownloadedPath == null)
            {
                if (MessageBox.Show(this, "Stop the download? You can resume it later from Qwen > Model > Download a Model.",
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { e.Cancel = true; return; }
                cancel = true;
                var r = request;
                if (r != null) try { r.Abort(); } catch { }
            }
            base.OnFormClosing(e);
        }
    }
}
