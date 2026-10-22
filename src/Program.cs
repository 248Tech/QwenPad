using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace QwenPad
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        /// <summary>Started with --notes (e.g. at Windows sign-in): open the sticky notes only, with QwenPad in the tray.</summary>
        public const string NotesSwitch = "--notes";

        [STAThread]
        static void Main(string[] args)
        {
            bool notesOnly = args.Length > 0 && args[0] == NotesSwitch;
            string file = !notesOnly && args.Length > 0 ? args[0] : null;
            // When this folder's QwenPad is already running in the tray (keeping notes open), bring it back instead
            // of starting a second copy that couldn't open the notes.
            if (WakeBackgroundInstance(notesOnly, file)) return;

            try { SetProcessDPIAware(); } catch { }
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2 for Hugging Face downloads
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(file, notesOnly));
        }

        /// <summary>Per-folder name for the "come back from the tray" signal.</summary>
        public static string WakeName { get { return @"Local\QwenPad.Wake." + FolderTag(); } }

        /// <summary>Where a woken instance finds the file it should open (empty: just show the window).</summary>
        public static string WakeFile { get { return Path.Combine(Path.GetTempPath(), "QwenPad.wake." + FolderTag() + ".txt"); } }

        static bool WakeBackgroundInstance(bool notesOnly, string file)
        {
            try
            {
                using (var wake = EventWaitHandle.OpenExisting(WakeName))
                {
                    if (notesOnly) return true;   // the notes are already open
                    File.WriteAllText(WakeFile, file != null ? Path.GetFullPath(file) : "", Encoding.UTF8);
                    wake.Set();
                    return true;
                }
            }
            catch { return false; }
        }

        static string FolderTag()
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(Settings.AppDir.TrimEnd('\\').ToLowerInvariant()));
                return BitConverter.ToString(h, 0, 8).Replace("-", "");
            }
        }
    }
}
