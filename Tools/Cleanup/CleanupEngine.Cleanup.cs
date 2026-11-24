namespace UltimateCleaningUtility;

internal static partial class CleanupEngine
{
    public static Task CleanBrowserCachesAsync(IProgress<string> progress, CancellationToken token) =>
        Task.Run(() =>
        {
            var browserProcesses = new[] { "chrome", "msedge", "firefox" };
            var running = browserProcesses.Where(name => Process.GetProcessesByName(name).Length > 0).ToArray();
            if (running.Length > 0)
                throw new InvalidOperationException($"Close these browsers before clearing their caches: {string.Join(", ", running)}. This cleaner never removes cookies, passwords, history or sessions.");

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var cacheFolders = new List<string>();

            AddProfileCaches(Path.Combine(local, "Google", "Chrome", "User Data"), cacheFolders,
                new[] { "Cache", "Code Cache", "GPUCache", "DawnCache" });
            AddProfileCaches(Path.Combine(local, "Microsoft", "Edge", "User Data"), cacheFolders,
                new[] { "Cache", "Code Cache", "GPUCache", "DawnCache" });

            var firefoxProfiles = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefoxProfiles))
            {
                foreach (var profile in Directory.GetDirectories(firefoxProfiles, "*", SearchOption.TopDirectoryOnly))
                {
                    var cache = Path.Combine(profile, "cache2");
                    if (Directory.Exists(cache)) cacheFolders.Add(cache);
                }
            }

            if (cacheFolders.Count == 0)
                progress.Report("No supported browser cache folders were found.");

            foreach (var folder in cacheFolders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                progress.Report($"Clearing browser cache: {folder}");
                DeleteContents(folder, progress, token);
            }
            progress.Report($"Browser cache cleanup finished. Processed {cacheFolders.Distinct(StringComparer.OrdinalIgnoreCase).Count()} cache folder(s).");
        }, token);

    private static void AddProfileCaches(string userDataRoot, List<string> cacheFolders, string[] names)
    {
        if (!Directory.Exists(userDataRoot)) return;
        foreach (var profile in Directory.GetDirectories(userDataRoot, "*", SearchOption.TopDirectoryOnly))
        {
            foreach (var name in names)
            {
                var cache = Path.Combine(profile, name);
                if (Directory.Exists(cache)) cacheFolders.Add(cache);
            }
        }
    }

    public static Task CleanDeliveryOptimizationCacheAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Stop'; " +
            "if (-not (Get-Command Delete-DeliveryOptimizationCache -ErrorAction SilentlyContinue)) { " +
            "Import-Module DeliveryOptimization -ErrorAction Stop }; " +
            "Delete-DeliveryOptimizationCache -Force; " +
            "Write-Output 'Windows Delivery Optimization cache cleanup completed.'";
        return RunProcessAsync("powershell.exe",
            new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script },
            progress, token);
    }

    public static Task CleanOldCrashDumpsAsync(IProgress<string> progress, CancellationToken token) =>
        Task.Run(() =>
        {
            var cutoff = DateTime.Now.AddDays(-30);
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var roots = new[]
            {
                Path.Combine(windows, "Minidump"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps")
            };

            foreach (var root in roots)
            {
                token.ThrowIfCancellationRequested();
                if (!Directory.Exists(root))
                {
                    progress.Report($"Crash dump folder not found; skipped: {root}");
                    continue;
                }

                string[] files;
                try { files = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly); }
                catch (Exception ex) { progress.Report($"Could not scan {root}: {ex.Message}"); continue; }

                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff)
                            TryDeleteFile(file, progress);
                        else
                            progress.Report($"Kept recent crash dump: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        progress.Report($"Skipped crash dump {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }

            var memoryDump = Path.Combine(windows, "MEMORY.DMP");
            if (File.Exists(memoryDump))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (File.GetLastWriteTime(memoryDump) < cutoff)
                        TryDeleteFile(memoryDump, progress);
                    else
                        progress.Report("Kept recent C:\\Windows\\MEMORY.DMP (less than 30 days old).");
                }
                catch (Exception ex) { progress.Report($"Could not process MEMORY.DMP: {ex.Message}"); }
            }
            progress.Report("Old crash dump cleanup finished. Dumps from the last 30 days were retained.");
        }, token);
}
