using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Globalization;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace UltimateCleaningUtility;

internal sealed class UsbMediaCreatorForm : AdvancedToolForm
{
    private readonly ComboBox _disk = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(24, 112), Width = 690 };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(24, 188), Width = 300 };
    private readonly ComboBox _scheme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(340, 188), Width = 190 };
    private readonly TextBox _iso = new() { Location = new Point(24, 265), Width = 690, ReadOnly = true };
    private readonly Label _info = new() { Location = new Point(24, 150), AutoSize = true };
    private readonly Label _hint = new() { Location = new Point(24, 222), AutoSize = true };
    private readonly CheckBox _verify = new() { Text = "Verify raw image after writing", Location = new Point(24, 330), AutoSize = true, Checked = true };
    private readonly ProgressBar _progress = new() { Location = new Point(24, 400), Width = 900, Height = 22 };
    private readonly Label _status = new() { Location = new Point(24, 370), AutoSize = true, Text = "Ready" };
    private readonly Button _browse, _refresh, _create;
    private string? _isoPath;
    private bool _busy;

    public UsbMediaCreatorForm() : base("USB Bootable Media Creator", "Create Windows installation USBs or write hybrid Linux / recovery images to removable USB drives.")
    {
        MinimumSize = new Size(1180, 820);
        _mode.Items.AddRange(new object[] { "Windows installer (UEFI / BIOS)", "Raw image (Linux / hybrid ISO)" }); _mode.SelectedIndex = 0;
        _scheme.Items.AddRange(new object[] { "GPT (UEFI)", "MBR (Legacy BIOS)" }); _scheme.SelectedIndex = 0;
        _mode.SelectedIndexChanged += (_, _) => UpdateMode();
        _disk.SelectedIndexChanged += (_, _) => UpdateDisk();
        Controls.AddRange(new Control[] { LabelAt("TARGET USB DRIVE", 24, 88), _disk, _info, LabelAt("IMAGE METHOD", 24, 162), _mode, _scheme, _hint, LabelAt("ISO IMAGE", 24, 240), _iso, _verify, _status, _progress });
        _refresh = AddButton("Refresh USB drives", 730, 110, 160, RefreshDisks);
        _browse = AddButton("Browse ISO...", 730, 263, 140, Browse);
        _create = AddButton("Create bootable USB", 24, 440, 205, Create);
        _create.BackColor = Color.FromArgb(30, 184, 126); _create.ForeColor = Color.White;
        Controls.Add(new Label { Text = "WARNING: The selected USB drive will be erased completely. Disconnect other external drives first.", Location = new Point(24, 305), Size = new Size(900, 20), ForeColor = Color.FromArgb(235, 166, 76), AutoEllipsis = true });
        _verify.Location = new Point(24, 335); _status.Location = new Point(24, 370); _progress.Location = new Point(24, 400);
        Resize += (_, _) => { int w = Math.Max(500, ClientSize.Width - 48); _disk.Width = _iso.Width = Math.Min(760, w); _progress.Width = Math.Min(900, w); };
        _info.Text = "Ready. Connect a USB drive, then click Refresh USB drives.";
        _status.Text = "Ready — USB scanning starts only when requested.";
        // Do not run storage enumeration during window startup. Let the form paint
        // and remain responsive; the user can start discovery explicitly.
        WriteLog("Only USB-bus disks not marked as boot/system are listed. Disk identity is checked again before erasing.");
        UpdateMode();
    }

    private static Label LabelAt(string t, int x, int y) => new() { Text = t, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray };
    private void UpdateMode() { bool raw = _mode.SelectedIndex == 1; _scheme.Enabled = !raw; _verify.Enabled = raw; _hint.Text = raw ? "Byte-for-byte writing for hybrid Linux and recovery ISOs that support USB/DD mode." : "Creates a FAT32 Windows installer and splits large install.wim files for UEFI compatibility."; }

    private async void RefreshDisks()
    {
        if (_busy || !_refresh.Enabled) return;
        _refresh.Enabled = false;
        try
        {
            WriteLog("Scanning removable USB drives...");
            string json = await Task.Run(() => Ps("Get-Disk | Where-Object { $_.BusType -eq 'USB' -and -not $_.IsBoot -and -not $_.IsSystem } | Select-Object Number,FriendlyName,SerialNumber,BusType,Size,PartitionStyle,IsBoot,IsSystem,IsReadOnly,IsOffline | ConvertTo-Json -Compress"));
            _disk.Items.Clear();
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                var rows = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement> { doc.RootElement };
                foreach (var r in rows)
                {
                    string G(string k) => r.TryGetProperty(k, out var v) ? v.ToString() : "";
                    if (!int.TryParse(G("Number"), out int n) || !long.TryParse(G("Size"), out long size)) continue;
                    if (G("IsBoot") == "True" || G("IsSystem") == "True" || G("IsReadOnly") == "True" || G("IsOffline") == "True") continue;
                    _disk.Items.Add(new UsbDisk(n, G("FriendlyName"), G("SerialNumber"), size, G("PartitionStyle")));
                }
            }
            if (_disk.Items.Count > 0) _disk.SelectedIndex = 0; else _info.Text = "No eligible USB drive detected. Connect one and refresh.";
            WriteLog("USB scan complete. Eligible drives: " + _disk.Items.Count + ".");
        }
        catch (Exception ex) { _info.Text = "USB detection failed: " + ex.Message; WriteLog(ex.ToString()); }
        finally { if (!IsDisposed) _refresh.Enabled = !_busy; }
    }

    private void UpdateDisk() { if (_disk.SelectedItem is UsbDisk d) _info.Text = $"Disk {d.Number} • {d.Name} • {Fmt(d.Size)} • {d.Partition} • Serial: {d.Serial}"; }
    private void Browse()
    {
        using var d = new OpenFileDialog { Title = "Select bootable ISO", Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*", CheckFileExists = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        var f = new FileInfo(d.FileName); if (f.Length == 0) { MessageBox.Show(this, "The image is empty."); return; }
        _isoPath = f.FullName; _iso.Text = f.FullName; WriteLog("Selected " + f.Name + " (" + Fmt(f.Length) + ").");
    }

    private async void Create()
    {
        if (_busy) return;
        if (_disk.SelectedItem is not UsbDisk disk) { MessageBox.Show(this, "Connect and select a USB drive first."); return; }
        if (string.IsNullOrWhiteSpace(_isoPath) || !File.Exists(_isoPath)) { MessageBox.Show(this, "Choose a valid ISO image first."); return; }
        var file = new FileInfo(_isoPath); bool raw = _mode.SelectedIndex == 1;
        if (file.Length > disk.Size) { MessageBox.Show(this, "The ISO is larger than the USB drive."); return; }
        WindowsSetupOptions? setupOptions = null;
        if (!raw)
        {
            using var optionsForm = new WindowsSetupOptionsForm();
            if (optionsForm.ShowDialog(this) != DialogResult.OK) { WriteLog("Windows setup customization cancelled."); return; }
            setupOptions = optionsForm.Options;
            if (setupOptions.BypassHardware && !IsAdministrator())
            {
                MessageBox.Show(this, "Hardware requirement bypass modifies the Windows PE image and requires running this utility as Administrator. No USB data has been changed.", "Administrator required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        string detail = $"Disk {disk.Number}: {disk.Name}\nCapacity: {Fmt(disk.Size)}\nSerial: {disk.Serial}\nImage: {file.Name} ({Fmt(file.Length)})\nMode: {(raw ? "Raw image" : "Windows installer")}";
        if (MessageBox.Show(this, "THIS OPERATION ERASES THE ENTIRE USB DRIVE.\n\n" + detail + "\n\nContinue only if this is the correct drive.", "Confirm USB erase", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        string confirm = Microsoft.VisualBasic.Interaction.InputBox($"Type ERASE {disk.Number} to confirm.", "Final confirmation", "");
        if (confirm != "ERASE " + disk.Number) { WriteLog("Cancelled: confirmation did not match."); return; }
        Busy(true); _progress.Value = 0;
        try
        {
            var check = ReadDisk(disk.Number);
            if (check == null || check.BusType != "USB" || check.Boot || check.System || check.ReadOnly || check.Offline || check.Size != disk.Size || check.Serial != disk.Serial)
                throw new InvalidOperationException("USB identity changed or disk is no longer eligible. Nothing was erased. Refresh and retry.");
            if (raw) await Task.Run(() => WriteRaw(disk, _isoPath, _verify.Checked));
            else await CreateWindows(disk, _isoPath, setupOptions ?? new WindowsSetupOptions());
            _progress.Value = 100; _status.Text = "Completed. Safely eject the USB before unplugging.";
            WriteLog("Bootable USB creation completed.");
            MessageBox.Show(this, "Bootable USB creation completed. Safely eject before unplugging.", "USB ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshDisks();
        }
        catch (Exception ex) { _status.Text = "Creation failed. Review the live log."; WriteLog("FAILED: " + ex.Message); MessageBox.Show(this, ex.Message, "USB creation failed", MessageBoxButtons.OK, MessageBoxIcon.Error); RefreshDisks(); }
        finally { Busy(false); }
    }

    private async Task CreateWindows(UsbDisk disk, string iso, WindowsSetupOptions options)
    {
        string? mounted = null; string temp = Path.Combine(Path.GetTempPath(), "ucusb-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            string? letter = Enumerable.Range('R', 9).Select(x => ((char)x).ToString()).FirstOrDefault(l => !DriveInfo.GetDrives().Any(d => d.Name.StartsWith(l + ":\\", StringComparison.OrdinalIgnoreCase)));
            if (letter == null) throw new InvalidOperationException("No free drive letter is available.");
            string scheme = _scheme.SelectedIndex == 1 ? "mbr" : "gpt";
            File.WriteAllText(temp, $"select disk {disk.Number}\r\nattributes disk clear readonly\r\nclean\r\nconvert {scheme}\r\ncreate partition primary\r\nformat fs=fat32 quick label=UCUBOOT\r\nassign letter={letter}\r\nexit\r\n");
            WriteLog("Partitioning and formatting USB as FAT32...");
            await Tool("diskpart.exe", new[] { "/s", temp }, WriteLog);
            if (!Directory.Exists(letter + ":\\") ) throw new InvalidOperationException("DiskPart did not mount the formatted USB volume.");
            mounted = (await PsAsync("$ErrorActionPreference='Stop'; $d=Mount-DiskImage -ImagePath " + Q(iso) + " -PassThru; ($d | Get-Volume | Where-Object DriveLetter | Select-Object -First 1 -ExpandProperty DriveLetter)")).Trim().TrimEnd(':');
            if (mounted.Length != 1) throw new InvalidOperationException("Windows could not assign a drive letter to the ISO.");
            string src = mounted + ":\\", dst = letter + ":\\";
            string wim = Path.Combine(src, "sources", "install.wim"), esd = Path.Combine(src, "sources", "install.esd");
            string? large = File.Exists(wim) && new FileInfo(wim).Length > 4L * 1024 * 1024 * 1024 - 1 ? wim : null;
            if (File.Exists(esd) && new FileInfo(esd).Length > 4L * 1024 * 1024 * 1024 - 1) throw new InvalidOperationException("This ISO contains an install.esd larger than FAT32's 4 GB limit. Use an ISO with install.wim or a smaller ESD.");
            WriteLog("Copying Windows setup files...");
            var args = new List<string> { src, dst, "/E", "/COPY:DAT", "/DCOPY:DAT", "/R:2", "/W:2", "/NFL", "/NDL", "/NP" };
            if (large != null) { args.Add("/XF"); args.Add("install.wim"); }
            int rc = await Tool("robocopy.exe", args, WriteLog); if (rc >= 8) throw new InvalidOperationException("Robocopy failed with exit code " + rc);
            if (large != null) { WriteLog("Splitting install.wim into FAT32-compatible parts using DISM..."); await Tool("dism.exe", new[] { "/Split-Image", "/ImageFile:" + large, "/SWMFile:" + Path.Combine(dst, "sources", "install.swm"), "/FileSize:3800", "/CheckIntegrity" }, WriteLog); if (!File.Exists(Path.Combine(dst, "sources", "install.swm"))) throw new InvalidOperationException("DISM did not create install.swm."); }
            _progress.Value = 82; WriteLog("Windows setup files copied.");
            await ApplyWindowsSetupOptions(dst, options);
            _progress.Value = 96; WriteLog("Windows installation customizations applied.");
        }
        finally
        {
            if (mounted != null) try { await PsAsync("Dismount-DiskImage -ImagePath " + Q(iso) + " -ErrorAction SilentlyContinue"); } catch (Exception ex) { WriteLog("ISO dismount warning: " + ex.Message); }
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private void WriteRaw(UsbDisk disk, string iso, bool verify)
    {
        string temp = Path.Combine(Path.GetTempPath(), "ucusb-" + Guid.NewGuid().ToString("N") + ".txt");
        try { File.WriteAllText(temp, $"select disk {disk.Number}\r\nattributes disk clear readonly\r\nclean\r\nexit\r\n"); Tool("diskpart.exe", new[] { "/s", temp }, WriteLog).GetAwaiter().GetResult(); }
        finally { try { File.Delete(temp); } catch { } }
        string device = @"\\.\PhysicalDrive" + disk.Number;
        using var handle = OpenDevice(device, 0x40000000, 3, IntPtr.Zero, 3, 0x80000000, IntPtr.Zero);
        if (handle.IsInvalid) throw new IOException("Could not open USB for writing. Win32 error " + Marshal.GetLastWin32Error());
        using var output = new FileStream(handle, FileAccess.Write, 1024 * 1024, false);
        using (var input = new FileStream(iso, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] buffer = new byte[1024 * 1024]; long total = input.Length, done = 0; int n;
            while ((n = input.Read(buffer, 0, buffer.Length)) > 0) { output.Write(buffer, 0, n); done += n; int p = (int)Math.Min(94, done * 94 / total); Progress(p, $"Writing image: {p}% ({Fmt(done)} / {Fmt(total)})"); }
            output.Flush(true);
        }
        WriteLog("Raw image write completed.");
        if (!verify) return;
        output.Dispose();
        using var readHandle = OpenDevice(device, 0x80000000, 3, IntPtr.Zero, 3, 0x80000000, IntPtr.Zero);
        if (readHandle.IsInvalid) throw new IOException("Could not reopen USB for verification. Win32 error " + Marshal.GetLastWin32Error());
        using var actual = new FileStream(readHandle, FileAccess.Read, 1024 * 1024, false);
        using var expected = new FileStream(iso, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] a = new byte[1024 * 1024], b = new byte[1024 * 1024]; long done2 = 0, total2 = expected.Length;
        while (true)
        {
            int n = expected.Read(b, 0, b.Length); if (n == 0) break; int got = 0;
            while (got < n) { int r = actual.Read(a, got, n - got); if (r == 0) throw new IOException("USB ended before image."); got += r; }
            for (int i = 0; i < n; i++) if (a[i] != b[i]) throw new IOException("Verification mismatch at byte " + (done2 + i));
            done2 += n; Progress(95 + (int)(done2 * 4 / total2), "Verifying: " + (done2 * 100 / total2) + "%");
        }
        WriteLog("Read-back verification passed.");
    }

    private static UsbDisk? ReadDisk(int n)
    {
        using var doc = JsonDocument.Parse(Ps("Get-Disk -Number " + n + " | Select-Object Number,FriendlyName,SerialNumber,BusType,Size,IsBoot,IsSystem,IsReadOnly,IsOffline | ConvertTo-Json -Compress"));
        var r = doc.RootElement; string G(string k) => r.TryGetProperty(k, out var v) ? v.ToString() : "";
        if (!long.TryParse(G("Size"), out long size)) return null;
        return new UsbDisk(n, G("FriendlyName"), G("SerialNumber"), size, "", G("BusType"), G("IsBoot") == "True", G("IsSystem") == "True", G("IsReadOnly") == "True", G("IsOffline") == "True");
    }
    private static string Q(string s) => "'" + s.Replace("'", "''") + "'";
    private static string Ps(string s) => PsAsync(s).GetAwaiter().GetResult();
    private static Task<string> PsAsync(string s) => Capture("powershell.exe", new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(s)) });
    private static async Task<int> Tool(string file, IEnumerable<string> args, Action<string> log)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo { FileName = file, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var a in args) p.StartInfo.ArgumentList.Add(a); p.Start(); var o = Pump(p.StandardOutput, log); var e = Pump(p.StandardError, log); await p.WaitForExitAsync(); await Task.WhenAll(o, e);
        if (file.Equals("dism.exe", StringComparison.OrdinalIgnoreCase) && p.ExitCode != 0) throw new InvalidOperationException("DISM failed: " + p.ExitCode);
        if (file.Equals("robocopy.exe", StringComparison.OrdinalIgnoreCase) && p.ExitCode >= 8) throw new InvalidOperationException("Robocopy failed: " + p.ExitCode);
        return p.ExitCode;
    }
    private static async Task Pump(StreamReader r, Action<string> log) { while (await r.ReadLineAsync() is { } line) if (!string.IsNullOrWhiteSpace(line)) log(line); }
    private static async Task<string> Capture(string file, IEnumerable<string> args)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo { FileName = file, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var a in args) p.StartInfo.ArgumentList.Add(a);
        p.Start();
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException(file + " did not respond within 20 seconds. The operation was stopped; the USB creator should remain usable.");
        }
        string result = await o, error = await e;
        if (p.ExitCode != 0) throw new InvalidOperationException(file + " failed: " + error);
        return result;
    }
    private void Progress(int value, string status) { if (IsDisposed) return; if (InvokeRequired) { BeginInvoke(new Action(() => Progress(value, status))); return; } _progress.Value = Math.Clamp(value, 0, 100); _status.Text = status; }
    private void Busy(bool b) { _busy = b; _disk.Enabled = _mode.Enabled = _browse.Enabled = _refresh.Enabled = _create.Enabled = !b; _scheme.Enabled = !b && _mode.SelectedIndex == 0; _verify.Enabled = !b && _mode.SelectedIndex == 1; }
    private static string Fmt(long b) { string[] u = { "B", "KB", "MB", "GB", "TB" }; double n = b; int i = 0; while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; } return n.ToString("0.##") + " " + u[i]; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle OpenDevice(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    private sealed record UsbDisk(int Number, string Name, string Serial, long Size, string Partition, string BusType = "USB", bool Boot = false, bool System = false, bool ReadOnly = false, bool Offline = false) { public override string ToString() => $"Disk {Number} — {Name} ({Fmt(Size)})"; }

    private static bool IsAdministrator() =>
        new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    private async Task ApplyWindowsSetupOptions(string usbRoot, WindowsSetupOptions options)
    {
        if (options.HasAny)
        {
            await File.WriteAllTextAsync(Path.Combine(usbRoot, "autounattend.xml"), BuildUnattend(options), new System.Text.UTF8Encoding(false));
            WriteLog("Created Windows Setup answer file (autounattend.xml).");
        }
        if (options.BypassHardware)
            await InjectHardwareBypassAsync(Path.Combine(usbRoot, "sources", "boot.wim"));
        if (options.QualityOfLife)
            await InstallQualityOfLifeScriptAsync(usbRoot);
    }

    private static string BuildUnattend(WindowsSetupOptions o)
    {
        string user = System.Security.SecurityElement.Escape(o.Username) ?? "User";
        string culture = CultureInfo.CurrentCulture.Name;
        if (string.IsNullOrWhiteSpace(culture)) culture = "en-US";
        string inputLocale = "0409:00000409";
        try
        {
            using var keyboard = Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload");
            inputLocale = Convert.ToString(keyboard?.GetValue("1")) ?? inputLocale;
        }
        catch { }
        string timezone = System.Security.SecurityElement.Escape(TimeZoneInfo.Local.Id) ?? "UTC";
        string bitlocker = o.DisableBitLocker
            ? """
              <component name="Microsoft-Windows-SecureStartup-FilterDriver" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
                <PreventDeviceEncryption>true</PreventDeviceEncryption>
              </component>
              <component name="Microsoft-Windows-EnhancedStorage-Adm" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
                <TCGSecurityActivationDisabled>1</TCGSecurityActivationDisabled>
              </component>
              """
            : "";
        string online = o.BypassOnlineAccount ? "<HideOnlineAccountScreens>true</HideOnlineAccountScreens><HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>" : "";
        string specialize = o.BypassOnlineAccount
            ? """
              <component name="Microsoft-Windows-Deployment" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
                <RunSynchronous><RunSynchronousCommand wcm:action="add">
                  <Order>1</Order><Description>Enable offline account setup</Description>
                  <Path>reg.exe add "HKLM\Software\Microsoft\Windows\CurrentVersion\OOBE" /v BypassNRO /t REG_DWORD /d 1 /f</Path>
                  <WillReboot>Never</WillReboot>
                </RunSynchronousCommand></RunSynchronous>
              </component>
              """
            : "";
        string privacy = o.SkipPrivacyQuestions ? "<ProtectYourPC>3</ProtectYourPC>" : "";
        string local = o.CreateLocalAccount
            ? $"""
              <UserAccounts><LocalAccounts><LocalAccount wcm:action="add">
                <Name>{user}</Name><DisplayName>{user}</DisplayName><Group>Administrators</Group>
                <Password><Value>UABhAHMAcwB3AG8AcgBkAA==</Value><PlainText>false</PlainText></Password>
              </LocalAccount></LocalAccounts></UserAccounts>
              """
            : "";
        string regional = o.CopyRegionalSettings
            ? $"""
              <component name="Microsoft-Windows-International-Core" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
                <InputLocale>{inputLocale}</InputLocale><SystemLocale>{culture}</SystemLocale>
                <UILanguage>{culture}</UILanguage><UserLocale>{culture}</UserLocale>
              </component>
              """
            : "";
        var commands = new List<(string Description, string Command)>();
        if (o.CreateLocalAccount)
        {
            commands.Add(("Require the new local account to change its temporary password", "net user \"" + o.Username + "\" /logonpasswordchg:yes"));
            commands.Add(("Prevent temporary password expiry", "net accounts /maxpwage:unlimited"));
        }
        if (o.QualityOfLife)
        {
            commands.Add(("Disable Fast Startup", "reg add \"HKLM\\System\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HiberbootEnabled /t REG_DWORD /d 0 /f"));
            commands.Add(("Hide Copilot button", "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowCopilotButton /t REG_DWORD /d 0 /f"));
            commands.Add(("Disable Windows Copilot policy", "reg add \"HKLM\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot\" /v TurnOffWindowsCopilot /t REG_DWORD /d 1 /f"));
            commands.Add(("Use compact taskbar search icon", "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search\" /v SearchboxTaskbarMode /t REG_DWORD /d 1 /f"));
            commands.Add(("Disable consumer suggestions", "reg add \"HKLM\\Software\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f"));
            commands.Add(("Disable Start menu suggestions", "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SystemPaneSuggestionsEnabled /t REG_DWORD /d 0 /f"));
            commands.Add(("Disable Bing search integration", "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search\" /v BingSearchEnabled /t REG_DWORD /d 0 /f"));
            commands.Add(("Disable automatic device metadata downloads", "reg add \"HKLM\\Software\\Policies\\Microsoft\\Windows\\Device Metadata\" /v PreventDeviceMetadataFromNetwork /t REG_DWORD /d 1 /f"));
            commands.Add(("Disable News and Interests", "reg add \"HKLM\\Software\\Policies\\Microsoft\\Dsh\" /v AllowNewsAndInterests /t REG_DWORD /d 0 /f"));
            commands.Add(("Disable Windows Feeds", "reg add \"HKLM\\Software\\Policies\\Microsoft\\Windows\\Windows Feeds\" /v EnableFeeds /t REG_DWORD /d 0 /f"));
            commands.Add(("Disable Teams auto-install", "reg add \"HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Communications\" /v ConfigureChatAutoInstall /t REG_DWORD /d 0 /f"));
            commands.Add(("Prevent Outlook taskbar promotion", "reg add \"HKLM\\Software\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableCloudOptimizedContent /t REG_DWORD /d 1 /f"));
            commands.Add(("Skip Microsoft Edge first-run screen", "reg add \"HKLM\\Software\\Policies\\Microsoft\\Edge\" /v HideFirstRunExperience /t REG_DWORD /d 1 /f"));
            commands.Add(("Use expanded Start menu pin layout", "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v Start_Layout /t REG_DWORD /d 1 /f"));
            commands.Add(("Restore classic right-click context menu", "reg add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /ve /t REG_SZ /d \"\" /f"));
        }
        if (o.ApplySkuSiPolicy)
            commands.Add(("Copy installed Windows Secure Boot policy to EFI system partition", "cmd /c mountvol S: /S && copy %WINDIR%\\system32\\SecureBootUpdates\\SkuSiPolicy.p7b S:\\EFI\\Microsoft\\Boot && mountvol S: /D"));
        string firstLogon = commands.Count == 0 ? "" : "<FirstLogonCommands>" +
            string.Join("", commands.Select((x, i) => "<SynchronousCommand wcm:action=\"add\"><Order>" + (i + 1) +
                "</Order><Description>" + System.Security.SecurityElement.Escape(x.Description) + "</Description><CommandLine>" +
                System.Security.SecurityElement.Escape(x.Command) + "</CommandLine></SynchronousCommand>")) +
            "</FirstLogonCommands>";
        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <unattend xmlns="urn:schemas-microsoft-com:unattend" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
          <settings pass="windowsPE">
            <component name="Microsoft-Windows-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
              <UserData><AcceptEula>true</AcceptEula><ProductKey><Key></Key></ProductKey></UserData>
            </component>
          </settings>
          <settings pass="specialize">
            {specialize}
          </settings>
          <settings pass="oobeSystem">
            {regional}
            <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
              <TimeZone>{timezone}</TimeZone>
              <OOBE><HideEULAPage>true</HideEULAPage>{online}{privacy}</OOBE>
              {local}
              {firstLogon}
            </component>
            {bitlocker}
          </settings>
        </unattend>
        """;
    }
    private async Task InjectHardwareBypassAsync(string bootWim)
    {
        if (!File.Exists(bootWim))
            throw new FileNotFoundException("This Windows ISO has no sources\\boot.wim; the setup requirement bypass cannot be applied.");
        string mount = Path.Combine(Path.GetTempPath(), "EXE_USB_WIM_" + Guid.NewGuid().ToString("N"));
        string hive = "EXE_USB_SYSTEM_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        Directory.CreateDirectory(mount);
        bool mounted = false, loaded = false;
        try
        {
            WriteLog("Mounting Windows PE image to add setup compatibility flags...");
            await RunProcessAsync("dism.exe", new[] { "/Mount-Wim", "/WimFile:" + bootWim, "/Index:2", "/MountDir:" + mount }, WriteLog);
            mounted = true;
            string systemHive = Path.Combine(mount, "Windows", "System32", "config", "SYSTEM");
            await RunProcessAsync("reg.exe", new[] { "load", "HKLM\\" + hive, systemHive }, WriteLog);
            loaded = true;
            string key = "HKLM\\" + hive + "\\ControlSet001\\Setup\\LabConfig";
            foreach (string flag in new[] { "BypassTPMCheck", "BypassSecureBootCheck", "BypassRAMCheck" })
                await RunProcessAsync("reg.exe", new[] { "add", key, "/v", flag, "/t", "REG_DWORD", "/d", "1", "/f" }, WriteLog);
            await RunProcessAsync("reg.exe", new[] { "unload", "HKLM\\" + hive }, WriteLog);
            loaded = false;
            await RunProcessAsync("dism.exe", new[] { "/Unmount-Wim", "/MountDir:" + mount, "/Commit" }, WriteLog);
            mounted = false;
            WriteLog("TPM 2.0, Secure Boot and 4 GB RAM bypass flags committed to Windows PE.");
        }
        finally
        {
            if (loaded) try { await RunProcessAsync("reg.exe", new[] { "unload", "HKLM\\" + hive }, WriteLog); } catch { }
            if (mounted) try { await RunProcessAsync("dism.exe", new[] { "/Unmount-Wim", "/MountDir:" + mount, "/Discard" }, WriteLog); } catch { }
            try { if (Directory.Exists(mount)) Directory.Delete(mount, true); } catch { }
        }
    }

    private async Task InstallQualityOfLifeScriptAsync(string usbRoot)
    {
        string scripts = Path.Combine(usbRoot, "sources", "$OEM$", "$", "Setup", "Scripts");
        Directory.CreateDirectory(scripts);
        string ps = """
        $ErrorActionPreference = 'Continue'
        $log = Join-Path $env:WINDIR 'Temp\EXE-SetupTweaks.log'
        function Set-Dword($path, $name, $value) {
          if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
          New-ItemProperty -Path $path -Name $name -PropertyType DWord -Value $value -Force | Out-Null
        }
        try {
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive' 'DisableFileSyncNGSC' 1
          Set-Dword 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 0
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Device Metadata' 'PreventDeviceMetadataFromNetwork' 1
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' 'DisableCloudOptimizedContent' 1
          Set-Dword 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Communications' 'ConfigureChatAutoInstall' 0
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Dsh' 'AllowNewsAndInterests' 0
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' 'DisableWindowsConsumerFeatures' 1
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds' 'EnableFeeds' 0
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Edge' 'HideFirstRunExperience' 1
          Set-Dword 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot' 'TurnOffWindowsCopilot' 1
          $provisioned = Get-AppxProvisionedPackage -Online
          foreach ($pattern in @('Microsoft.OutlookForWindows','Microsoft.Copilot','MSTeams')) {
            $provisioned | Where-Object { $_.DisplayName -eq $pattern } | ForEach-Object {
              Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction Continue | Out-Null
            }
          }
          foreach ($pattern in @('*Outlook*','*Teams*')) {
            Get-AppxPackage -AllUsers $pattern | Remove-AppxPackage -AllUsers -ErrorAction Continue
          }
          foreach ($setup in @((Join-Path $env:SystemRoot 'System32\OneDriveSetup.exe'),(Join-Path $env:SystemRoot 'SysWOW64\OneDriveSetup.exe'))) {
            if (Test-Path $setup) { Start-Process -FilePath $setup -ArgumentList '/uninstall' -Wait -WindowStyle Hidden; break }
          }
          $defaultHive = Join-Path $env:SystemDrive 'Users\Default\NTUSER.DAT'
          if (Test-Path $defaultHive) {
            & reg.exe load HKU\EXE_DEFAULT $defaultHive | Out-Null
            if ($LASTEXITCODE -eq 0) {
              $cdm = 'Registry::HKEY_USERS\EXE_DEFAULT\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
              foreach ($n in @('SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SubscribedContent-353694Enabled','SubscribedContent-353696Enabled','SystemPaneSuggestionsEnabled','SilentInstalledAppsEnabled')) {
                New-ItemProperty -Path $cdm -Name $n -PropertyType DWord -Value 0 -Force | Out-Null
              }
              $search = 'Registry::HKEY_USERS\EXE_DEFAULT\Software\Microsoft\Windows\CurrentVersion\Search'
              New-ItemProperty -Path $search -Name 'SearchboxTaskbarMode' -PropertyType DWord -Value 1 -Force | Out-Null
              $classic = 'Registry::HKEY_USERS\EXE_DEFAULT\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32'
              New-Item -Path $classic -Force | Out-Null
              Set-ItemProperty -Path $classic -Name '(default)' -Value ''
              & reg.exe unload HKU\EXE_DEFAULT | Out-Null
            }
          }
          "EXE setup QoL actions completed: $(Get-Date -Format o)" | Out-File $log -Append
        } catch { ($_ | Out-String) | Out-File $log -Append }
        """;
        await File.WriteAllTextAsync(Path.Combine(scripts, "EXE-SetupTweaks.ps1"), ps, new System.Text.UTF8Encoding(false));
        string cmd = "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%WINDIR%\\Setup\\Scripts\\EXE-SetupTweaks.ps1\"\r\nexit /b 0\r\n";
        await File.WriteAllTextAsync(Path.Combine(scripts, "SetupComplete.cmd"), cmd, new System.Text.ASCIIEncoding());
        WriteLog("Added optional QoL setup script: OneDrive policy, Outlook/Copilot provisioned-app removal, consumer suggestions, news, Edge first-run, taskbar/search and classic context menu defaults.");
    }

    private static async Task RunProcessAsync(string file, IEnumerable<string> args, Action<string> log)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo { FileName = file, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
        p.Start();
        var stdout = Pump(p.StandardOutput, log);
        var stderr = Pump(p.StandardError, log);
        await p.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        if (p.ExitCode != 0) throw new InvalidOperationException(file + " failed with exit code " + p.ExitCode);
    }

    private sealed class WindowsSetupOptions
    {
        public bool BypassHardware { get; set; }
        public bool BypassOnlineAccount { get; set; }
        public bool CreateLocalAccount { get; set; }
        public string Username { get; set; } = "User";
        public bool CopyRegionalSettings { get; set; }
        public bool SkipPrivacyQuestions { get; set; }
        public bool DisableBitLocker { get; set; }
        public bool QualityOfLife { get; set; }
        public bool ApplySkuSiPolicy { get; set; }
        public bool HasAny => BypassHardware || BypassOnlineAccount || CreateLocalAccount || CopyRegionalSettings || SkipPrivacyQuestions || DisableBitLocker || QualityOfLife || ApplySkuSiPolicy;
    }

    private sealed class WindowsSetupOptionsForm : Form
    {
        private readonly CheckBox _hardware = new() { Text = "Remove requirement for 4GB+ RAM, Secure Boot and TPM 2.0", AutoSize = true };
        private readonly CheckBox _online = new() { Text = "Remove requirement for an online Microsoft account", AutoSize = true };
        private readonly CheckBox _local = new() { Text = "Create a local administrator account", AutoSize = true };
        private readonly TextBox _username = new() { Text = "User", Width = 240 };
        private readonly CheckBox _regional = new() { Text = "Use this PC's regional settings", AutoSize = true };
        private readonly CheckBox _privacy = new() { Text = "Disable data collection questions (privacy prompts)", AutoSize = true };
        private readonly CheckBox _bitlocker = new() { Text = "Disable automatic BitLocker device encryption", AutoSize = true };
        private readonly CheckBox _qol = new() { Text = "Apply Rufus-style Quality of Life improvements", AutoSize = true };
        private readonly CheckBox _sku = new() { Text = "Copy installed Windows Secure Boot policy to EFI partition (SkuSiPolicy)", AutoSize = true };
        public WindowsSetupOptions Options { get; private set; } = new();

        public WindowsSetupOptionsForm()
        {
            Text = "Windows User Experience — EXE USB Creator";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(690, 590);
            BackColor = SystemColors.Control;
            Controls.Add(new Label { Text = "Customize Windows installation", Font = new Font("Segoe UI", 15, FontStyle.Bold), Location = new Point(24, 20), AutoSize = true });
            Controls.Add(new Label { Text = "Choose optional setup bypasses and first-run preferences. Nothing is selected automatically.", Location = new Point(24, 58), Size = new Size(640, 38), ForeColor = SystemColors.GrayText });
            _hardware.Location = new Point(28, 108);
            _online.Location = new Point(28, 149);
            _local.Location = new Point(28, 190);
            var usernameLabel = new Label { Text = "Local account username:", Location = new Point(52, 222), AutoSize = true };
            _username.Location = new Point(230, 218);
            _regional.Location = new Point(28, 258);
            _privacy.Location = new Point(28, 299);
            _bitlocker.Location = new Point(28, 340);
            _qol.Location = new Point(28, 381);
            _sku.Location = new Point(28, 422);
            Controls.AddRange(new Control[] { _hardware, _online, _local, _regional, _privacy, _bitlocker, _qol, _sku });
            _local.CheckedChanged += (_, _) => _username.Enabled = _local.Checked;
            _username.Enabled = false;
            var note = new Label { Text = "Hardware bypass modifies boot.wim and needs Administrator. QoL actions run during setup. BitLocker protection is recommended for portable devices. Silent auto-install is intentionally omitted because it can erase the wrong internal disk.", Location = new Point(28, 465), Size = new Size(630, 58), ForeColor = Color.DarkGoldenrod };
            var ok = new Button { Text = "Continue", DialogResult = DialogResult.OK, Location = new Point(430, 530), Size = new Size(105, 34) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(548, 530), Size = new Size(105, 34) };
            Controls.AddRange(new Control[] { usernameLabel, _username, note, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            FormClosing += (_, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                if (_local.Checked && (string.IsNullOrWhiteSpace(_username.Text) || _username.Text.Length > 20 || _username.Text.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_'))))
                {
                    MessageBox.Show(this, "Enter a local username of 1–20 letters, numbers, hyphens or underscores.", "Invalid username", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true; DialogResult = DialogResult.None; return;
                }
                Options = new WindowsSetupOptions { BypassHardware = _hardware.Checked, BypassOnlineAccount = _online.Checked, CreateLocalAccount = _local.Checked, Username = _username.Text.Trim(), CopyRegionalSettings = _regional.Checked, SkipPrivacyQuestions = _privacy.Checked, DisableBitLocker = _bitlocker.Checked, QualityOfLife = _qol.Checked, ApplySkuSiPolicy = _sku.Checked };
            };
        }
    }
}
