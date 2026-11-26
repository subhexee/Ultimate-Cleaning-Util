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

    public static Task CheckDefenderStatusAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "if (Get-Command Get-MpComputerStatus -ErrorAction SilentlyContinue) { Get-MpComputerStatus | Select-Object AMServiceEnabled,AntivirusEnabled,AntispywareEnabled,RealTimeProtectionEnabled,BehaviorMonitorEnabled,IoavProtectionEnabled,NISEnabled,QuickScanAge,FullScanAge,AntivirusSignatureVersion,AntivirusSignatureLastUpdated | Format-List | Out-String -Width 220 } " +
            "else { Write-Output 'Microsoft Defender status cmdlets are unavailable. Another antivirus may be active or Defender may be managed.' }";
        return RunProcessAsync("powershell.exe", new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script }, progress, token);
    }

    public static Task CheckFirewallStatusAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "if (Get-Command Get-NetFirewallProfile -ErrorAction SilentlyContinue) { Get-NetFirewallProfile | Select-Object Name,Enabled,DefaultInboundAction,DefaultOutboundAction,NotifyOnListen | Format-Table -AutoSize | Out-String -Width 220 } " +
            "else { netsh.exe advfirewall show allprofiles }";
        return RunProcessAsync("powershell.exe", new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script }, progress, token);
    }

    public static Task CheckBitLockerStatusAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "if (Get-Command Get-BitLockerVolume -ErrorAction SilentlyContinue) { Get-BitLockerVolume | Select-Object MountPoint,VolumeType,VolumeStatus,ProtectionStatus,LockStatus,EncryptionMethod,@{N='EncryptionPercent';E={$_.EncryptionPercentage}} | Format-Table -AutoSize | Out-String -Width 220 } " +
            "else { manage-bde.exe -status }";
        return RunProcessAsync("powershell.exe", new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script }, progress, token);
    }

    public static Task CheckSmartScreenStatusAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "$paths = @('HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer','HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System'); " +
            "foreach ($p in $paths) { if (Test-Path $p) { Get-ItemProperty $p | Select-Object PSPath,SmartScreenEnabled,EnableSmartScreen,ShellSmartScreenLevel | Format-List | Out-String -Width 220 } }; " +
            "Write-Output 'This is a read-only registry inspection; policy and edition can affect the effective SmartScreen state.'";
        return RunProcessAsync("powershell.exe", new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script }, progress, token);
    }

}
