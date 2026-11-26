# Ultimate Cleaning Utility ReDesigned

**Made by EXE COMPUTERS**

An original Windows maintenance and diagnostics utility by **SubhExe**.

## Tool catalogue

### Cleanup
- User and Windows temporary files, including an age-filtered user-temp mode
- Thumbnail, Explorer icon, DirectX shader, legacy Internet and supported browser caches
- Windows Error Reporting queue/archive, aged crash dumps and Delivery Optimization cache
- Recycle Bin cleanup (permanent; confirmation required)
- Previous Windows installation (Windows.old) size analysis and Windows-managed cleanup
- Large-file finder (512 MB threshold) with CSV report; user-profile storage breakdown with CSV report
- Empty-folder finder for common user folders (report only; no automatic deletion)
- Windows Update download-cache cleanup with service stop/restart handling

### Repair and maintenance
- DISM component-store analysis, quick health check, deep scan, cleanup and RestoreHealth
- SFC scan and read-only verification
- Online CHKDSK scan, Optimize Drives for the system drive or all fixed local drives
- SSD TRIM status and System Restore point creation

### Diagnostics and reports
- Disk health/SMART, volume capacity and free-space inventory
- Battery and power-efficiency reports
- Startup inventory, boot-performance events, process/memory usage and event-log analysis
- Installed application and driver inventories, Windows activation status and MSInfo32 report
- Network connectivity, adapter and IP configuration diagnostics
- Windows Update history and common pending-reboot indicators

### Security
- Microsoft Defender quick scan, signature update and protection status
- Windows Firewall profile status, BitLocker volume status and SmartScreen configuration inspection

### Advanced tools
- App uninstaller with post-uninstall leftover scan and review-before-delete
- Duplicate file finder with hash-based matching and Recycle Bin removal
- Reversible gaming profile with settings backup and restore
- Windows debloat profiles with registry backup/restore and optional app removal
- Startup Manager for common Run keys, Startup folders and logon/boot scheduled tasks; services and drivers are inventory-only
- Windows USB media creator with Windows setup customizations and raw image writing

## Safety model

- Nothing is selected or executed automatically.
- Read-only diagnostics are separated from actions that modify Windows.
- Destructive actions require explicit confirmation in the UI.
- Temporary-file cleanup skips inaccessible/locked files and avoids traversing reparse points.
- Windows services, drivers, Defender, Windows Update, Store and core Xbox services are not indiscriminately disabled.
- Gaming and debloat settings create backups where restoration is supported.
- USB creation erases the selected USB drive; carefully verify the selected disk before proceeding.
- Some operations require Administrator privileges. Long-running operations can take several minutes.

This utility is not a guarantee of improved performance. Results depend on hardware, Windows version, drivers and workload. Some cleanup operations are irreversible, and restore points are not a substitute for a full backup. Test changes on a non-critical system before broad deployment.

## Requirements
- Windows 10 or Windows 11
- .NET 10 SDK to build
- Administrator privileges for elevated maintenance and repair operations

## Build locally
```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The EXE will be in `publish/`.

## GitHub Actions
Push to `main`, manually trigger the workflow, or push a `v*` tag to build a Windows x64 artifact.
