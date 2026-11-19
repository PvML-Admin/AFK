using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace KavrosAfkWarden
{
    internal static class AppUpdates
    {
        internal static string RestartedVersion;
        private static int busy;

        internal static void Start()
        {
            VelopackApp.Build().OnRestarted(delegate(SemanticVersion version) { RestartedVersion = version == null ? "" : version.ToString(); }).Run();
        }

        internal static void Check(Form owner, bool interactive, Action<string> status)
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            {
                if (interactive && status != null) status("An update check is already running.");
                return;
            }
            if (interactive && status != null) status("Checking GitHub for updates...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { CheckWorker(owner, interactive, status); }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }

        private static void CheckWorker(Form owner, bool interactive, Action<string> status)
        {
            try
            {
                UpdateManager manager = new UpdateManager(new GithubSource(AppInfo.RepositoryUrl, null, false, null), null, null);
                if (!manager.IsInstalled)
                {
                    if (interactive) Tell(owner, status, "This copy cannot update itself. Download Setup.exe from GitHub Releases.");
                    return;
                }
                UpdateInfo info = manager.CheckForUpdates();
                if (info == null || info.TargetFullRelease == null)
                {
                    if (interactive)
                    {
                        string current = manager.CurrentVersion == null ? AppInfo.DisplayVersion : manager.CurrentVersion.ToString();
                        Tell(owner, status, "You are on the latest version (" + current + ").");
                    }
                    return;
                }
                string version = info.TargetFullRelease.Version.ToString();
                Tell(owner, status, "Version " + version + " is available. Downloading...");
                manager.DownloadUpdates(info, null);
                manager.ApplyUpdatesAndRestart(info.TargetFullRelease, null);
            }
            catch (NotInstalledException)
            {
                if (interactive) Tell(owner, status, "This copy cannot update itself. Download Setup.exe from GitHub Releases.");
            }
            catch (Exception ex)
            {
                if (interactive) Tell(owner, status, "Could not check GitHub for updates. " + ex.Message);
            }
        }

        private static void Tell(Form owner, Action<string> status, string message)
        {
            if (status == null) return;
            try
            {
                if (owner != null && !owner.IsDisposed && owner.InvokeRequired) owner.BeginInvoke(status, new object[] { message });
                else status(message);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }
}
