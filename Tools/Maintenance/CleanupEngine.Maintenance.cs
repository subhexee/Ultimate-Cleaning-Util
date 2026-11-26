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

    public static Task OptimizeAllFixedDrivesAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Stop'; " +
            "Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object { " +
            "$drive = $_.DeviceID; Write-Output ('Optimizing fixed drive ' + $drive + '...'); " +
            "& defrag.exe $drive /O /U /V; if ($LASTEXITCODE -ne 0) { throw ('Optimize Drives failed for ' + $drive + ' with exit code ' + $LASTEXITCODE) } " +
            "}; Write-Output 'All detected fixed drives processed.'";
        return RunProcessAsync("powershell.exe",
            new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script },
            progress, token);
    }

    public static Task ScanFilesystemAsync(IProgress<string> progress, CancellationToken token)
    {
        var drive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        progress.Report($"Running an online read-only filesystem scan on {drive}. No offline repair is scheduled.");
        return RunProcessAsync("chkdsk.exe", new[] { drive, "/scan" }, progress, token);
    }

    public static Task VerifySystemFilesAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("sfc.exe", new[] { "/verifyonly" }, progress, token);

    public static Task CheckComponentStoreHealthAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("dism.exe", new[] { "/Online", "/Cleanup-Image", "/CheckHealth" }, progress, token);

    public static Task ScanComponentStoreHealthAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("dism.exe", new[] { "/Online", "/Cleanup-Image", "/ScanHealth" }, progress, token);

}
