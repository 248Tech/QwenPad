using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QwenPad
{
    /// <summary>
    /// Launches a local model server (llama-server for text, sd-server for images) as a hidden child process.
    /// The child is placed in a kill-on-close job object so the model is unloaded even if QwenPad crashes.
    /// </summary>
    sealed class ServerManager : IDisposable
    {
        Process proc;
        IntPtr job;
        StreamWriter log;
        readonly object logLock = new object();
        readonly string logName, displayName;
        string logPath;

        public ServerManager() : this("server.log", "llama-server") { }

        public ServerManager(string logName, string displayName)
        {
            this.logName = logName;
            this.displayName = displayName;
        }

        /// <summary>logs\&lt;logName&gt; in the app folder (portable); %TEMP% if the folder is read-only.</summary>
        public string LogPath
        {
            get
            {
                if (logPath != null) return logPath;
                try
                {
                    string dir = Path.Combine(Settings.AppDir, "logs");
                    Directory.CreateDirectory(dir);
                    string probe = Path.Combine(dir, ".write-test");
                    File.WriteAllText(probe, "");
                    File.Delete(probe);
                    logPath = Path.Combine(dir, logName);
                }
                catch { logPath = Path.Combine(Path.GetTempPath(), "QwenPad-" + logName); }
                return logPath;
            }
        }

        public bool Running { get { return proc != null && !proc.HasExited; } }
        public bool ExitedUnexpectedly { get; private set; }

        /// <summary>Starts llama-server from the settings; returns an error message, or null on success.</summary>
        public string Start(Settings s)
        {
            if (Running) return null;
            string exe = Settings.Resolve(s.ServerExe);
            if (!File.Exists(exe)) return "llama-server was not found at:\n" + exe;
            string model = Settings.Resolve(s.ModelPath);
            if (!File.Exists(model))
            {
                // Fall back to whatever model ships in models\ (e.g. a package built with a different one).
                string[] found = Settings.FindModels();
                if (found.Length == 0) return "The model file was not found at:\n" + model;
                model = found[0];
                s.ModelPath = model.Substring(Settings.AppDir.Length);
            }

            Uri uri;
            if (!Uri.TryCreate(s.ServerUrl, UriKind.Absolute, out uri)) return "Invalid server URL: " + s.ServerUrl;
            return StartProcess(exe, string.Format("-m \"{0}\" --host {1} --port {2} {3}", model, uri.Host, uri.Port, s.ServerArgs), null);
        }

        /// <summary>Starts any server exe hidden, logging its output. extraPath is prepended to its PATH (for shared DLLs).</summary>
        public string StartProcess(string exe, string args, string extraPath)
        {
            if (Running) return null;
            if (!File.Exists(exe)) return displayName + " was not found at:\n" + exe;
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            if (!string.IsNullOrEmpty(extraPath))
                psi.EnvironmentVariables["PATH"] = extraPath + ";" + Environment.GetEnvironmentVariable("PATH");

            CloseLog();
            try { log = new StreamWriter(new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8) { AutoFlush = true }; }
            catch { log = null; }

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            DataReceivedEventHandler onLine = (o, e) =>
            {
                if (e.Data == null) return;
                lock (logLock) { if (log != null) try { log.WriteLine(e.Data); } catch { } }
            };
            p.OutputDataReceived += onLine;
            p.ErrorDataReceived += onLine;
            p.Exited += (o, e) => { if (proc == p) ExitedUnexpectedly = true; };
            try { p.Start(); }
            catch (Exception ex) { return "Could not start " + displayName + ": " + ex.Message; }

            AddToJob(p);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            proc = p;
            ExitedUnexpectedly = false;
            return null;
        }

        public void Stop()
        {
            var p = proc;
            proc = null;
            if (p != null)
            {
                try { if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); } } catch { }
                p.Dispose();
            }
            ExitedUnexpectedly = false;
        }

        public void Dispose()
        {
            Stop();
            CloseLog();
            if (job != IntPtr.Zero) { CloseHandle(job); job = IntPtr.Zero; }
        }

        void CloseLog()
        {
            lock (logLock) { if (log != null) { try { log.Dispose(); } catch { } log = null; } }
        }

        void AddToJob(Process p)
        {
            try
            {
                if (job == IntPtr.Zero)
                {
                    job = CreateJobObject(IntPtr.Zero, null);
                    var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                    info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
                    int len = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr ptr = Marshal.AllocHGlobal(len);
                    try
                    {
                        Marshal.StructureToPtr(info, ptr, false);
                        SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)len);
                    }
                    finally { Marshal.FreeHGlobal(ptr); }
                }
                AssignProcessToJobObject(job, p.Handle);
            }
            catch { }
        }

        #region Win32
        const int JobObjectExtendedLimitInformation = 9;
        const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateJobObject(IntPtr attrs, string name);
        [DllImport("kernel32.dll")]
        static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);
        [DllImport("kernel32.dll")]
        static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);
        #endregion
    }
}
