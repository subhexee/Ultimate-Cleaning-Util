using System.Diagnostics;

namespace UltimateCleaningUtility;

internal static partial class CleanupEngine
{
    public static Task CleanBrowserCachesAsync(IProgress<string> progress, CancellationToken token) =>
        Task.Run(() =>
        {
            var browserProcesses = new[] { "chrome", "msedge", "firefox" };
            var running = browserProcesses.Where(name =>
            {
                var processes = Process.GetProcessesByName(name);
                try { return processes.Length > 0; }
                finally { foreach (var process in processes) process.Dispose(); }
            }).ToArray();
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

    public static Task CleanExplorerIconCacheAsync(IProgress<string> progress, CancellationToken token)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
        return Task.Run(() =>
        {
            if (!Directory.Exists(path)) { progress.Report("Explorer cache folder was not found."); return; }
            var files = Directory.GetFiles(path, "iconcache*.db", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(path, "iconcache*.db-wal", SearchOption.TopDirectoryOnly))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                TryDeleteFile(file, progress);
            }
            progress.Report("Icon cache cleanup finished. Windows Explorer will rebuild icons as needed; a sign-out or Explorer restart may be needed.");
        }, token);
    }

    public static Task CleanOldUserTempFilesAsync(IProgress<string> progress, CancellationToken token)
    {
        var root = Path.GetTempPath();
        var cutoff = DateTime.Now.AddDays(-7);
        return Task.Run(() =>
        {
            if (!Directory.Exists(root)) { progress.Report("User temporary folder was not found."); return; }
            progress.Report($"Removing only temporary files older than 7 days from {root}.");
            CleanOldFilesRecursive(root, cutoff, progress, token);
            progress.Report("Old temporary file cleanup finished. Newer files and locked files were retained.");
        }, token);
    }

    private static void CleanOldFilesRecursive(string root, DateTime cutoff, IProgress<string> progress, CancellationToken token)
    {
        string[] files;
        string[] dirs;
        try { files = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly); dirs = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly); }
        catch (Exception ex) { progress.Report($"Skipped inaccessible folder {root}: {ex.Message}"); return; }
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            try { if (File.GetLastWriteTime(file) < cutoff) TryDeleteFile(file, progress); }
            catch (Exception ex) { progress.Report($"Skipped {Path.GetFileName(file)}: {ex.Message}"); }
        }
        foreach (var dir in dirs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var info = new DirectoryInfo(dir);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                CleanOldFilesRecursive(dir, cutoff, progress, token);
            }
            catch (Exception ex) { progress.Report($"Skipped folder {Path.GetFileName(dir)}: {ex.Message}"); }
        }
    }


    public static Task AnalyzePreviousInstallationAsync(IProgress<string> progress, CancellationToken token) => Task.Run(() =>
    {
        var windowsOld = Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\\\", "Windows.old");
        if (!Directory.Exists(windowsOld)) { progress.Report("Windows.old was not found. There is no previous Windows installation folder to review."); return; }
        long bytes = GetDirectorySizeSafe(windowsOld, progress, token);
        progress.Report($"Previous Windows installation found: {windowsOld}");
        progress.Report($"Estimated size: {FormatBytes(bytes)}");
        progress.Report("Review this folder before removal. It may contain files needed to roll back to the previous Windows version.");
    }, token);

    public static Task CleanPreviousInstallationAsync(IProgress<string> progress, CancellationToken token)
    {
        progress.Report("Launching Windows Disk Cleanup automatic previous-installation cleanup.");
        progress.Report("Windows decides whether old installation files are eligible. Rollback may no longer be possible afterward.");
        return RunProcessAsync("cleanmgr.exe", new[] { "/autoclean" }, progress, token);
    }

    public static Task ScanLargeFilesAsync(IProgress<string> progress, CancellationToken token) => Task.Run(() =>
    {
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.GetTempPath()
        }.Where(Directory.Exists)
         .Select(Path.GetFullPath)
         .Distinct(StringComparer.OrdinalIgnoreCase)
         .ToArray();

        // The normal user temp directory is already inside the user profile.
        // Avoid scanning it twice, but retain it when TEMP is redirected elsewhere.
        var roots = candidates.Where(candidate => !candidates.Any(parent =>
        {
            if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)) return false;
            var relative = Path.GetRelativePath(parent, candidate);
            return !Path.IsPathRooted(relative)
                && relative != "."
                && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        })).ToArray();
        var found = new List<(string Path, long Size, DateTime Modified)>();
        foreach (var root in roots)
        {
            progress.Report("Scanning for large files under " + root);
            CollectLargeFiles(root, found, progress, token, 512L * 1024 * 1024);
        }
        var ordered = found.OrderByDescending(x => x.Size).Take(150).ToList();
        var report = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EXE-Large-Files-Report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
        var csv = new List<string> { "SizeBytes,LastModified,FilePath" };
        csv.AddRange(ordered.Select(x => $"{x.Size},{x.Modified.ToString("yyyy-MM-dd HH:mm:ss")},\"{x.Path.Replace("\"", "\"\"")}\""));
        File.WriteAllLines(report, csv, System.Text.Encoding.UTF8);
        progress.Report($"Found {found.Count} file(s) at least 512 MB. Top {ordered.Count} saved to: {report}");
        progress.Report("This is a review-only report. No files were deleted or moved.");
    }, token);

    public static Task AnalyzeStorageUsageAsync(IProgress<string> progress, CancellationToken token) => Task.Run(() =>
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Directory.Exists(profile)) { progress.Report("User profile folder not found."); return; }
        var rows = new List<(string Folder, long Bytes)>();
        foreach (var folder in Directory.GetDirectories(profile, "*", SearchOption.TopDirectoryOnly))
        {
            token.ThrowIfCancellationRequested();
            var info = new DirectoryInfo(folder);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            progress.Report("Measuring " + info.Name);
            rows.Add((folder, GetDirectorySizeSafe(folder, progress, token)));
        }
        var report = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EXE-Storage-Breakdown-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
        File.WriteAllLines(report, new[] { "Folder,SizeBytes" }.Concat(rows.OrderByDescending(x => x.Bytes).Select(x => $"\"{x.Folder.Replace("\"", "\"\"")}\",{x.Bytes}")), System.Text.Encoding.UTF8);
        foreach (var row in rows.OrderByDescending(x => x.Bytes).Take(12)) progress.Report($"{Path.GetFileName(row.Folder)} — {FormatBytes(row.Bytes)}");
        progress.Report("Storage breakdown report saved to: " + report);
        progress.Report("Read-only scan: no files were changed.");
    }, token);

    public static Task FindEmptyFoldersAsync(IProgress<string> progress, CancellationToken token) => Task.Run(() =>
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[] { "Desktop", "Documents", "Downloads", "Pictures", "Videos", "Music" }
            .Select(name => Path.Combine(profile, name)).Where(Directory.Exists).ToArray();
        var empty = new List<string>();
        foreach (var root in roots) CollectEmptyFolders(root, empty, progress, token);
        var report = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EXE-Empty-Folders-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        File.WriteAllLines(report, empty);
        progress.Report($"Found {empty.Count} empty folder(s) in common user folders. Report: {report}");
        progress.Report("Nothing was deleted. Review each folder manually before removing it.");
    }, token);

    private static void CollectLargeFiles(string root, List<(string Path, long Size, DateTime Modified)> found, IProgress<string> progress, CancellationToken token, long minimum)
    {
        token.ThrowIfCancellationRequested();
        string[] files; string[] dirs;
        try { files = Directory.GetFiles(root); dirs = Directory.GetDirectories(root); }
        catch (Exception ex) { progress.Report("Skipped inaccessible folder: " + root + " — " + ex.Message); return; }
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            try { var info = new FileInfo(file); if (info.Length >= minimum) found.Add((file, info.Length, info.LastWriteTime)); }
            catch { }
        }
        foreach (var dir in dirs)
        {
            token.ThrowIfCancellationRequested();
            try { if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0) CollectLargeFiles(dir, found, progress, token, minimum); }
            catch { }
        }
    }

    private static void CollectEmptyFolders(string root, List<string> empty, IProgress<string> progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string[] dirs;
        try { dirs = Directory.GetDirectories(root); }
        catch (Exception ex) { progress.Report("Skipped inaccessible folder: " + root + " — " + ex.Message); return; }
        foreach (var dir in dirs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                CollectEmptyFolders(dir, empty, progress, token);
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) empty.Add(dir);
            }
            catch { }
        }
    }

    private static long GetDirectorySizeSafe(string root, IProgress<string> progress, CancellationToken token)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
                try { total += new FileInfo(file).Length; } catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
                try { if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0) total += GetDirectorySizeSafe(dir, progress, token); } catch { }
            }
        }
        catch (Exception ex) { progress.Report("Partial size estimate; skipped " + root + " — " + ex.Message); }
        return total;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes; int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString("0.##") + " " + units[unit];
    }

}
