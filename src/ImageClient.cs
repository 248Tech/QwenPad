using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace QwenPad
{
    /// <summary>One image request: text-to-image, or an edit when reference images are given.</summary>
    sealed class ImageJob
    {
        public string Prompt = "";
        public int Width = 1024, Height = 1024;
        public long Seed = -1;                       // -1: random
        public List<byte[]> References = new List<byte[]>();   // PNG/JPEG bytes, in order (edits)
    }

    /// <summary>Progress of a submitted job, from GET /sdcpp/v1/jobs/{id}.</summary>
    sealed class ImageJobState
    {
        public string Status = "";                   // queued | generating | completed | failed | cancelled
        public int Step, TotalSteps;
        public byte[] Preview;                       // latest preview frame (PNG), if any
        public List<byte[]> Images = new List<byte[]>();
        public long Seed = -1;
        public string Error;
        public bool Done { get { return Status == "completed" || Status == "failed" || Status == "cancelled"; } }
    }

    /// <summary>
    /// Client for stable-diffusion.cpp's sd-server native async API: submit a job, poll its status (with live
    /// preview frames), cancel it. Blocking calls; use them off the UI thread.
    /// </summary>
    sealed class ImageClient
    {
        // Qwen-Image-2.1 Turbo's fixed 8-step schedule (euler, CFG 1).
        static readonly double[] TurboSigmas = { 1.0, 0.978453, 0.95418, 0.926626, 0.89508, 0.845148, 0.704534, 0.414568, 0.0 };

        readonly Settings settings;
        readonly JavaScriptSerializer ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public ImageClient(Settings settings) { this.settings = settings; }

        string BaseUrl { get { return settings.ImageServerUrl.TrimEnd('/'); } }

        /// <summary>"ready", "loading" or "offline".</summary>
        public string CheckHealth()
        {
            try
            {
                var obj = Get("/sdcpp/v1/models/status", 1500);
                // Ready once a model context is loaded; while sd-server is still loading it, report "loading".
                string state = Str(obj, "state") ?? Str(obj, "status") ?? "";
                if (state.IndexOf("load", StringComparison.OrdinalIgnoreCase) >= 0 && state.IndexOf("loaded", StringComparison.OrdinalIgnoreCase) < 0) return "loading";
                if (state.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("unloaded", StringComparison.OrdinalIgnoreCase) >= 0) return "loading";
                return "ready";
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp == null) return "offline";
                resp.Close();
                return "loading";
            }
            catch { return "offline"; }
        }

        /// <summary>Submits a job; returns its id.</summary>
        public string Submit(ImageJob job)
        {
            var refs = new List<object>();
            foreach (var r in job.References) refs.Add("data:image/png;base64," + Convert.ToBase64String(r));
            var sigmas = new List<object>();
            foreach (double s in TurboSigmas) sigmas.Add(s);
            var body = new Dictionary<string, object>
            {
                { "prompt", job.Prompt },
                { "negative_prompt", "" },
                { "width", job.Width },
                { "height", job.Height },
                { "seed", job.Seed },
                { "batch_count", 1 },
                { "sample_params", new Dictionary<string, object>
                    {
                        { "sample_steps", TurboSigmas.Length - 1 },
                        { "sample_method", "euler" },
                        { "custom_sigmas", sigmas },
                        { "guidance", new Dictionary<string, object> { { "txt_cfg", 1.0 } } }
                    }
                },
                { "ref_images", refs },
                { "init_image", null },
                { "mask_image", null },
                { "preview", "proj" },
                { "output_format", "png" }
            };
            var obj = Post("/sdcpp/v1/img_gen", body, 60000);
            string id = Str(obj, "id") ?? Str(obj, "job_id");
            if (id == null)
            {
                string poll = Str(obj, "poll_url");
                if (poll != null) id = poll.Substring(poll.LastIndexOf('/') + 1);
            }
            if (id == null) throw new Exception("The image engine didn't return a job id.");
            return id;
        }

        public ImageJobState Poll(string id)
        {
            var obj = Get("/sdcpp/v1/jobs/" + Uri.EscapeDataString(id), 30000);
            var st = new ImageJobState { Status = Str(obj, "status") ?? "" };
            var preview = Dict(obj, "preview");
            if (preview != null)
            {
                st.Step = Int(preview, "step");
                st.TotalSteps = Int(preview, "total_steps");
                string b64 = Str(preview, "b64_json");
                if (!string.IsNullOrEmpty(b64)) st.Preview = FromBase64(b64);
            }
            var progress = Dict(obj, "progress");
            if (progress != null)
            {
                if (st.Step == 0) st.Step = Int(progress, "step");
                if (st.TotalSteps == 0) st.TotalSteps = Int(progress, "total_steps");
            }
            var result = Dict(obj, "result");
            if (result != null)
            {
                object images;
                if (result.TryGetValue("images", out images) && images is object[])
                    foreach (var im in (object[])images)
                    {
                        string b64 = Str(im as Dictionary<string, object>, "b64_json");
                        if (!string.IsNullOrEmpty(b64)) st.Images.Add(FromBase64(b64));
                    }
                object seed;
                if (result.TryGetValue("seed", out seed)) try { st.Seed = Convert.ToInt64(seed); } catch { }
            }
            object err;
            if (obj.TryGetValue("error", out err) && err != null) st.Error = LlmClient.ErrorMessage(err);
            return st;
        }

        public void Cancel(string id)
        {
            try { Post("/sdcpp/v1/jobs/" + Uri.EscapeDataString(id) + "/cancel", new Dictionary<string, object>(), 5000); } catch { }
        }

        static byte[] FromBase64(string s)
        {
            int comma = s.StartsWith("data:") ? s.IndexOf(',') : -1;
            return Convert.FromBase64String(comma >= 0 ? s.Substring(comma + 1) : s);
        }

        Dictionary<string, object> Get(string path, int timeout)
        {
            var req = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
            req.Timeout = timeout;
            req.ReadWriteTimeout = timeout;
            req.Proxy = null;
            req.KeepAlive = false;
            return Send(req, null);
        }

        Dictionary<string, object> Post(string path, Dictionary<string, object> body, int timeout)
        {
            var req = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Timeout = timeout;
            req.ReadWriteTimeout = timeout;
            req.Proxy = null;
            req.KeepAlive = false;
            return Send(req, Encoding.UTF8.GetBytes(ser.Serialize(body)));
        }

        Dictionary<string, object> Send(HttpWebRequest req, byte[] payload)
        {
            if (payload != null) using (Stream rs = req.GetRequestStream()) rs.Write(payload, 0, payload.Length);
            HttpWebResponse resp;
            try { resp = (HttpWebResponse)req.GetResponse(); }
            catch (WebException ex)
            {
                if (ex.Response != null && payload != null) throw new Exception(LlmClient.ReadError(ex.Response, ser));
                throw;
            }
            using (resp)
            using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string text = r.ReadToEnd();
                return (text.Length > 0 ? ser.DeserializeObject(text) as Dictionary<string, object> : null) ?? new Dictionary<string, object>();
            }
        }

        static Dictionary<string, object> Dict(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
        }

        static int Int(Dictionary<string, object> d, string key)
        {
            object v;
            try { return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToInt32(v) : 0; } catch { return 0; }
        }
    }
}
