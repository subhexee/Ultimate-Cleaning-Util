using Microsoft.Win32;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace UltimateCleaningUtility;

internal sealed class StartupManagerForm : Form
{
    private const string DisabledPrefix = "__EXE_CLEANER_DISABLED__";
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new();
    private readonly Button _apply = new();
    private readonly string _backupFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EXE COMPUTERS", "Ultimate Cleaning Utility", "Disabled Startup");
    private readonly List<StartupEntry> _entries = new();

    public StartupManagerForm()
    {
        Text = "Startup Manager — EXE COMPUTERS";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 580);
        Size = new Size(1120, 720);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(17, 24, 36);
        ForeColor = Color.FromArgb(235, 241, 248);

        var header = new Panel { Dock = DockStyle.Top, Height = 104, Padding = new Padding(20, 14, 20, 10) };
        header.BackColor = Color.FromArgb(24, 34, 49);
        header.Controls.Add(new Label
        {
            Text = "Startup",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(18, 10)
        });
        header.Controls.Add(new Label
        {
            Text = "Autoruns-inspired inventory of common Windows logon and startup locations.",
            ForeColor = Color.FromArgb(170, 185, 202),
            AutoSize = true,
            Location = new Point(20, 52)
        });
        Controls.Add(header);

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(18, 10, 18, 8),
            Text = "Checked = currently enabled. Untick entries you want to disable, then choose Run tweaks. Registry and Startup-folder entries can be restored here. Logon/boot tasks can be toggled with Task Scheduler. Services and drivers are shown for awareness only and are not bulk-disabled.",
            ForeColor = Color.FromArgb(205, 215, 226),
            BackColor = Color.FromArgb(30, 43, 60)
        };
        Controls.Add(note);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.BackgroundColor = Color.FromArgb(17, 24, 36);
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Color.FromArgb(47, 61, 80);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(37, 52, 72);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(24, 34, 49);
        _grid.DefaultCellStyle.ForeColor = Color.FromArgb(230, 237, 245);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 78, 69);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Enabled", Width = 72, ThreeState = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Startup item", Width = 240, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Entry type", Width = 155, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Location", HeaderText = "Location", Width = 260, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Command", HeaderText = "Command / target", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        Controls.Add(_grid);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(16, 10, 16, 10), BackColor = Color.FromArgb(24, 34, 49) };
        _summary.AutoSize = true;
        _summary.Location = new Point(18, 22);
        footer.Controls.Add(_summary);

        var refresh = new Button { Text = "Refresh", Width = 100, Height = 36, Anchor = AnchorStyles.Right | AnchorStyles.Top, Location = new Point(690, 13) };
        refresh.Click += async (_, _) => await LoadEntriesAsync();
        footer.Controls.Add(refresh);

        _apply.Text = "Run tweaks";
        _apply.Width = 140;
        _apply.Height = 36;
        _apply.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _apply.Location = new Point(800, 13);
        _apply.BackColor = Color.FromArgb(30, 184, 126);
        _apply.ForeColor = Color.White;
        _apply.FlatStyle = FlatStyle.Flat;
        _apply.FlatAppearance.BorderSize = 0;
        _apply.Click += async (_, _) => await ApplyChangesAsync();
        footer.Controls.Add(_apply);
        Controls.Add(footer);

        // Keep header, note and footer visible; the inventory grid fills the space between them.
        Controls.SetChildIndex(_grid, 0);
        Controls.SetChildIndex(footer, 1);
        Controls.SetChildIndex(note, 2);
        Controls.SetChildIndex(header, 3);

        Shown += async (_, _) => await LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        if (!_apply.Enabled) return;
        _apply.Enabled = false;
        _summary.Text = "Scanning startup locations… please wait";
        _grid.Enabled = false;
        try
        {
            await Task.Run(() =>
            {
                _entries.Clear();
                AddRegistryEntries(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run");
                AddRegistryEntries(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce");
                AddRegistryEntries(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run");
                AddRegistryEntries(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce");
                AddRegistryEntries(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run");
                AddRegistryEntries(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\RunOnce");
                AddStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Current user Startup folder");
                AddStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "All users Startup folder");
                AddDisabledFolderEntries();
                AddScheduledTasks();
                AddReadOnlyServices();
                AddReadOnlyDrivers();
            });
        }
        catch (Exception ex)
        {
            _entries.Add(StartupEntry.Info($"Startup inventory error: {ex.Message}"));
        }
        finally
        {
            _grid.Rows.Clear();
            foreach (var entry in _entries)
            {
                var index = _grid.Rows.Add(entry.Enabled, entry.Name, entry.Type, entry.Location, entry.Command);
                _grid.Rows[index].Tag = entry;
                if (entry.ReadOnly)
                {
                    _grid.Rows[index].Cells[0].ReadOnly = true;
                    _grid.Rows[index].Cells[0].ToolTipText = "Shown for awareness; this entry is not changed by this utility.";
                }
            }
            var editable = _entries.Count(x => !x.ReadOnly);
            _summary.Text = $"{_entries.Count} entries found  •  {editable} manageable  •  Checked entries are enabled";
            _grid.Enabled = true;
            _apply.Enabled = true;
        }
    }

    private void AddRegistryEntries(RegistryHive hive, RegistryView view, string path)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path, writable: false);
            if (key == null) return;
            foreach (var valueName in key.GetValueNames())
            {
                var disabled = valueName.StartsWith(DisabledPrefix, StringComparison.Ordinal);
                var displayName = disabled ? DecodeDisabledName(valueName) : valueName;
                var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var hiveName = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
                var viewName = view == RegistryView.Registry32 ? "32-bit" : "64-bit";
                _entries.Add(StartupEntry.Registry(displayName, disabled ? false : true,
                    $"{hiveName}\\{path} ({viewName})", value?.ToString() ?? "",
                    hive, view, path, valueName, disabled));
            }
        }
        catch (Exception ex) { _entries.Add(StartupEntry.Info($"Could not read {hive}\\{path}: {ex.Message}")); }
    }

    private void AddStartupFolder(string folder, string label)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(file);
                _entries.Add(StartupEntry.Folder(info.Name, true, label, file, file, false));
            }
        }
        catch (Exception ex) { _entries.Add(StartupEntry.Info($"Could not read {label}: {ex.Message}")); }
    }

    private void AddDisabledFolderEntries()
    {
        if (!Directory.Exists(_backupFolder)) return;
        foreach (var file in Directory.GetFiles(_backupFolder).Where(x => !x.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var metadata = file + ".json";
                if (!File.Exists(metadata)) continue;
                var data = JsonSerializer.Deserialize<FolderBackup>(File.ReadAllText(metadata));
                if (data == null) continue;
                _entries.Add(StartupEntry.Folder(Path.GetFileName(data.OriginalPath), false,
                    "Disabled Startup backup", file, data.OriginalPath, true));
            }
            catch { }
        }
    }

    private static string RunPowerShell(string script)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add(script);
        if (!process.Start()) throw new InvalidOperationException("Could not start PowerShell.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("PowerShell startup inventory timed out after 15 seconds.");
        }
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "PowerShell inventory failed." : error.Trim());
        return output;
    }

    private void AddScheduledTasks()
    {
        const string script = "$ErrorActionPreference='SilentlyContinue'; " +
            "Get-ScheduledTask | Where-Object { $_.Triggers | Where-Object { $_.CimClass.CimClassName -in @('MSFT_TaskLogonTrigger','MSFT_TaskBootTrigger') } } | " +
            "ForEach-Object { [pscustomobject]@{ Name=$_.TaskName; Path=$_.TaskPath; Enabled=[bool]$_.Settings.Enabled; State=[string]$_.State } } | " +
            "ConvertTo-Json -Compress";
        try
        {
            var output = RunPowerShell(script);
            if (string.IsNullOrWhiteSpace(output)) return;
            using var doc = JsonDocument.Parse(output);
            var nodes = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToArray()
                : new[] { doc.RootElement };
            foreach (var node in nodes)
            {
                var name = node.GetProperty("Name").GetString() ?? "";
                var path = node.GetProperty("Path").GetString() ?? "\\";
                var enabled = node.GetProperty("Enabled").GetBoolean();
                var fullName = path + name;
                _entries.Add(StartupEntry.Task(name, enabled, fullName, fullName));
            }
        }
        catch (Exception ex) { _entries.Add(StartupEntry.Info($"Scheduled task inventory unavailable: {ex.Message}")); }
    }

    private void AddReadOnlyServices()
    {
        const string script = "Get-CimInstance Win32_Service -Filter \"StartMode='Auto'\" | " +
            "Select-Object Name,DisplayName,StartName,State | ConvertTo-Json -Compress";
        try
        {
            var output = RunPowerShell(script);
            if (string.IsNullOrWhiteSpace(output)) return;
            using var doc = JsonDocument.Parse(output);
            var nodes = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToArray()
                : new[] { doc.RootElement };
            foreach (var node in nodes)
            {
                var name = node.TryGetProperty("DisplayName", out var display) ? display.GetString() ?? "" : "";
                var service = node.TryGetProperty("Name", out var svc) ? svc.GetString() ?? "" : "";
                var state = node.TryGetProperty("State", out var st) ? st.GetString() ?? "" : "";
                _entries.Add(StartupEntry.ReadOnlyService(name, service, state));
            }
        }
        catch (Exception ex) { _entries.Add(StartupEntry.Info($"Automatic service inventory unavailable: {ex.Message}")); }
    }

    private void AddReadOnlyDrivers()
    {
        const string script = "Get-CimInstance Win32_SystemDriver | Where-Object { $_.StartMode -in @('Boot','System','Auto') } | " +
            "Select-Object Name,DisplayName,StartMode,State | ConvertTo-Json -Compress";
        try
        {
            var output = RunPowerShell(script);
            if (string.IsNullOrWhiteSpace(output)) return;
            using var doc = JsonDocument.Parse(output);
            var nodes = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToArray()
                : new[] { doc.RootElement };
            foreach (var node in nodes)
            {
                var display = node.TryGetProperty("DisplayName", out var d) ? d.GetString() ?? "" : "";
                var name = node.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                var mode = node.TryGetProperty("StartMode", out var m) ? m.GetString() ?? "" : "";
                var state = node.TryGetProperty("State", out var s) ? s.GetString() ?? "" : "";
                _entries.Add(StartupEntry.ReadOnlyService(display, name, $"{mode} driver — {state}", "Startup driver (read-only)"));
            }
        }
        catch (Exception ex) { _entries.Add(StartupEntry.Info($"Startup driver inventory unavailable: {ex.Message}")); }
    }

    private async Task ApplyChangesAsync()
    {
        _grid.EndEdit();
        var changes = new List<(StartupEntry Entry, bool Enable)>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not StartupEntry entry || entry.ReadOnly) continue;
            var checkedNow = row.Cells[0].Value is bool b && b;
            if (checkedNow != entry.Enabled) changes.Add((entry, checkedNow));
        }
        if (changes.Count == 0)
        {
            MessageBox.Show(this, "No startup changes selected.", "Startup Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var message = $"Apply {changes.Count} startup change(s)?\n\n" +
            string.Join("\n", changes.Select(x => $"{(x.Enable ? "Enable" : "Disable")}: {x.Entry.Name}")) +
            "\n\nDisabling an item may affect an application's background features. Windows services and drivers are inventory-only in this manager.";
        if (MessageBox.Show(this, message, "Confirm startup changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _apply.Enabled = false;
        var errors = new List<string>();
        try
        {
            foreach (var (entry, enable) in changes)
            {
                try
                {
                    await Task.Run(() => ApplyOne(entry, enable));
                }
                catch (Exception ex) { errors.Add($"{entry.Name}: {ex.Message}"); }
            }
        }
        finally { _apply.Enabled = true; }

        LoadEntries();
        if (errors.Count == 0)
            MessageBox.Show(this, "Startup changes applied. Disabled items are retained so you can re-enable them here.", "Startup Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(this, "Some changes could not be applied:\n\n" + string.Join("\n", errors), "Startup Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ApplyOne(StartupEntry entry, bool enable)
    {
        switch (entry.Kind)
        {
            case StartupEntryKind.Registry:
                ChangeRegistry(entry, enable);
                break;
            case StartupEntryKind.Folder:
                ChangeStartupFile(entry, enable);
                break;
            case StartupEntryKind.ScheduledTask:
                RunSchtasks(entry.Command, enable);
                break;
        }
    }

    private static void ChangeRegistry(StartupEntry entry, bool enable)
    {
        using var baseKey = RegistryKey.OpenBaseKey(entry.Hive, entry.View);
        using var key = baseKey.OpenSubKey(entry.RegistryPath, writable: true)
            ?? throw new InvalidOperationException("Registry startup key no longer exists.");
        if (enable && entry.IsDisabled)
        {
            var originalName = DecodeDisabledName(entry.RegistryValueName);
            var data = key.GetValue(entry.RegistryValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            var kind = key.GetValueKind(entry.RegistryValueName);
            if (key.GetValueNames().Contains(originalName, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("An enabled entry with the original name already exists.");
            key.SetValue(originalName, data ?? "", kind);
            key.DeleteValue(entry.RegistryValueName, false);
        }
        else if (!enable && !entry.IsDisabled)
        {
            var data = key.GetValue(entry.RegistryValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            var kind = key.GetValueKind(entry.RegistryValueName);
            var disabledName = DisabledPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.RegistryValueName));
            key.SetValue(disabledName, data ?? "", kind);
            key.DeleteValue(entry.RegistryValueName, false);
        }
    }

    private void ChangeStartupFile(StartupEntry entry, bool enable)
    {
        Directory.CreateDirectory(_backupFolder);
        if (!enable && !entry.IsDisabled)
        {
            var backup = Path.Combine(_backupFolder, Guid.NewGuid().ToString("N") + "__" + Path.GetFileName(entry.FilePath));
            File.Move(entry.FilePath, backup);
            File.WriteAllText(backup + ".json", JsonSerializer.Serialize(new FolderBackup(entry.FilePath)));
        }
        else if (enable && entry.IsDisabled)
        {
            var destination = entry.OriginalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) throw new IOException("A startup item with the original filename already exists.");
            File.Move(entry.FilePath, destination);
            var metadata = entry.FilePath + ".json";
            if (File.Exists(metadata)) File.Delete(metadata);
        }
    }

    private static void RunSchtasks(string taskName, bool enable)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("/Change");
        process.StartInfo.ArgumentList.Add("/TN");
        process.StartInfo.ArgumentList.Add(taskName);
        process.StartInfo.ArgumentList.Add(enable ? "/Enable" : "/Disable");
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
    }

    private static string DecodeDisabledName(string valueName)
    {
        var encoded = valueName[DisabledPrefix.Length..];
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
        catch { return valueName; }
    }

    private sealed record FolderBackup(string OriginalPath);
}

internal enum StartupEntryKind { Registry, Folder, ScheduledTask, ReadOnly, Service }

internal sealed class StartupEntry
{
    public string Name { get; init; } = "";
    public bool Enabled { get; init; }
    public string Type { get; init; } = "";
    public string Location { get; init; } = "";
    public string Command { get; init; } = "";
    public StartupEntryKind Kind { get; init; }
    public bool ReadOnly { get; init; }
    public bool IsDisabled { get; init; }
    public RegistryHive Hive { get; init; }
    public RegistryView View { get; init; }
    public string RegistryPath { get; init; } = "";
    public string RegistryValueName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string OriginalPath { get; init; } = "";

    public static StartupEntry Registry(string name, bool enabled, string location, string command,
        RegistryHive hive, RegistryView view, string path, string valueName, bool disabled) =>
        new() { Name = name, Enabled = enabled, Type = "Registry Run key", Location = location, Command = command,
            Kind = StartupEntryKind.Registry, Hive = hive, View = view, RegistryPath = path, RegistryValueName = valueName, IsDisabled = disabled };

    public static StartupEntry Folder(string name, bool enabled, string location, string file, string original, bool disabled) =>
        new() { Name = name, Enabled = enabled, Type = "Startup folder", Location = location, Command = original,
            Kind = StartupEntryKind.Folder, FilePath = file, OriginalPath = original, IsDisabled = disabled };

    public static StartupEntry Task(string name, bool enabled, string location, string command) =>
        new() { Name = name, Enabled = enabled, Type = "Logon / boot task", Location = location, Command = command, Kind = StartupEntryKind.ScheduledTask };

    public static StartupEntry ReadOnlyService(string name, string service, string state, string type = "Automatic service (read-only)") =>
        new() { Name = name, Enabled = true, Type = type, Location = "Windows Services / Drivers", Command = $"{service} — {state}", Kind = StartupEntryKind.Service, ReadOnly = true };

    public static StartupEntry Info(string message) =>
        new() { Name = message, Enabled = false, Type = "Information", Location = "", Command = "", Kind = StartupEntryKind.ReadOnly, ReadOnly = true };
}
