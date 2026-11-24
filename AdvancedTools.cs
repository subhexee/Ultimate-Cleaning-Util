using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;

namespace UltimateCleaningUtility;

internal static class AdvancedToolForms
{
    public static void ShowUninstaller(IWin32Window owner) => new AppUninstallerForm().Show(owner);
    public static void ShowDuplicateFinder(IWin32Window owner) => new DuplicateFinderForm().Show(owner);
    public static void ShowGaming(IWin32Window owner) => new GamingOptimizationForm().Show(owner);
    public static void ShowDebloat(IWin32Window owner) => new DebloatForm().Show(owner);
}

internal abstract class AdvancedToolForm : Form
{
    protected readonly RichTextBox LogBox = new();
    protected AdvancedToolForm(string title, string subtitle)
    {
        Text = title + " — Ultimate Cleaning Utility";
        Icon = Branding.CreateIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(780, 560);
        Size = new Size(980, 720);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(18, 25, 37);
        ForeColor = Color.FromArgb(235, 241, 248);
        Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, Location = new Point(20, 16) });
        Controls.Add(new Label { Text = subtitle, ForeColor = Color.FromArgb(151, 166, 185), AutoSize = true, Location = new Point(22, 54) });
        LogBox.Location = new Point(20, 500);
        LogBox.Size = new Size(920, 165);
        LogBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        LogBox.ReadOnly = true;
        LogBox.BorderStyle = BorderStyle.None;
        LogBox.BackColor = Color.FromArgb(10, 15, 23);
        LogBox.ForeColor = Color.FromArgb(185, 205, 220);
        LogBox.Font = new Font("Cascadia Mono", 8.5f);
        Controls.Add(LogBox);
        Resize += (_, _) => { LogBox.Width = ClientSize.Width - 40; LogBox.Top = ClientSize.Height - 185; };
    }
    protected void WriteLog(string message)
    {
        if (IsDisposed || LogBox.IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => WriteLog(message))); return; }
        LogBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
    }
    protected Button AddButton(string text, int x, int y, int width, Action action)
    {
        var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(29, 40, 56), ForeColor = Color.White, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = Color.FromArgb(65, 78, 96);
        b.Click += (_, _) => action();
        Controls.Add(b);
        return b;
    }
    protected static async Task<string> RunCommand(string file, params string[] args)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo { FileName = file, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
        p.Start();
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        var result = await stdout + await stderr;
        if (p.ExitCode != 0) throw new InvalidOperationException(file + " exited with code " + p.ExitCode + ": " + result);
        return result;
    }
    protected static void StyleList(ListView list)
    {
        list.View = View.Details;
        list.FullRowSelect = true;
        list.GridLines = true;
        list.BackColor = Color.FromArgb(24, 33, 47);
        list.ForeColor = Color.White;
    }
}

internal sealed class AppUninstallerForm : AdvancedToolForm
{
    private readonly ListView _apps = new();
    private readonly ListView _leftovers = new();
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(151, 166, 185) };
    private AppEntry? _selected;

    public AppUninstallerForm() : base("App Uninstaller", "Revo-style workflow: launch the app's uninstaller, then inspect leftover folders before sending them to the Recycle Bin.")
    {
        _apps.Location = new Point(20, 94); _apps.Size = new Size(920, 225); _apps.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleList(_apps); _apps.MultiSelect = false;
        _apps.Columns.Add("Application", 280); _apps.Columns.Add("Publisher", 190); _apps.Columns.Add("Version", 100); _apps.Columns.Add("Install location", 330);
        _apps.SelectedIndexChanged += (_, _) => _selected = _apps.SelectedItems.Count == 0 ? null : _apps.SelectedItems[0].Tag as AppEntry;
        Controls.Add(_apps);
        _status.Text = "Loading installed applications..."; _status.Location = new Point(22, 326); Controls.Add(_status);
        AddButton("Refresh", 20, 350, 100, LoadApps);
        AddButton("Uninstall selected", 130, 350, 155, UninstallSelected);
        AddButton("Scan leftovers", 295, 350, 130, ScanLeftovers);
        AddButton("Recycle checked", 435, 350, 145, RemoveLeftovers);
        Controls.Add(new Label { Text = "Potential leftover folders — verify each full path before removal", AutoSize = true, Location = new Point(22, 396), ForeColor = Color.FromArgb(151, 166, 185) });
        _leftovers.Location = new Point(20, 420); _leftovers.Size = new Size(920, 65); _leftovers.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleList(_leftovers); _leftovers.CheckBoxes = true; _leftovers.Columns.Add("Folder path", 750); _leftovers.Columns.Add("Type", 130);
        Controls.Add(_leftovers);
        Resize += (_, _) => { _apps.Width = ClientSize.Width - 40; _leftovers.Width = ClientSize.Width - 40; _leftovers.Height = Math.Max(45, LogBox.Top - _leftovers.Top - 12); };
        Load += (_, _) => LoadApps();
    }

    private void LoadApps()
    {
        _apps.Items.Clear();
        var entries = new List<AppEntry>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (root == null) continue;
                foreach (var keyName in root.GetSubKeyNames())
                {
                    try
                    {
                        using var key = root.OpenSubKey(keyName);
                        var name = key?.GetValue("DisplayName") as string;
                        var command = key?.GetValue("QuietUninstallString") as string ?? key?.GetValue("UninstallString") as string;
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command) || Convert.ToInt32(key!.GetValue("SystemComponent", 0)) == 1) continue;
                        entries.Add(new AppEntry(name, key.GetValue("Publisher") as string ?? "", key.GetValue("DisplayVersion") as string ?? "", key.GetValue("InstallLocation") as string ?? "", command));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { WriteLog("Could not read an uninstall registry view: " + ex.Message); }
        }
        foreach (var entry in entries.GroupBy(e => e.Name + "|" + e.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(e => e.Name))
        {
            var row = new ListViewItem(entry.Name);
            row.SubItems.Add(entry.Publisher); row.SubItems.Add(entry.Version); row.SubItems.Add(entry.Location); row.Tag = entry;
            _apps.Items.Add(row);
        }
        _status.Text = _apps.Items.Count + " installed application entries found.";
        WriteLog("Application inventory refreshed.");
    }

    private void UninstallSelected()
    {
        if (_selected == null) { MessageBox.Show(this, "Select an application first."); return; }
        if (MessageBox.Show(this, "Launch the registered uninstaller for:\n\n" + _selected.Name + "\n\nComplete its prompts. The utility will not silently remove the application.", "Confirm uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            var command = Environment.ExpandEnvironmentVariables(_selected.Command.Trim());
            ProcessStartInfo psi;
            if (command.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                var args = command[(command.IndexOf(' ') + 1)..];
                args = System.Text.RegularExpressions.Regex.Replace(args, @"(?i)/I(?=\s*\{)", "/X");
                psi = new ProcessStartInfo("msiexec.exe", args) { UseShellExecute = true };
            }
            else
            {
                string exe, args;
                if (command.StartsWith("\""))
                {
                    var end = command.IndexOf('"', 1);
                    if (end < 0) throw new InvalidOperationException("Invalid uninstall command quoting.");
                    exe = command[1..end]; args = command[(end + 1)..].Trim();
                }
                else
                {
                    var split = command.IndexOf(' ');
                    exe = split < 0 ? command : command[..split];
                    args = split < 0 ? "" : command[(split + 1)..];
                }
                psi = new ProcessStartInfo(exe, args) { UseShellExecute = true };
            }
            Process.Start(psi);
            _status.Text = "Uninstaller launched. Complete its prompts, then scan for leftovers.";
            WriteLog("Launched uninstaller for " + _selected.Name);
        }
        catch (Exception ex) { WriteLog("Uninstall launch failed: " + ex.Message); MessageBox.Show(this, ex.Message, "Uninstaller", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ScanLeftovers()
    {
        if (_selected == null) { MessageBox.Show(this, "Select the application you just uninstalled."); return; }
        _leftovers.Items.Clear();
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        var words = _selected.Name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 4).ToArray();
        foreach (var root in roots)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(root, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    var folder = Path.GetFileName(dir);
                    if (folder.Equals(_selected.Name, StringComparison.OrdinalIgnoreCase) || (words.Length > 0 && words.All(w => folder.Contains(w, StringComparison.OrdinalIgnoreCase))))
                    {
                        var row = new ListViewItem(dir); row.SubItems.Add("Folder"); row.Tag = dir; _leftovers.Items.Add(row);
                    }
                }
            }
            catch (Exception ex) { WriteLog("Skipped " + root + ": " + ex.Message); }
        }
        _status.Text = _leftovers.Items.Count + " possible leftover folder(s). Nothing is selected automatically.";
        WriteLog("Leftover scan only checks common application-data folders. Registry entries are not removed automatically.");
    }

    private void RemoveLeftovers()
    {
        var rows = _leftovers.CheckedItems.Cast<ListViewItem>().ToList();
        if (rows.Count == 0) { MessageBox.Show(this, "Tick the folders you have verified."); return; }
        if (MessageBox.Show(this, "Move " + rows.Count + " checked folder(s) to the Recycle Bin?", "Confirm leftover removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var row in rows)
        {
            var path = row.Tag as string;
            try
            {
                if (path != null && Directory.Exists(path)) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                _leftovers.Items.Remove(row);
                WriteLog("Recycled: " + path);
            }
            catch (Exception ex) { WriteLog("Could not recycle " + path + ": " + ex.Message); }
        }
    }
    private sealed record AppEntry(string Name, string Publisher, string Version, string Location, string Command);
}

internal sealed class DuplicateFinderForm : AdvancedToolForm
{
    private readonly TextBox _folder = new() { Location = new Point(20, 100), Width = 690, Height = 28, Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
    private readonly CheckBox _recursive = new() { Text = "Include subfolders", Checked = true, AutoSize = true, Location = new Point(20, 140) };
    private readonly ListView _results = new();
    private readonly Label _summary = new() { Location = new Point(22, 174), AutoSize = true, ForeColor = Color.FromArgb(151, 166, 185) };
    private readonly Button _scan = new() { Text = "Scan", Location = new Point(820, 98), Size = new Size(90, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(29, 40, 56), ForeColor = Color.White };
    public DuplicateFinderForm() : base("Duplicate File Finder", "SHA-256 content matching. Scanning is read-only; selected duplicates are sent to the Recycle Bin.")
    {
        Controls.Add(_folder); Controls.Add(_recursive); Controls.Add(_summary);
        AddButton("Browse", 720, 98, 90, Browse);
        _scan.Click += async (_, _) => await ScanAsync(); Controls.Add(_scan);
        _results.Location = new Point(20, 205); _results.Size = new Size(920, 275); _results.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleList(_results); _results.CheckBoxes = true;
        _results.Columns.Add("Duplicate candidate (one original per group is retained)", 700); _results.Columns.Add("Size", 110); _results.Columns.Add("Group", 80);
        Controls.Add(_results);
        AddButton("Select duplicate candidates", 20, 485, 190, SelectDuplicates);
        AddButton("Recycle checked files", 220, 485, 165, RecycleChecked);
        Resize += (_, _) => { _folder.Width = ClientSize.Width - 270; _results.Width = ClientSize.Width - 40; _results.Height = Math.Max(100, LogBox.Top - _results.Top - 55); };
    }
    private void Browse()
    {
        using var d = new FolderBrowserDialog { Description = "Choose a folder to scan" };
        if (d.ShowDialog(this) == DialogResult.OK) _folder.Text = d.SelectedPath;
    }
    private async Task ScanAsync()
    {
        if (!Directory.Exists(_folder.Text)) { MessageBox.Show(this, "Choose an existing folder."); return; }
        _scan.Enabled = false; _results.Items.Clear(); _summary.Text = "Scanning files and calculating hashes...";
        try
        {
            var groups = await Task.Run(() => ScanDuplicates(_folder.Text, _recursive.Checked));
            long bytes = 0; int count = 0; int groupNo = 0;
            foreach (var group in groups)
            {
                groupNo++;
                var sorted = group.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                for (int i = 1; i < sorted.Length; i++)
                {
                    try
                    {
                        var info = new FileInfo(sorted[i]);
                        var row = new ListViewItem(sorted[i]); row.SubItems.Add(FormatSize(info.Length)); row.SubItems.Add(groupNo.ToString()); row.Tag = sorted[i];
                        _results.Items.Add(row); count++; bytes += info.Length;
                    }
                    catch { }
                }
            }
            _summary.Text = count + " duplicate candidate(s) found; approximately " + FormatSize(bytes) + " potentially reclaimable. First file in each group is kept.";
            WriteLog("Scan finished. " + count + " duplicate candidate(s) across " + groups.Count + " groups.");
        }
        catch (Exception ex) { WriteLog("Scan failed: " + ex.Message); MessageBox.Show(this, ex.Message, "Duplicate finder", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { _scan.Enabled = true; }
    }
    private static List<List<string>> ScanDuplicates(string root, bool recursive)
    {
        var sizes = new Dictionary<long, List<string>>();
        foreach (var file in Enumerate(root, recursive))
        {
            try
            {
                var len = new FileInfo(file).Length;
                if (len == 0) continue;
                if (!sizes.TryGetValue(len, out var list)) sizes[len] = list = new List<string>();
                list.Add(file);
            }
            catch { }
        }
        var results = new List<List<string>>();
        foreach (var files in sizes.Values.Where(v => v.Count > 1))
        {
            var hashes = new Dictionary<string, List<string>>();
            foreach (var file in files)
            {
                try
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    if (!hashes.TryGetValue(hash, out var list)) hashes[hash] = list = new List<string>();
                    list.Add(file);
                }
                catch { }
            }
            results.AddRange(hashes.Values.Where(v => v.Count > 1));
        }
        return results;
    }
    private static IEnumerable<string> Enumerate(string root, bool recursive)
    {
        var stack = new Stack<string>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files; try { files = Directory.GetFiles(dir); } catch { continue; }
            foreach (var file in files) yield return file;
            if (!recursive) continue;
            string[] dirs; try { dirs = Directory.GetDirectories(dir); } catch { continue; }
            foreach (var child in dirs)
                try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) stack.Push(child); } catch { }
        }
    }
    private void SelectDuplicates() { foreach (ListViewItem row in _results.Items) row.Checked = true; }
    private void RecycleChecked()
    {
        var rows = _results.CheckedItems.Cast<ListViewItem>().ToList();
        if (rows.Count == 0) { MessageBox.Show(this, "Select duplicate candidates first."); return; }
        if (MessageBox.Show(this, "Move " + rows.Count + " checked file(s) to the Recycle Bin? Review their paths before continuing.", "Confirm duplicate removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var row in rows)
        {
            var path = row.Tag as string;
            try { if (path != null && File.Exists(path)) FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); _results.Items.Remove(row); }
            catch (Exception ex) { WriteLog("Could not recycle " + path + ": " + ex.Message); }
        }
        WriteLog("Checked duplicate files processed.");
    }
    private static string FormatSize(long b) => b >= 1073741824 ? (b / 1073741824d).ToString("0.00") + " GB" : b >= 1048576 ? (b / 1048576d).ToString("0.0") + " MB" : (b / 1024d).ToString("0") + " KB";
}

internal sealed class GamingOptimizationForm : AdvancedToolForm
{
    private readonly CheckBox _gameMode = new() { Text = "Enable Windows Game Mode", Checked = true, AutoSize = true, Location = new Point(24, 112) };
    private readonly CheckBox _ultimate = new() { Text = "Create / activate Ultimate Performance power plan", Checked = true, AutoSize = true, Location = new Point(24, 150) };
    private readonly CheckBox _dvr = new() { Text = "Disable background Game DVR recording", Checked = true, AutoSize = true, Location = new Point(24, 188) };
    private readonly string _backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ultimate Cleaning Utility", "gaming-settings-backup.json");
    public GamingOptimizationForm() : base("Gaming Optimization", "Windows-level gaming profile. No overclocking, timer tweaks, or disabling essential services.")
    {
        Controls.AddRange(new Control[] { _gameMode, _ultimate, _dvr });
        Controls.Add(new Label { Text = "The current power plan and affected per-user registry values are backed up before changes. Ultimate Performance may increase power use and temperatures.", Location = new Point(24, 235), Size = new Size(850, 58), ForeColor = Color.FromArgb(151, 166, 185) });
        AddButton("Apply gaming profile", 24, 320, 190, Apply);
        AddButton("Restore previous settings", 224, 320, 190, Restore);
        WriteLog("Recommended defaults are selected. Review before applying.");
    }
    private async void Apply()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_backup)!);
            var data = new GamingBackup { ActivePlan = await RunCommand("powercfg.exe", "/getactivescheme") };
            data.Values.Add(CaptureValue(@"Software\Microsoft\GameBar", "AutoGameModeEnabled"));
            data.Values.Add(CaptureValue(@"Software\Microsoft\GameBar", "AllowAutoGameMode"));
            data.Values.Add(CaptureValue(@"System\GameConfigStore", "GameDVR_Enabled"));
            data.Values.Add(CaptureValue(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled"));
            File.WriteAllText(_backup, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            if (_gameMode.Checked)
            {
                SetValue(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                SetValue(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
                WriteLog("Windows Game Mode enabled.");
            }
            if (_dvr.Checked)
            {
                SetValue(@"System\GameConfigStore", "GameDVR_Enabled", 0);
                SetValue(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                WriteLog("Background Game DVR capture disabled; Game Bar itself is not removed.");
            }
            if (_ultimate.Checked)
            {
                var list = await RunCommand("powercfg.exe", "/list");
                var match = System.Text.RegularExpressions.Regex.Match(list, @"([a-fA-F0-9-]{36}).*Ultimate Performance", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var guid = match.Success ? match.Groups[1].Value : "";
                if (guid.Length == 0)
                {
                    var created = await RunCommand("powercfg.exe", "-duplicatescheme", "e9a42b02-d5df-448d-aa00-03f14749eb61");
                    var createdMatch = System.Text.RegularExpressions.Regex.Match(created, @"([a-fA-F0-9-]{36})");
                    if (!createdMatch.Success) throw new InvalidOperationException("Windows did not return the created power plan GUID.");
                    guid = createdMatch.Groups[1].Value;
                }
                await RunCommand("powercfg.exe", "/setactive", guid);
                WriteLog("Ultimate Performance power plan activated. It can increase power consumption.");
            }
            WriteLog("Gaming profile applied. Sign out or restart if a setting does not take effect immediately.");
        }
        catch (Exception ex) { WriteLog("Apply failed: " + ex.Message); MessageBox.Show(this, ex.Message, "Gaming profile", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private async void Restore()
    {
        try
        {
            if (!File.Exists(_backup)) { MessageBox.Show(this, "No saved gaming backup exists."); return; }
            var data = JsonSerializer.Deserialize<GamingBackup>(File.ReadAllText(_backup)) ?? throw new InvalidDataException("Backup file is invalid.");
            foreach (var value in data.Values) RestoreValue(value);
            var match = System.Text.RegularExpressions.Regex.Match(data.ActivePlan, @"([a-fA-F0-9-]{36})");
            if (match.Success) await RunCommand("powercfg.exe", "/setactive", match.Groups[1].Value);
            WriteLog("Previous registry settings and power plan restored.");
        }
        catch (Exception ex) { WriteLog("Restore failed: " + ex.Message); MessageBox.Show(this, ex.Message, "Restore settings", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private static SavedValue CaptureValue(string path, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        var exists = key?.GetValueNames().Contains(name) == true;
        return new SavedValue { Path = path, Name = name, Exists = exists, Value = exists ? Convert.ToString(key!.GetValue(name)) : "", Kind = exists ? key!.GetValueKind(name).ToString() : "DWord" };
    }
    private static void SetValue(string path, string name, int value) { using var key = Registry.CurrentUser.CreateSubKey(path, true); key?.SetValue(name, value, RegistryValueKind.DWord); }
    private static void RestoreValue(SavedValue v)
    {
        using var key = Registry.CurrentUser.CreateSubKey(v.Path, true);
        if (key == null) return;
        if (!v.Exists) { key.DeleteValue(v.Name, false); return; }
        object value = v.Kind == "DWord" && int.TryParse(v.Value, out var number) ? number : v.Value;
        key.SetValue(v.Name, value, Enum.TryParse<RegistryValueKind>(v.Kind, out var kind) ? kind : RegistryValueKind.String);
    }
    private sealed class GamingBackup { public string ActivePlan { get; set; } = ""; public List<SavedValue> Values { get; set; } = new(); }
    private sealed class SavedValue { public string Path { get; set; } = ""; public string Name { get; set; } = ""; public bool Exists { get; set; } public string Value { get; set; } = ""; public string Kind { get; set; } = "DWord"; }
}

internal sealed class DebloatForm : AdvancedToolForm
{
    private readonly ComboBox _preset = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(24, 105), Width = 220 };
    private readonly CheckedListBox _items = new() { Location = new Point(24, 150), Size = new Size(900, 270), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, CheckOnClick = true };
    private readonly Label _description = new() { Location = new Point(260, 109), Size = new Size(640, 32), ForeColor = Color.FromArgb(151, 166, 185) };
    private readonly List<Tweak> _all = new();
    private readonly string _backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ultimate Cleaning Utility", "DebloatBackups");
    public DebloatForm() : base("Windows Debloat", "CTT-inspired Safe, Balanced and Aggressive presets with individual tweak selection and registry backups.")
    {
        _preset.Items.AddRange(new object[] { "Safe", "Balanced", "Aggressive" });
        _preset.SelectedIndex = 0;
        _preset.SelectedIndexChanged += (_, _) => LoadPreset();
        Controls.AddRange(new Control[] { _preset, _items, _description });
        AddButton("Apply selected tweaks", 24, 430, 190, ApplySelected);
        AddButton("Restore latest backup", 224, 430, 180, RestoreLatest);
        _all.AddRange(new[]
        {
            new Tweak("Disable Windows consumer suggestions", 0, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, "Stops consumer app suggestions and promotional installs."),
            new Tweak("Disable tips and suggested content", 0, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, "Turns off tips and suggestion notifications."),
            new Tweak("Disable Start recommendations", 0, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0, "Hides recommendations in Start where supported by this Windows build."),
            new Tweak("Disable advertising ID", 1, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, "Disables the per-user advertising identifier."),
            new Tweak("Disable tailored experiences", 1, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, "Stops tailored consumer experiences based on diagnostic data."),
            new Tweak("Disable app launch tracking", 1, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0, "Stops Windows tracking app launches for Start suggestions."),
            new Tweak("Disable feedback notifications", 2, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, "Disables periodic feedback prompts."),
            new Tweak("Disable activity history publishing", 2, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "PublishUserActivities", 0, "Disables publishing of user activity history.")
        });
        LoadPreset();
        WriteLog("Presets change current-user settings only. They do not disable Defender, Windows Update, Store, Xbox services, or essential services.");
    }
    private void LoadPreset()
    {
        if (_preset.SelectedItem == null) return;
        int level = _preset.SelectedIndex;
        _items.Items.Clear();
        foreach (var tweak in _all.Where(t => t.Level <= level)) _items.Items.Add(tweak, true);
        _description.Text = level switch
        {
            0 => "Low-impact suggestions, tips and recommendation changes.",
            1 => "Safe settings plus advertising and personalization reductions.",
            _ => "All listed per-user privacy and recommendation changes. Review each item."
        };
    }
    private void ApplySelected()
    {
        var selected = _items.CheckedItems.Cast<Tweak>().ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Select at least one tweak."); return; }
        var name = _preset.SelectedItem?.ToString() ?? "Custom";
        var prompt = "Apply " + selected.Count + " " + name + " tweak(s)?\n\n" + string.Join("\n", selected.Select(t => "• " + t.Name)) + "\n\nA backup of the previous registry values will be saved first.";
        if (MessageBox.Show(this, prompt, "Confirm debloat preset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            Directory.CreateDirectory(_backupFolder);
            var backup = new List<SavedTweak>();
            foreach (var tweak in selected)
            {
                using var key = Registry.CurrentUser.OpenSubKey(tweak.Path);
                var exists = key?.GetValueNames().Contains(tweak.ValueName) == true;
                backup.Add(new SavedTweak { Path = tweak.Path, Name = tweak.ValueName, Exists = exists, Value = exists ? Convert.ToString(key!.GetValue(tweak.ValueName)) : "", Kind = exists ? key!.GetValueKind(tweak.ValueName).ToString() : "DWord" });
                using var writable = Registry.CurrentUser.CreateSubKey(tweak.Path, true);
                writable?.SetValue(tweak.ValueName, tweak.Value, RegistryValueKind.DWord);
                WriteLog("Applied: " + tweak.Name);
            }
            var file = Path.Combine(_backupFolder, "debloat-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            File.WriteAllText(file, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
            WriteLog("Backup saved: " + file);
            MessageBox.Show(this, "Selected tweaks applied.\n\nBackup saved to:\n" + file + "\n\nSome settings may require sign-out or restart.", "Debloat complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { WriteLog("Apply failed: " + ex.Message); MessageBox.Show(this, ex.Message, "Debloat", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void RestoreLatest()
    {
        try
        {
            if (!Directory.Exists(_backupFolder)) { MessageBox.Show(this, "No debloat backup exists."); return; }
            var file = Directory.GetFiles(_backupFolder, "debloat-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (file == null) { MessageBox.Show(this, "No debloat backup exists."); return; }
            if (MessageBox.Show(this, "Restore registry values from:\n" + file + "?", "Restore debloat settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var values = JsonSerializer.Deserialize<List<SavedTweak>>(File.ReadAllText(file)) ?? new();
            foreach (var v in values)
            {
                using var key = Registry.CurrentUser.CreateSubKey(v.Path, true);
                if (key == null) continue;
                if (!v.Exists) key.DeleteValue(v.Name, false);
                else
                {
                    object value = v.Kind == "DWord" && int.TryParse(v.Value, out var number) ? number : v.Value;
                    key.SetValue(v.Name, value, Enum.TryParse<RegistryValueKind>(v.Kind, out var kind) ? kind : RegistryValueKind.String);
                }
            }
            WriteLog("Restored " + values.Count + " registry values from " + file);
            MessageBox.Show(this, "Latest saved debloat registry values restored. Sign out or restart if required.");
        }
        catch (Exception ex) { WriteLog("Restore failed: " + ex.Message); MessageBox.Show(this, ex.Message, "Restore debloat", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private sealed record Tweak(string Name, int Level, string Path, string ValueName, int Value, string Description)
    {
        public override string ToString() => Name + " — " + Description;
    }
    private sealed class SavedTweak { public string Path { get; set; } = ""; public string Name { get; set; } = ""; public bool Exists { get; set; } public string Value { get; set; } = ""; public string Kind { get; set; } = "DWord"; }
}
