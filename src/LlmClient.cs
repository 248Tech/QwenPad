using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace QwenPad
{
    /// <summary>Minimal streaming client for an OpenAI-compatible /v1/chat/completions endpoint (llama-server).</summary>
    sealed class LlmClient
    {
        readonly Settings settings;
        volatile HttpWebRequest current;

        public LlmClient(Settings settings) { this.settings = settings; }

        string BaseUrl { get { return settings.ServerUrl.TrimEnd('/'); } }

        /// <summary>Returns "ready", "loading", or "offline". Blocking; call off the UI thread.</summary>
        public string CheckHealth()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(BaseUrl + "/health");
                req.Timeout = 1500;
                req.Proxy = null;
                req.KeepAlive = false;
                using (req.GetResponse()) return "ready";
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp == null) return "offline";
                int code = (int)resp.StatusCode;
                resp.Close();
                return code == 503 ? "loading" : "offline";
            }
            catch { return "offline"; }
        }

        /// <summary>Streams a chat completion, calling onDelta with each piece of content. Blocking.</summary>
        public void Stream(string systemPrompt, string userPrompt, Action<string> onDelta, Func<bool> cancelled)
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var body = new Dictionary<string, object>
            {
                { "model", "qwen" },
                { "messages", new object[]
                    {
                        new Dictionary<string, object> { { "role", "system" }, { "content", systemPrompt } },
                        new Dictionary<string, object> { { "role", "user" }, { "content", userPrompt } }
                    }
                },
                { "stream", true },
                { "max_tokens", settings.MaxTokens },
                // Qwen3.5's recommended sampling for non-thinking mode
                { "temperature", settings.Temperature },
                { "top_p", 0.8 },
                { "top_k", 20 },
                { "min_p", 0.0 },
                { "presence_penalty", 1.5 },
                // An editor wants answers, not chain-of-thought
                { "chat_template_kwargs", new Dictionary<string, object> { { "enable_thinking", false } } }
            };
            byte[] payload = Encoding.UTF8.GetBytes(ser.Serialize(body));

            var req = (HttpWebRequest)WebRequest.Create(BaseUrl + "/v1/chat/completions");
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "text/event-stream";
            req.Proxy = null;
            req.Timeout = 10 * 60 * 1000;
            req.ReadWriteTimeout = 5 * 60 * 1000;
            req.AllowReadStreamBuffering = false;
            current = req;
            try
            {
                using (Stream rs = req.GetRequestStream()) rs.Write(payload, 0, payload.Length);

                HttpWebResponse resp;
                try { resp = (HttpWebResponse)req.GetResponse(); }
                catch (WebException ex)
                {
                    if (ex.Response != null) throw new Exception(ReadError(ex.Response, ser));
                    throw;
                }

                using (resp)
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (cancelled()) break;
                        if (!line.StartsWith("data:")) continue;
                        string data = line.Substring(5).Trim();
                        if (data == "[DONE]") break;
                        var obj = ser.DeserializeObject(data) as Dictionary<string, object>;
                        if (obj == null) continue;
                        object err;
                        if (obj.TryGetValue("error", out err)) throw new Exception(ErrorMessage(err));
                        object choices;
                        if (!obj.TryGetValue("choices", out choices)) continue;
                        var arr = choices as object[];
                        if (arr == null || arr.Length == 0) continue;
                        var choice = arr[0] as Dictionary<string, object>;
                        object delta;
                        if (choice == null || !choice.TryGetValue("delta", out delta)) continue;
                        var d = delta as Dictionary<string, object>;
                        object content;
                        if (d != null && d.TryGetValue("content", out content))
                        {
                            string text = content as string;
                            if (!string.IsNullOrEmpty(text)) onDelta(text);
                        }
                    }
                }
            }
            finally
            {
                current = null;
            }
        }

        public void Abort()
        {
            var req = current;
            if (req != null) try { req.Abort(); } catch { }
        }

        internal static string ReadError(WebResponse response, JavaScriptSerializer ser)
        {
            try
            {
                using (response)
                using (var r = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string text = r.ReadToEnd();
                    var obj = ser.DeserializeObject(text) as Dictionary<string, object>;
                    object err;
                    if (obj != null && obj.TryGetValue("error", out err)) return ErrorMessage(err);
                    return text.Length > 0 ? text : "Server error";
                }
            }
            catch { return "Server error"; }
        }

        internal static string ErrorMessage(object err)
        {
            var d = err as Dictionary<string, object>;
            object msg;
            if (d != null && d.TryGetValue("message", out msg)) return Convert.ToString(msg);
            return Convert.ToString(err);
        }
    }
}
