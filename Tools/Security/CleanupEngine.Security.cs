namespace UltimateCleaningUtility;

internal static partial class CleanupEngine
{
    public static Task RunDefenderQuickScanAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Stop'; " +
            "if (-not (Get-Command Start-MpScan -ErrorAction SilentlyContinue)) { throw 'Microsoft Defender cmdlets are unavailable. Defender may be disabled or managed by another antivirus.' }; " +
            "Start-MpScan -ScanType QuickScan; Write-Output 'Microsoft Defender quick scan request completed.'";
        return RunProcessAsync("powershell.exe",
            new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script },
            progress, token);
    }

    public static Task UpdateDefenderSignaturesAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Stop'; " +
            "if (-not (Get-Command Update-MpSignature -ErrorAction SilentlyContinue)) { throw 'Microsoft Defender cmdlets are unavailable. Defender may be disabled or managed by another antivirus.' }; " +
            "Update-MpSignature; Write-Output 'Microsoft Defender security intelligence update completed.'";
        return RunProcessAsync("powershell.exe",
            new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script },
            progress, token);
    }
}
