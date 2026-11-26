using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
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
            else await CreateWindows(disk, _isoPath);
            _progress.Value = 100; _status.Text = "Completed. Safely eject the USB before unplugging.";
            WriteLog("Bootable USB creation completed.");
            MessageBox.Show(this, "Bootable USB creation completed. Safely eject before unplugging.", "USB ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshDisks();
        }
        catch (Exception ex) { _status.Text = "Creation failed. Review the live log."; WriteLog("FAILED: " + ex.Message); MessageBox.Show(this, ex.Message, "USB creation failed", MessageBoxButtons.OK, MessageBoxIcon.Error); RefreshDisks(); }
        finally { Busy(false); }
    }

    private async Task CreateWindows(UsbDisk disk, string iso)
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
            _progress.Value = 95; WriteLog("Windows setup files copied.");
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
        foreach (var a in args) p.StartInfo.ArgumentList.Add(a); p.Start(); var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(); string result = await o, error = await e; if (p.ExitCode != 0) throw new InvalidOperationException(file + " failed: " + error); return result;
    }
    private void Progress(int value, string status) { if (IsDisposed) return; if (InvokeRequired) { BeginInvoke(new Action(() => Progress(value, status))); return; } _progress.Value = Math.Clamp(value, 0, 100); _status.Text = status; }
    private void Busy(bool b) { _busy = b; _disk.Enabled = _mode.Enabled = _browse.Enabled = _refresh.Enabled = _create.Enabled = !b; _scheme.Enabled = !b && _mode.SelectedIndex == 0; _verify.Enabled = !b && _mode.SelectedIndex == 1; }
    private static string Fmt(long b) { string[] u = { "B", "KB", "MB", "GB", "TB" }; double n = b; int i = 0; while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; } return n.ToString("0.##") + " " + u[i]; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle OpenDevice(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    private sealed record UsbDisk(int Number, string Name, string Serial, long Size, string Partition, string BusType = "USB", bool Boot = false, bool System = false, bool ReadOnly = false, bool Offline = false) { public override string ToString() => $"Disk {Number} — {Name} ({Fmt(Size)})"; }
}
