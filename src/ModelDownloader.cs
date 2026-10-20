using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace QwenPad
{
    sealed class ModelOption
    {
        public string Name, Description, FileName, Url, Sha256;
        public long Size;
    }

    /// <summary>
    /// One-time model download from Hugging Face. URLs are pinned to a commit and every file is
    /// checked against its published SHA-256. Interrupted downloads resume from the .part file.
    /// </summary>
    sealed class ModelDownloadDialog : Form
    {
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

        readonly RadioButton[] radios = new RadioButton[Options.Length];
        readonly ProgressBar bar = new ProgressBar { Width = 480, Height = 18, Maximum = 1000, Margin = new Padding(3, 10, 3, 3) };
        readonly Label lblStatus = new Label { AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
        readonly Button btnDownload = Ui.MakeButton("&Download", DialogResult.None);
        readonly Button btnCancel = Ui.MakeButton("Cancel", DialogResult.None);
        volatile bool cancel;
        volatile HttpWebRequest request;
        Thread worker;

        public string DownloadedPath { get; private set; }

        static string ModelsDir { get { return Path.Combine(Settings.AppDir, "models"); } }

        public ModelDownloadDialog(bool firstRun)
        {
            Ui.MakeDialog(this, "Download a Model");
            var t = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            t.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Size(500, 0), Margin = new Padding(3, 3, 3, 10),
                Text = (firstRun ? "QwenPad needs a language model for its AI features. " : "") +
                       "Pick one to download from Hugging Face. This is a one-time download; afterwards everything runs offline on this PC."
            });
            for (int i = 0; i < Options.Length; i++)
            {
                var o = Options[i];
                bool installed = File.Exists(Path.Combine(ModelsDir, o.FileName));
                radios[i] = new RadioButton
                {
                    AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 6, 3, 0),
                    Text = string.Format("{0}  ·  {1:0.0} GB{2}", o.Name, o.Size / 1073741824.0, installed ? "  ·  installed" : ""),
                    Enabled = !installed
                };
                t.Controls.Add(radios[i]);
                t.Controls.Add(new Label { Text = o.Description, AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(22, 0, 3, 2) });
            }
            foreach (var r in radios) if (r.Enabled) { r.Checked = true; break; }
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
            var opt = Options[idx];
            try
            {
                Directory.CreateDirectory(ModelsDir);
                long free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(ModelsDir))).AvailableFreeSpace;
                if (free < opt.Size + 256L * 1024 * 1024)
                {
                    MessageBox.Show(this, string.Format("Not enough disk space: {0:0.0} GB needed, {1:0.0} GB free.", (opt.Size + 268435456) / 1073741824.0, free / 1073741824.0),
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

        void Download(ModelOption opt)
        {
            string final = Path.Combine(ModelsDir, opt.FileName), part = final + ".part";
            string error = null;
            try
            {
                long have = File.Exists(part) ? new FileInfo(part).Length : 0;
                if (have > opt.Size) { File.Delete(part); have = 0; }
                if (have < opt.Size)
                {
                    var req = (HttpWebRequest)WebRequest.Create(opt.Url);
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
                                    double eta = rate > 0 ? (opt.Size - done) / rate : 0;
                                    long d = done;
                                    Ui_(() => Progress(d, opt.Size, string.Format("Downloading {0}: {1:0.00} / {2:0.00} GB · {3:0.0} MB/s · {4}",
                                        opt.Name, d / 1073741824.0, opt.Size / 1073741824.0, rate / 1048576.0, FormatEta(eta))));
                                }
                            }
                        }
                    }
                    if (cancel) return;
                }

                if (new FileInfo(part).Length != opt.Size) throw new IOException("The download is incomplete. Click Download to resume.");
                Ui_(() => Progress(0, 1, "Verifying file integrity (SHA-256)…"));
                if (!string.Equals(Sha256Of(part, opt.Size), opt.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(part);
                    throw new IOException("The downloaded file failed its integrity check and was deleted. Please try again.");
                }
                if (File.Exists(final)) File.Delete(final);
                File.Move(part, final);
                DownloadedPath = final;
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
                for (int i = 0; i < radios.Length; i++) radios[i].Enabled = !File.Exists(Path.Combine(ModelsDir, Options[i].FileName));
            });
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
