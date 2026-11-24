# Ultimate Cleaning Utility ReDesigned

**Made by EXE COMPUTERS**

An original Windows maintenance utility by **SubhExe**.

## Current foundation
- Modern WinForms shell with dark/light theme toggle
- Sidebar navigation and operation cards
- Selectable operations and confirmation before execution
- Persistent execution console, progress bar and cancellation
- Safe temporary-folder cleanup that skips inaccessible files and avoids traversing reparse points
- Thumbnail and Internet cache cleanup
- DNS cache flush
- DISM component cleanup and SFC scan
- Windows Update cache intentionally marked as coming soon until service-state handling is implemented and tested

## Safety choices
This project intentionally does **not** clear Event Logs, delete Prefetch, delete Windows Installer PatchCache, or blindly delete Windows Update data. It does not silently empty the Recycle Bin or remove Windows.old.

Some actions (such as deleting cache files) cannot be undone. The UI asks for confirmation before execution. A future release should add a restore-point workflow and reversible settings snapshots before adding system tweaks.

## Requirements
- Windows 10 or Windows 11
- .NET 10 SDK to build
- Administrator privileges (requested by application manifest)

## Build locally
```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The EXE will be in `publish/`.

## GitHub Actions
Push this project to GitHub. The included workflow builds a Windows x64 artifact when manually triggered or when a `v*` tag is pushed.

## Important
This is an initial development build, not a certified or fully tested release. Test in a Windows virtual machine before using it on a primary PC. Review the code before distributing it.
