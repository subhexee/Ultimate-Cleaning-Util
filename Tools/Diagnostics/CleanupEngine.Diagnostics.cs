using System.Diagnostics;

namespace UltimateCleaningUtility;

internal static partial class CleanupEngine
{
    private static Task RunPowerShellAsync(string script, IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("powershell.exe",
            new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script },
            progress, token);

    public static Task CheckDiskHealthAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "if (Get-Command Get-PhysicalDisk -ErrorAction SilentlyContinue) { " +
            "Get-PhysicalDisk | Select-Object FriendlyName,MediaType,HealthStatus,OperationalStatus,@{Name='SizeGB';Expression={[math]::Round($_.Size/1GB,1)}} | Format-Table -AutoSize | Out-String -Width 220 } " +
            "else { Get-CimInstance Win32_DiskDrive | Select-Object Model,Status,Size | Format-Table -AutoSize | Out-String -Width 220 }";
        return RunPowerShellAsync(script, progress, token);
    }

    public static Task CreateBatteryReportAsync(IProgress<string> progress, CancellationToken token)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"BatteryReport_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        progress.Report($"The battery report will be saved to: {path}");
        return RunProcessAsync("powercfg.exe", new[] { "/batteryreport", "/output", path }, progress, token);
    }

    public static Task AnalyzeStartupAppsAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "Get-CimInstance Win32_StartupCommand | Sort-Object Name | " +
            "Select-Object Name,Command,Location,User | Format-Table -Wrap -AutoSize | Out-String -Width 240";
        return RunPowerShellAsync(script, progress, token);
    }

    public static Task AnalyzeEventLogsAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "$since = (Get-Date).AddDays(-1); " +
            "Get-WinEvent -FilterHashtable @{LogName=@('System','Application');Level=@(1,2);StartTime=$since} -MaxEvents 30 -ErrorAction SilentlyContinue | " +
            "Select-Object TimeCreated,LogName,ProviderName,Id,LevelDisplayName,Message | Format-List | Out-String -Width 220";
        return RunPowerShellAsync(script, progress, token);
    }

    public static Task ListInstalledAppsAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'SilentlyContinue'; " +
            "$paths = @('HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*'," +
            "'HKLM:\\Software\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*'," +
            "'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*'); " +
            "foreach ($p in $paths) { Get-ItemProperty $p | Where-Object { $_.DisplayName } | " +
            "Select-Object DisplayName,DisplayVersion,Publisher } | Sort-Object DisplayName -Unique | " +
            "Format-Table -Wrap -AutoSize | Out-String -Width 220";
        return RunPowerShellAsync(script, progress, token);
    }

    public static Task ListDriversAsync(IProgress<string> progress, CancellationToken token) =>
        RunProcessAsync("pnputil.exe", new[] { "/enum-drivers" }, progress, token);

    public static Task CheckActivationAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "$ErrorActionPreference = 'Continue'; " +
            "$items = Get-CimInstance SoftwareLicensingProduct | Where-Object { $_.PartialProductKey -and $_.Name -like '*Windows*' }; " +
            "if ($items) { $items | Select-Object Name,Description,LicenseStatus,PartialProductKey | Format-List | Out-String -Width 220 } " +
            "else { Write-Output 'No Windows licensing product with a partial key was found.' }";
        return RunPowerShellAsync(script, progress, token);
    }

    public static Task CreateSystemInfoReportAsync(IProgress<string> progress, CancellationToken token)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"SystemInfo_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        progress.Report($"The MSInfo32 report will be saved to: {path}");
        return RunProcessAsync("msinfo32.exe", new[] { "/report", path }, progress, token);
    }

    public static Task RunNetworkDiagnosticsAsync(IProgress<string> progress, CancellationToken token)
    {
        const string script = "Write-Output '=== IP CONFIGURATION ==='; ipconfig.exe /all; " +
            "Write-Output '=== INTERNET REACHABILITY (1.1.1.1) ==='; ping.exe -n 2 1.1.1.1; " +
            "Write-Output '=== DNS LOOKUP (microsoft.com) ==='; nslookup.exe microsoft.com";
        return RunPowerShellAsync(script, progress, token);
    }
}
