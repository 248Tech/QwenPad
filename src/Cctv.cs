using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace QwenPad
{
    /// <summary>
    /// CCTV notes are backed by RTSPanda (https://github.com/248Tech/RTSPanda): QwenPad runs it as a hidden child process
    /// in viewer mode, registers each note's camera through its REST API, and RTSPanda's mediamtx relay then serves the
    /// stream on rtsp://127.0.0.1:8554/camera-ID. Several notes (or the RTSPanda dashboard) can watch one camera while the
    /// camera itself only sees a single connection. If RTSPanda isn't installed, notes connect to the camera directly.
    /// </summary>
    static class CctvHost
    {
        const int RelayRtspPort = 8554;   // mediamtx's default; RTSPanda doesn't change it
        static readonly object gate = new object();
        static readonly ServerManager server = new ServerManager("cctv.log", "RTSPanda");
        static readonly JavaScriptSerializer json = new JavaScriptSerializer();
        static string token;
        static int port;

        /// <summary>Folder for RTSPanda's database, recordings and auth token (portable, next to the exe).</summary>
        public static string DataDir { get { return Path.Combine(Settings.AppDir, "cctv"); } }
        public static string LogPath { get { return server.LogPath; } }
        public static bool Running { get { return server.Running; } }

        public static bool Installed(Settings s) { return File.Exists(Settings.Resolve(s.CctvExe)); }
        public static bool FfmpegInstalled(Settings s) { return File.Exists(Settings.Resolve(s.FfmpegExe)); }

        public static string DashboardUrl(Settings s) { return "http://127.0.0.1:" + s.CctvPort + "/"; }

        /// <summary>The bearer token RTSPanda was started with; also what its dashboard's login page asks for.</summary>
        public static string Token()
        {
            lock (gate)
            {
                if (token != null) return token;
                string path = Path.Combine(DataDir, "token.txt");
                try { if (File.Exists(path)) token = File.ReadAllText(path).Trim(); } catch { }
                if (string.IsNullOrEmpty(token))
                {
                    var bytes = new byte[24];
                    using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
                    token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                    try { Directory.CreateDirectory(DataDir); File.WriteAllText(path, token); } catch { }
                }
                return token;
            }
        }

        /// <summary>
        /// Starts RTSPanda if needed and waits until its API and relay answer. Blocking: call from a worker thread.
        /// Returns null when ready, otherwise a short reason (the caller can fall back to a direct connection).
        /// </summary>
        public static string EnsureRunning(Settings s)
        {
            lock (gate)
            {
                if (!Installed(s)) return "RTSPanda is not installed (runtime\\rtspanda).";
                if (server.Running && port == s.CctvPort && Ready(s)) return null;
                if (!server.Running || port != s.CctvPort)
                {
                    server.Stop();
                    port = s.CctvPort;
                    string exe = Settings.Resolve(s.CctvExe);
                    string dir = Path.GetDirectoryName(exe);
                    string data = Path.Combine(DataDir, "data");
                    try { Directory.CreateDirectory(data); } catch { }
                    string mtx = Path.Combine(dir, "mediamtx", "mediamtx.exe");
                    var env = new Dictionary<string, string>
                    {
                        { "DATA_DIR", data },
                        { "PORT", s.CctvPort.ToString() },
                        { "RTSPANDA_MODE", "viewer" },                   // live view + relay only: no AI workers
                        { "AUTH_ENABLED", "true" },
                        { "AUTH_TOKEN", Token() },
                        { "AUTH_COOKIE_SECURE", "false" },
                        { "MEDIAMTX_SOURCE_ON_DEMAND", "true" },         // pull from a camera only while someone is watching
                        { "FFMPEG_BIN", Settings.Resolve(s.FfmpegExe) }
                    };
                    if (File.Exists(mtx)) env["MEDIAMTX_BIN"] = mtx;
                    string err = server.StartProcess(exe, "", null, env);
                    if (err != null) return err;
                }
                var until = DateTime.UtcNow.AddSeconds(25);
                while (DateTime.UtcNow < until)
                {
                    if (!server.Running) return "RTSPanda exited at startup. See " + LogPath;
                    if (Ready(s)) return null;
                    Thread.Sleep(300);
                }
                return "RTSPanda did not become ready. See " + LogPath;
            }
        }

        static bool Ready(Settings s)
        {
            try
            {
                var d = Get(s, "/api/v1/health/ready") as Dictionary<string, object>;
                if (d == null) return false;
                var checks = d["checks"] as Dictionary<string, object>;
                var mtx = checks != null && checks.ContainsKey("mediamtx") ? checks["mediamtx"] as Dictionary<string, object> : null;
                return mtx != null && true.Equals(mtx["ok"]);
            }
            catch { return false; }
        }

        /// <summary>Finds the RTSPanda camera for this stream URL (by URL, so notes sharing a camera share one entry) or creates it.</summary>
        public static string EnsureCamera(Settings s, string name, string rtspUrl)
        {
            lock (gate)
            {
                var list = Get(s, "/api/v1/cameras") as object[] ?? new object[0];
                foreach (object o in list)
                {
                    var cam = o as Dictionary<string, object>;
                    if (cam != null && string.Equals(cam["rtsp_url"] as string, rtspUrl, StringComparison.Ordinal))
                    {
                        if (!true.Equals(cam["enabled"])) Send(s, "PUT", "/api/v1/cameras/" + cam["id"], json.Serialize(new Dictionary<string, object> { { "enabled", true } }));
                        return (string)cam["id"];
                    }
                }
                var made = Send(s, "POST", "/api/v1/cameras", json.Serialize(new Dictionary<string, object>
                {
                    { "name", string.IsNullOrEmpty(name) ? "QwenPad camera" : name }, { "rtsp_url", rtspUrl }
                })) as Dictionary<string, object>;
                if (made == null || !(made["id"] is string)) throw new InvalidOperationException("RTSPanda did not return a camera id.");
                return (string)made["id"];
            }
        }

        /// <summary>Where ffmpeg should read this camera from: RTSPanda's relay.</summary>
        public static string RelayUrl(string cameraId) { return "rtsp://127.0.0.1:" + RelayRtspPort + "/camera-" + cameraId; }

        static object Get(Settings s, string path) { return Send(s, "GET", path, null); }

        static object Send(Settings s, string method, string path, string body)
        {
            var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + s.CctvPort + path);
            req.Method = method;
            req.Timeout = req.ReadWriteTimeout = 8000;
            req.Proxy = null;
            req.Headers["Authorization"] = "Bearer " + Token();
            if (body != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentType = "application/json";
                req.ContentLength = data.Length;
                using (var rs = req.GetRequestStream()) rs.Write(data, 0, data.Length);
            }
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string text = sr.ReadToEnd();
                return text.Length == 0 ? null : json.DeserializeObject(text);
            }
        }

        public static void Shutdown() { lock (gate) { server.Stop(); } }
    }

    /// <summary>Turns a YouTube link into a direct stream URL with yt-dlp (these URLs expire, so it runs on every connect).</summary>
    static class YouTubeResolver
    {
        /// <summary>Returns the stream URL, or null with a short reason in error. live is true for a live broadcast.</summary>
        public static string Resolve(Settings s, string pageUrl, out bool live, out string error)
        {
            live = false; error = null;
            string exe = Settings.Resolve(s.YtDlpExe);
            if (!File.Exists(exe)) { error = "yt-dlp is missing (runtime\\ytdlp\\yt-dlp.exe)"; return null; }
            var psi = new ProcessStartInfo(exe, "--no-playlist --no-warnings -f \"best[height<=720]/bv*[height<=720]/best\" --print is_live --print urls \"" + pageUrl + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            try
            {
                using (var p = new Process { StartInfo = psi })
                {
                    var err = new StringBuilder();
                    p.ErrorDataReceived += delegate (object o, DataReceivedEventArgs e) { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
                    p.Start();
                    ServerManager.KillOnExit(p);
                    p.BeginErrorReadLine();
                    string outText = null;
                    var reader = new Thread(delegate () { try { outText = p.StandardOutput.ReadToEnd(); } catch { } });
                    reader.Start();
                    if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } error = "YouTube did not answer in time"; return null; }
                    reader.Join(2000);
                    string[] lines = (outText ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2 && lines[1].StartsWith("http"))
                    {
                        live = lines[0].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                        return lines[1].Trim();
                    }
                    string last = "";
                    lock (err) foreach (string l in err.ToString().Split('\n')) if (l.Trim().Length > 0) last = l.Trim();
                    error = last.Length > 0 ? last.Replace("ERROR: ", "") : "Could not open that YouTube video";
                    return null;
                }
            }
            catch (Exception ex) { error = "Could not run yt-dlp: " + ex.Message; return null; }
        }
    }

    /// <summary>Where a CCTV note's camera settings are stored: notes\&lt;id&gt;\camera.txt (name and URL, one per line).</summary>
    sealed class CctvCamera
    {
        public string Name = "", Url = "";

        public static CctvCamera Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var c = new CctvCamera();
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("name=")) c.Name = line.Substring(5);
                    else if (line.StartsWith("url=")) c.Url = line.Substring(4);
                }
                return c.Url.Length > 0 ? c : null;
            }
            catch { return null; }
        }

        public void Save(string path)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, "name=" + Name + "\r\nurl=" + Url + "\r\n", new UTF8Encoding(false)); } catch { }
        }

        /// <summary>The URL without its user:password, for display.</summary>
        public static string Redact(string url)
        {
            try
            {
                var u = new Uri(url);
                return u.UserInfo.Length == 0 ? url : url.Replace(u.UserInfo + "@", "");
            }
            catch { return url; }
        }

        public static bool IsValidUrl(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Host.Length == 0) return false;
            return u.Scheme == "rtsp" || u.Scheme == "rtsps" || IsYouTube(url);
        }

        /// <summary>youtube.com / youtu.be watch, live and shorts links.</summary>
        public static bool IsYouTube(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https")) return false;
            string h = u.Host.ToLowerInvariant();
            return h == "youtu.be" || h == "youtube.com" || h.EndsWith(".youtube.com");
        }
    }

    /// <summary>
    /// The live picture of a CCTV note. A background thread runs ffmpeg (RTSP in, a stream of small JPEGs out), cuts the
    /// JPEGs apart and shows the latest one; if the stream drops it reconnects with a growing delay. It only runs while
    /// the view is wanted (note visible and not minimized), so hidden notes don't use the camera or the CPU.
    /// </summary>
    class CctvView : Control
    {
        readonly object frameLock = new object();
        Bitmap frame;
        string status = "";
        string cameraName = "";
        string url = "";
        volatile bool wanted;
        Thread worker;
        Process ffmpeg;
        int generation;   // bumped on every (re)start so a stale worker exits
        DateTime lastFrame = DateTime.MinValue;
        string lastError = "";
        volatile string badge = "LIVE";
        bool Snapshot0() { lock (frameLock) return frame == null; }
        Settings settings;
        readonly System.Windows.Forms.Timer staleTimer = new System.Windows.Forms.Timer { Interval = 1000 };

        public event EventHandler FrameArrived;
        public double Aspect { get; private set; }

        public CctvView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            // Repaint once a second so a frozen picture turns into "NO SIGNAL" even when no new frames arrive.
            staleTimer.Tick += delegate { if (wanted && frame != null) Invalidate(); };
            staleTimer.Start();
            BackColor = Color.Black;
            Aspect = 16.0 / 9.0;
            status = "No camera";
        }

        public void Configure(Settings s, string name, string rtspUrl)
        {
            settings = s;
            cameraName = name ?? "";
            badge = "LIVE";
            if (rtspUrl != url) { ClearFrame(); url = rtspUrl ?? ""; }
            Restart();
        }

        /// <summary>Whether the stream should be running (the note decides: visible, not minimized).</summary>
        public void SetWanted(bool on)
        {
            if (on == wanted) return;
            wanted = on;
            if (on) Restart(); else StopWorker();
        }

        public Bitmap Snapshot()
        {
            lock (frameLock) return frame == null ? null : new Bitmap(frame);
        }

        void Restart()
        {
            StopWorker();
            if (!wanted || url.Length == 0 || settings == null) { SetStatus(url.Length == 0 ? "No camera" : "Paused"); return; }
            int gen = Interlocked.Increment(ref generation);
            SetStatus("Connecting...");
            worker = new Thread(delegate () { Run(gen, settings, url, cameraName); }) { IsBackground = true, Name = "CCTV " + cameraName };
            worker.Start();
        }

        void StopWorker()
        {
            Interlocked.Increment(ref generation);
            KillFfmpeg();
            worker = null;
        }

        void KillFfmpeg()
        {
            Process p;
            lock (frameLock) { p = ffmpeg; ffmpeg = null; }
            if (p == null) return;
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        bool Current(int gen) { return gen == Thread.VolatileRead(ref generation); }

        void Run(int gen, Settings s, string rtsp, string name)
        {
            int delay = 2;
            while (Current(gen))
            {
                string source = rtsp, via = "direct";
                string err = null;
                bool youtube = CctvCamera.IsYouTube(rtsp), live = false;
                if (youtube)
                {
                    if (Snapshot0()) SetStatus("Opening YouTube...");
                    source = YouTubeResolver.Resolve(s, rtsp, out live, out err);
                    if (!Current(gen)) return;
                    if (source == null)
                    {
                        lastError = err ?? "";
                        SetStatus(lastError + "\nRetrying in " + delay + "s...");
                        for (int i = 0; i < delay * 10 && Current(gen); i++) Thread.Sleep(100);
                        delay = Math.Min(delay * 2, 60);
                        continue;
                    }
                    via = "youtube"; badge = live ? "LIVE" : "VIDEO";
                    err = null;
                }
                else if (CctvHost.Installed(s))
                {
                    err = CctvHost.EnsureRunning(s);
                    if (err == null)
                    {
                        try { source = CctvHost.RelayUrl(CctvHost.EnsureCamera(s, name, rtsp)); via = "RTSPanda"; }
                        catch (Exception ex) { err = "RTSPanda: " + ex.Message; }
                    }
                    if (err != null) lastError = err;   // fall back to a direct connection below
                }
                if (!Current(gen)) return;
                if (!CctvHost.FfmpegInstalled(s)) { SetStatus("ffmpeg is missing (runtime\\ffmpeg\\ffmpeg.exe)"); return; }

                bool gotFrames = RunFfmpeg(gen, s, source, via, youtube && !live);
                if (!Current(gen)) return;
                if (gotFrames) delay = 2;
                if (youtube && !live && gotFrames) { delay = 1; lastError = ""; }   // a finished video just starts over
                if (!(youtube && !live && gotFrames)) SetStatus((lastError.Length > 0 ? lastError + "\n" : "") + "Reconnecting in " + delay + "s...");
                for (int i = 0; i < delay * 10 && Current(gen); i++) Thread.Sleep(100);
                delay = Math.Min(delay * 2, 30);
            }
        }

        /// <summary>Runs one ffmpeg session until it ends; returns true if at least one frame arrived.</summary>
        bool RunFfmpeg(int gen, Settings s, string source, string via, bool realtime)
        {
            // Low-latency decode, no audio, N frames a second, scaled down so a 4K camera doesn't cost a core.
            // Recorded videos are read at their own speed (-re); live HLS and RTSP are already real time.
            string input = source.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase) ? "-fflags nobuffer -flags low_delay -rtsp_transport tcp" : (realtime ? "-re" : "");
            string args = "-hide_banner -loglevel error " + input + " -i \"" + source + "\" -an " +
                          "-vf \"fps=" + s.CctvFps + ",scale='min(1280,iw)':-2\" -f image2pipe -c:v mjpeg -q:v 5 pipe:1";
            var psi = new ProcessStartInfo(Settings.Resolve(s.FfmpegExe), args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            var p = new Process { StartInfo = psi };
            var errText = new StringBuilder();
            p.ErrorDataReceived += delegate (object o, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (errText) { errText.AppendLine(e.Data); if (errText.Length > 2000) errText.Remove(0, errText.Length - 2000); }
            };
            try { p.Start(); }
            catch (Exception ex) { lastError = "Could not start ffmpeg: " + ex.Message; return false; }
            ServerManager.KillOnExit(p);
            p.BeginErrorReadLine();
            lock (frameLock) ffmpeg = p;
            if (!Current(gen)) { KillFfmpeg(); return false; }

            bool got = false;
            var buf = new byte[1 << 20];
            int len = 0;
            var chunk = new byte[65536];
            try
            {
                var stream = p.StandardOutput.BaseStream;
                int n;
                while (Current(gen) && (n = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (len + n > buf.Length) { if (len + n > 16 << 20) len = 0; Array.Resize(ref buf, Math.Max(buf.Length * 2, len + n)); }
                    Buffer.BlockCopy(chunk, 0, buf, len, n);
                    len += n;
                    // Cut complete JPEGs (FFD8 ... FFD9) out of the stream; 0xFF is always escaped inside the data.
                    int start = -1, pos = 0, lastEnd = 0;
                    while (pos < len - 1)
                    {
                        if (buf[pos] == 0xFF && buf[pos + 1] == 0xD8 && start < 0) { start = pos; pos += 2; continue; }
                        if (buf[pos] == 0xFF && buf[pos + 1] == 0xD9 && start >= 0)
                        {
                            Show(buf, start, pos + 2 - start);
                            got = true; lastError = "";
                            lastEnd = pos + 2; start = -1; pos += 2; continue;
                        }
                        pos++;
                    }
                    int keep = start >= 0 ? start : Math.Max(lastEnd, len - 1);
                    if (keep > 0) { Buffer.BlockCopy(buf, keep, buf, 0, len - keep); len -= keep; }
                }
            }
            catch { }
            lock (frameLock) { if (ffmpeg == p) ffmpeg = null; }
            try { if (!p.HasExited) p.Kill(); } catch { }
            try { p.WaitForExit(1500); } catch { }
            if (!got && Current(gen))
            {
                string tail;
                lock (errText) tail = LastLine(errText.ToString());
                lastError = tail.Length > 0 ? tail : "No picture from the camera";
                if (via == "direct" && CctvHost.Installed(s)) lastError += "\n(RTSPanda unavailable; connected directly)";
            }
            try { p.Dispose(); } catch { }
            return got;
        }

        static string LastLine(string text)
        {
            string last = "";
            foreach (string l in text.Split('\n')) if (l.Trim().Length > 0) last = l.Trim();
            // Never show credentials from the camera URL in the note.
            return System.Text.RegularExpressions.Regex.Replace(last, @"(rtsps?://)[^@/\s]+@", "$1");
        }

        void Show(byte[] data, int offset, int count)
        {
            Bitmap bmp;
            try
            {
                using (var ms = new MemoryStream(data, offset, count))
                using (var img = Image.FromStream(ms)) bmp = new Bitmap(img);
            }
            catch { return; }
            Bitmap old;
            lock (frameLock) { old = frame; frame = bmp; lastFrame = DateTime.UtcNow; status = ""; }
            if (old != null) old.Dispose();
            if (bmp.Height > 0) Aspect = (double)bmp.Width / bmp.Height;
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)delegate { Invalidate(); var h = FrameArrived; if (h != null) h(this, EventArgs.Empty); }); }
            catch { }
        }

        void SetStatus(string text)
        {
            lock (frameLock) status = text;
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)delegate { Invalidate(); }); } catch { }
        }

        void ClearFrame()
        {
            Bitmap old;
            lock (frameLock) { old = frame; frame = null; }
            if (old != null) old.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Black);
            string st;
            bool have;
            lock (frameLock)   // the worker disposes the previous frame when it swaps in a new one, so draw while holding the lock
            {
                st = status;
                have = frame != null;
                if (have)
                {
                    double ar = (double)frame.Width / frame.Height;
                    int w = ClientSize.Width, h = (int)(w / ar);
                    if (h > ClientSize.Height) { h = ClientSize.Height; w = (int)(h * ar); }
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(frame, (ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
                }
            }
            if (have)
            {
                // A tiny LIVE badge while frames are fresh; a stale picture is dimmed and labelled instead.
                bool stale = (DateTime.UtcNow - lastFrame).TotalSeconds > 4;
                if (stale) using (var b = new SolidBrush(Color.FromArgb(140, 0, 0, 0))) g.FillRectangle(b, ClientRectangle);
                using (var b = new SolidBrush(stale ? Color.Gray : Color.FromArgb(235, 60, 60))) g.FillEllipse(b, 8, 8, 8, 8);
                TextRenderer.DrawText(g, stale ? "NO SIGNAL" : badge, Font, new Point(20, 4), Color.White, TextFormatFlags.NoPadding);
            }
            if (!have || st.Length > 0)
            {
                var r = new Rectangle(8, 24, Math.Max(10, ClientSize.Width - 16), Math.Max(10, ClientSize.Height - 32));
                TextRenderer.DrawText(g, st.Length > 0 ? st : "Connecting...", Font, r, Color.FromArgb(210, 210, 210),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                wanted = false;
                staleTimer.Dispose();
                StopWorker();
                Bitmap old;
                lock (frameLock) { old = frame; frame = null; }
                if (old != null) old.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>A CCTV view whose edges pass clicks through so the note's resize border keeps working over the picture.</summary>
    sealed class EdgeCctvView : CctvView
    {
        protected override void WndProc(ref Message m) { if (!StickyNote.PassNoteMessage(this, ref m)) base.WndProc(ref m); }
    }

    /// <summary>Asks for a camera's name and RTSP address.</summary>
    sealed class CctvDialog : Form
    {
        readonly TextBox txtName = new TextBox(), txtUrl = new TextBox();

        public string CameraName { get { return txtName.Text.Trim(); } }
        public string CameraUrl { get { return txtUrl.Text.Trim(); } }

        public CctvDialog(string name, string url)
        {
            Ui.MakeDialog(this, "CCTV Camera");
            var table = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            table.Controls.Add(new Label { Text = "Name", AutoSize = true, Margin = new Padding(3, 3, 3, 3) });
            txtName.Text = name ?? "";
            txtName.Width = 420;
            table.Controls.Add(txtName);
            table.Controls.Add(new Label { Text = "Stream address (RTSP camera or YouTube video / live stream)", AutoSize = true, Margin = new Padding(3, 10, 3, 3) });
            txtUrl.Text = url ?? "";
            txtUrl.Width = 420;
            table.Controls.Add(txtUrl);
            table.Controls.Add(new Label
            {
                Text = "Examples: rtsp://user:password@192.168.1.20:554/stream1  or  https://www.youtube.com/watch?v=...\r\nThe address is stored in this note's folder, so only share notes with people you'd trust with the camera.",
                AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 6, 3, 0)
            });
            var ok = Ui.MakeButton("OK", DialogResult.None);
            var cancel = Ui.MakeButton("Cancel", DialogResult.Cancel);
            ok.Click += delegate
            {
                string u = CameraUrl;
                if (!CctvCamera.IsValidUrl(u))
                {
                    MessageBox.Show(this, "Enter an rtsp:// address or a YouTube link.", "CCTV Camera", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtUrl.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            table.Controls.Add(Ui.ButtonRow(ok, cancel));
            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate { (txtUrl.Text.Length == 0 && txtName.Text.Length > 0 ? txtUrl : txtName.Text.Length == 0 ? txtName : txtUrl).Focus(); };
        }
    }
}
