using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QwenPad
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2 for Hugging Face downloads
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
        }
    }
}
