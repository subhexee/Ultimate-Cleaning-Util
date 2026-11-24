namespace UltimateCleaningUtility;

internal static partial class CleanupEngine
{
    public static Task OptimizeSystemDriveAsync(IProgress<string> progress, CancellationToken token)
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        progress.Report($"Windows will select the appropriate optimization for {systemDrive} (TRIM for supported SSDs, optimization for HDDs).");
        return RunProcessAsync("defrag.exe", new[] { systemDrive, "/O", "/U", "/V" }, progress, token);
    }

    public static Task CheckTrimStatusAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("fsutil.exe", new[] { "behavior", "query", "DisableDeleteNotify" }, progress, token);

    public static Task CreateRestorePointAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Stop'; " +
            "Checkpoint-Computer -Description 'Ultimate Cleaning Utility - Before maintenance' -RestorePointType MODIFY_SETTINGS; " +
            "Write-Output 'Restore point creation request completed.'";
        return RunProcessAsync("powershell.exe",
            new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script },
            progress, token);
    }
}
