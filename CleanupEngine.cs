using System.Diagnostics;

namespace UltimateCleaningUtility;

internal static class CleanupEngine
{
    public static Task CleanUserTempAsync(IProgress<string> progress, CancellationToken token) =>
        CleanDirectoryAsync(Path.GetTempPath(), progress, token);

    public static Task CleanWindowsTempAsync(IProgress<string> progress, CancellationToken token) =>
        CleanDirectoryAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), progress, token);

    public static Task CleanThumbnailCacheAsync(IProgress<string> progress, CancellationToken token)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
        return CleanMatchingFilesAsync(path, "thumbcache_*.db", progress, token);
    }

    public static Task CleanInternetCacheAsync(IProgress<string> progress, CancellationToken token)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "INetCache");
        return CleanDirectoryAsync(path, progress, token);
    }

    private static Task CleanDirectoryAsync(string path, IProgress<string> progress, CancellationToken token) =>
        Task.Run(() =>
        {
            if (!Directory.Exists(path)) { progress.Report($"Folder not found; skipped: {path}"); return; }
            progress.Report($"Scanning: {path}");
            DeleteContents(path, progress, token);
        }, token);

    private static Task CleanMatchingFilesAsync(string path, string pattern, IProgress<string> progress, CancellationToken token) =>
        Task.Run(() =>
        {
            if (!Directory.Exists(path)) { progress.Report($"Folder not found; skipped: {path}"); return; }
            string[] files;
            try { files = Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly); }
            catch (Exception ex) { progress.Report($"Could not scan {path}: {ex.Message}"); return; }

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                TryDeleteFile(file, progress);
            }
        }, token);

    private static void DeleteContents(string root, IProgress<string> progress, CancellationToken token)
    {
        string[] files;
        string[] directories;
        try
        {
            files = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly);
            directories = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            progress.Report($"Could not scan folder {root}: {ex.Message}");
            return;
        }

        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            TryDeleteFile(file, progress);
        }

        foreach (var dir in directories)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var info = new DirectoryInfo(dir);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    progress.Report($"Skipped reparse point: {dir}");
                    continue;
                }

                DeleteContents(dir, progress, token);
                try
                {
                    Directory.Delete(dir, false);
                    progress.Report($"Removed empty folder: {Path.GetFileName(dir)}");
                }
                catch (Exception ex)
                {
                    progress.Report($"Kept folder {Path.GetFileName(dir)}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                progress.Report($"Skipped folder {Path.GetFileName(dir)}: {ex.Message}");
            }
        }
    }

    private static void TryDeleteFile(string file, IProgress<string> progress)
    {
        try
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
            progress.Report($"Deleted: {Path.GetFileName(file)}");
        }
        catch (Exception ex)
        {
            progress.Report($"Skipped locked/inaccessible file: {Path.GetFileName(file)} ({ex.Message})");
        }
    }

    public static Task FlushDnsAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("ipconfig.exe", new[] { "/flushdns" }, progress, token);

    public static Task RunDismCleanupAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("dism.exe", new[] { "/Online", "/Cleanup-Image", "/StartComponentCleanup", "/NoRestart" }, progress, token);

    public static Task RunSfcAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("sfc.exe", new[] { "/scannow" }, progress, token);

    private static async Task RunProcessAsync(string file, string[] args, IProgress<string> progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        progress.Report($"Starting {file} {string.Join(" ", args)}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) progress.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) progress.Report("STDERR: " + e.Data);
        };

        if (!process.Start()) throw new InvalidOperationException($"Could not start {file}.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(token);
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            try { await process.WaitForExitAsync(); } catch { }
            throw;
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{file} exited with code {process.ExitCode}. Check the execution log for details.");
        progress.Report($"{file} finished successfully (exit code 0).");
    }
}
