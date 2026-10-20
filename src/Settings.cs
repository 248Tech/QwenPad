using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace QwenPad
{
    /// <summary>Persistent settings, stored as key=value lines in QwenPad.ini next to the exe.</summary>
    sealed class Settings
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Local model
        public string ServerUrl = "http://127.0.0.1:8765";
        public string ServerExe = @"runtime\llama\llama-server.exe";
        public string ModelPath = @"models\Qwen3.5-9B-Q4_K_M.gguf";
        public string ServerArgs = "-c 16384 --reasoning-budget 0";
        public bool AutoStartServer = true;
        public int MaxTokens = 2048;
        public double Temperature = 0.7;

        // Editor
        public string FontName = "Consolas";
        public float FontSize = 11f;
        public FontStyle FontStyle = FontStyle.Regular;
        public bool WordWrap = true;
        public bool StatusBar = true;
        public string Theme = "system";   // system | light | dark
        public Rectangle Bounds = Rectangle.Empty;
        public bool Maximized;

        public static string AppDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string FilePath { get { return Path.Combine(AppDir, "QwenPad.ini"); } }

        /// <summary>Resolves a path that may be relative to the exe directory.</summary>
        public static string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppDir, path));
        }

        /// <summary>All language-model .gguf files in models\ (vision projectors are skipped).</summary>
        public static string[] FindModels()
        {
            try
            {
                string dir = Path.Combine(AppDir, "models");
                if (!Directory.Exists(dir)) return new string[0];
                var list = new System.Collections.Generic.List<string>();
                foreach (string f in Directory.GetFiles(dir, "*.gguf"))
                    if (Path.GetFileName(f).IndexOf("mmproj", StringComparison.OrdinalIgnoreCase) < 0) list.Add(f);
                list.Sort(StringComparer.OrdinalIgnoreCase);
                return list.ToArray();
            }
            catch { return new string[0]; }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    int n; double d; float f;
                    switch (k)
                    {
                        case "ServerUrl": s.ServerUrl = v; break;
                        case "ServerExe": s.ServerExe = v; break;
                        case "ModelPath": s.ModelPath = v; break;
                        case "ServerArgs": s.ServerArgs = v; break;
                        case "AutoStartServer": s.AutoStartServer = v == "1" || v.ToLowerInvariant() == "true"; break;
                        case "MaxTokens": if (int.TryParse(v, NumberStyles.Integer, Inv, out n) && n > 0) s.MaxTokens = n; break;
                        case "Temperature": if (double.TryParse(v, NumberStyles.Float, Inv, out d)) s.Temperature = d; break;
                        case "FontName": s.FontName = v; break;
                        case "FontSize": if (float.TryParse(v, NumberStyles.Float, Inv, out f) && f > 1) s.FontSize = f; break;
                        case "FontStyle": if (int.TryParse(v, NumberStyles.Integer, Inv, out n)) s.FontStyle = (FontStyle)n; break;
                        case "WordWrap": s.WordWrap = v == "1"; break;
                        case "StatusBar": s.StatusBar = v == "1"; break;
                        case "Theme": s.Theme = v.ToLowerInvariant(); break;
                        case "Maximized": s.Maximized = v == "1"; break;
                        case "Window":
                            string[] p = v.Split(',');
                            int x, y, w, h;
                            if (p.Length == 4 && int.TryParse(p[0], out x) && int.TryParse(p[1], out y) &&
                                int.TryParse(p[2], out w) && int.TryParse(p[3], out h))
                                s.Bounds = new Rectangle(x, y, w, h);
                            break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; QwenPad settings. Relative paths are resolved from the QwenPad folder.");
            sb.AppendLine("[Model]");
            sb.AppendLine("ServerUrl=" + ServerUrl);
            sb.AppendLine("ServerExe=" + ServerExe);
            sb.AppendLine("ModelPath=" + ModelPath);
            sb.AppendLine("ServerArgs=" + ServerArgs);
            sb.AppendLine("AutoStartServer=" + (AutoStartServer ? "1" : "0"));
            sb.AppendLine("MaxTokens=" + MaxTokens.ToString(Inv));
            sb.AppendLine("Temperature=" + Temperature.ToString(Inv));
            sb.AppendLine("[Editor]");
            sb.AppendLine("FontName=" + FontName);
            sb.AppendLine("FontSize=" + FontSize.ToString(Inv));
            sb.AppendLine("FontStyle=" + ((int)FontStyle).ToString(Inv));
            sb.AppendLine("WordWrap=" + (WordWrap ? "1" : "0"));
            sb.AppendLine("StatusBar=" + (StatusBar ? "1" : "0"));
            sb.AppendLine("Theme=" + Theme);
            sb.AppendLine("Maximized=" + (Maximized ? "1" : "0"));
            if (!Bounds.IsEmpty)
                sb.AppendLine(string.Format(Inv, "Window={0},{1},{2},{3}", Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height));
            try { File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false)); } catch { }
        }

        public bool BoundsVisible()
        {
            if (Bounds.Width < 200 || Bounds.Height < 150) return false;
            foreach (Screen sc in Screen.AllScreens)
                if (sc.WorkingArea.IntersectsWith(Bounds)) return true;
            return false;
        }
    }
}
