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
            foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
                TryDeleteFile(file, progress);
            }
        }, token);

    private static void DeleteContents(string root, IProgress<string> progress, CancellationToken token)
    {
        // Deliberately do not recurse through reparse points/junctions.
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            token.ThrowIfCancellationRequested();
            TryDeleteFile(file, progress);
        }
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var info = new DirectoryInfo(dir);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) { progress.Report($"Skipped reparse point: {dir}"); continue; }
                DeleteContents(dir, progress, token);
                try { Directory.Delete(dir, false); } catch { }
            }
            catch (Exception ex) { progress.Report($"Skipped folder: {Path.GetFileName(dir)} ({ex.Message})"); }
        }
    }

    private static void TryDeleteFile(string file, IProgress<string> progress)
    {
        try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); }
        catch (Exception ex) { progress.Report($"Skipped locked/inaccessible file: {Path.GetFileName(file)} ({ex.Message})"); }
    }

    public static Task FlushDnsAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("ipconfig.exe", "/flushdns", progress, token);

    public static Task RunDismCleanupAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /NoRestart", progress, token);

    public static Task RunSfcAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("sfc.exe", "/scannow", progress, token);

    private static async Task RunProcessAsync(string file, string args, IProgress<string> progress, CancellationToken token)
    {
        progress.Report($"Starting {file} {args}");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = file, Arguments = args, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        var output = await stdout;
        var error = await stderr;
        foreach (var line in (output + Environment.NewLine + error).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).TakeLast(18))
            progress.Report(line.Trim());
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{file} exited with code {process.ExitCode}.");
    }
}
