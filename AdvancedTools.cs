using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;

namespace UltimateCleaningUtility;

internal static class AdvancedToolForms
{
    public static void ShowUninstaller(IWin32Window owner) { using var form = new AppUninstallerForm(); form.ShowDialog(owner); }
    public static void ShowDuplicateFinder(IWin32Window owner) { using var form = new DuplicateFinderForm(); form.ShowDialog(owner); }
    public static void ShowGaming(IWin32Window owner) { using var form = new GamingOptimizationForm(); form.ShowDialog(owner); }
    public static void ShowDebloat(IWin32Window owner) { using var form = new DebloatForm(); form.ShowDialog(owner); }
}

internal abstract class AdvancedToolForm : Form
{
    protected readonly RichTextBox LogBox = new();
    protected AdvancedToolForm(string title, string subtitle)
    {
        Text = title + " — Ultimate Cleaning Utility";
        Icon = Branding.CreateIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1050, 760);
        Size = new Size(1120, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(18, 25, 37);
        ForeColor = Color.FromArgb(235, 241, 248);
        var titleLabel = new Label { Text = title, Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = false, Location = new Point(20, 14), Size = new Size(980, 38), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoEllipsis = true };
        var subtitleLabel = new Label { Text = subtitle, ForeColor = Color.FromArgb(151, 166, 185), AutoSize = false, Location = new Point(22, 54), Size = new Size(980, 34), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoEllipsis = true };
        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        LogBox.Location = new Point(20, 0);
        LogBox.Size = new Size(980, 120);
        LogBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        LogBox.ReadOnly = true;
        LogBox.BorderStyle = BorderStyle.None;
        LogBox.BackColor = Color.FromArgb(10, 15, 23);
        LogBox.ForeColor = Color.FromArgb(185, 205, 220);
        LogBox.Font = new Font("Cascadia Mono", 8.5f);
        Controls.Add(LogBox);
        Resize += (_, _) =>
        {
            titleLabel.Width = ClientSize.Width - 40;
            subtitleLabel.Width = ClientSize.Width - 44;
            LogBox.Width = ClientSize.Width - 40;
            LogBox.Height = 120;
            LogBox.Top = ClientSize.Height - LogBox.Height - 18;
        };
        Shown += (_, _) =>
        {
            LogBox.Width = ClientSize.Width - 40;
            LogBox.Top = ClientSize.Height - LogBox.Height - 18;
        };
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
        b.TextAlign = ContentAlignment.MiddleCenter;
        b.AutoEllipsis = true;
        b.Padding = new Padding(5, 0, 5, 0);
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
    private readonly TextBox _search = new() { Location = new Point(20, 94), Width = 610, Height = 28, PlaceholderText = "Search installed applications..." };
    private readonly ListView _apps = new();
    private readonly ListView _leftovers = new();
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(151, 166, 185) };
    private readonly Button _refresh = new() { Text = "Refresh", Location = new Point(20, 350), Size = new Size(92, 34) };
    private readonly Button _uninstall = new() { Text = "Uninstall selected", Location = new Point(120, 350), Size = new Size(155, 34) };
    private readonly Button _scan = new() { Text = "Deep scan leftovers", Location = new Point(285, 350), Size = new Size(160, 34) };
    private readonly Button _remove = new() { Text = "Remove checked", Location = new Point(455, 350), Size = new Size(145, 34) };
    private readonly List<AppEntry> _allApps = new();
    private AppEntry? _selected;
    private bool _busy;

    public AppUninstallerForm() : base("App Uninstaller", "Uninstall desktop apps, then inspect matching leftover folders and registry keys. Every cleanup candidate starts unchecked.")
    {
        _search.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _search.TextChanged += (_, _) => RenderApps();
        Controls.Add(_search);

        _apps.Location = new Point(20, 130);
        _apps.Size = new Size(920, 205);
        _apps.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleList(_apps);
        _apps.MultiSelect = false;
        _apps.HideSelection = false;
        _apps.Columns.Add("Application", 245);
        _apps.Columns.Add("Publisher", 170);
        _apps.Columns.Add("Version", 85);
        _apps.Columns.Add("Installed", 90);
        _apps.Columns.Add("Size", 80);
        _apps.Columns.Add("Install location", 240);
        _apps.SelectedIndexChanged += (_, _) =>
        {
            _selected = _apps.SelectedItems.Count == 0 ? null : _apps.SelectedItems[0].Tag as AppEntry;
            _uninstall.Enabled = _selected != null && !_busy;
            _scan.Enabled = _selected != null && !_busy;
        };
        Controls.Add(_apps);

        _status.Location = new Point(22, 323);
        Controls.Add(_status);

        foreach (var button in new[] { _refresh, _uninstall, _scan, _remove })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(29, 40, 56);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Color.FromArgb(65, 78, 96);
            button.Enabled = button == _refresh;
            Controls.Add(button);
        }
        _refresh.Click += async (_, _) => await LoadAppsAsync();
        _uninstall.Click += (_, _) => UninstallSelected();
        _scan.Click += async (_, _) => await ScanLeftoversAsync();
        _remove.Click += (_, _) => RemoveChecked();

        Controls.Add(new Label
        {
            Text = "LEFTOVERS  ·  Review every path and registry key before removal",
            AutoSize = true,
            Location = new Point(22, 395),
            ForeColor = Color.FromArgb(151, 166, 185),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        });
        _leftovers.Location = new Point(20, 420);
        _leftovers.Size = new Size(920, 65);
        _leftovers.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleList(_leftovers);
        _leftovers.CheckBoxes = true;
        _leftovers.Columns.Add("Candidate", 610);
        _leftovers.Columns.Add("Kind", 115);
        _leftovers.Columns.Add("Details", 180);
        Controls.Add(_leftovers);

        Resize += (_, _) =>
        {
            _search.Width = ClientSize.Width - 40;
            _apps.Width = ClientSize.Width - 40;
            _leftovers.Width = ClientSize.Width - 40;
            _leftovers.Height = Math.Max(45, LogBox.Top - _leftovers.Top - 12);
        };
        Load += async (_, _) => await LoadAppsAsync();
        _uninstall.Enabled = false;
        _scan.Enabled = false;
        _remove.Enabled = false;
    }

    private async Task LoadAppsAsync()
    {
        if (_busy) return;
        SetBusy(true, "Reading installed application registrations...");
        _apps.Items.Clear();
        _allApps.Clear();
        try
        {
            var entries = await Task.Run(ReadInstalledApps);
            _allApps.AddRange(entries);
            RenderApps();
            _status.Text = _allApps.Count + " installed application entries found.";
            WriteLog("Inventory refreshed. Sources: per-user and machine uninstall registrations, 32-bit and 64-bit views.");
        }
        catch (Exception ex)
        {
            _status.Text = "Could not load the application inventory.";
            WriteLog("Inventory error: " + ex.Message);
        }
        finally { SetBusy(false); }
    }

    private static List<AppEntry> ReadInstalledApps()
    {
        var entries = new List<AppEntry>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (root == null) continue;
                foreach (var keyName in root.GetSubKeyNames())
                {
                    try
                    {
                        using var key = root.OpenSubKey(keyName);
                        if (key == null) continue;
                        var name = key.GetValue("DisplayName") as string;
                        var command = key.GetValue("QuietUninstallString") as string ?? key.GetValue("UninstallString") as string;
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command)) continue;
                        if (Convert.ToInt32(key.GetValue("SystemComponent", 0)) == 1) continue;
                        var sizeKb = Convert.ToInt64(key.GetValue("EstimatedSize", 0L));
                        var size = sizeKb > 0 ? FormatBytes(sizeKb * 1024) : "—";
                        var date = key.GetValue("InstallDate") as string ?? "";
                        if (date.Length == 8 && DateTime.TryParseExact(date, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
                            date = parsed.ToString("yyyy-MM-dd");
                        entries.Add(new AppEntry(name.Trim(), key.GetValue("Publisher") as string ?? "",
                            key.GetValue("DisplayVersion") as string ?? "", key.GetValue("InstallLocation") as string ?? "",
                            command, date, size, hive, view, keyName));
                    }
                    catch { }
                }
            }
            catch { }
        }
        return entries
            .GroupBy(e => e.Name + "|" + e.Command, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString("0.#") + " " + units[unit];
    }

    private void RenderApps()
    {
        if (IsDisposed) return;
        var filter = _search.Text.Trim();
        _apps.BeginUpdate();
        _apps.Items.Clear();
        foreach (var entry in _allApps.Where(e => filter.Length == 0 ||
            e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            e.Publisher.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            e.Version.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new ListViewItem(entry.Name);
            row.SubItems.Add(entry.Publisher);
            row.SubItems.Add(entry.Version);
            row.SubItems.Add(entry.InstallDate);
            row.SubItems.Add(entry.Size);
            row.SubItems.Add(entry.Location);
            row.Tag = entry;
            _apps.Items.Add(row);
        }
        _apps.EndUpdate();
        _status.Text = _apps.Items.Count + " of " + _allApps.Count + " applications shown.";
        _uninstall.Enabled = _selected != null && !_busy;
        _scan.Enabled = _selected != null && !_busy;
    }

    private void UninstallSelected()
    {
        var app = _selected;
        if (app == null) { MessageBox.Show(this, "Select an application first."); return; }
        var answer = MessageBox.Show(this,
            "This opens the application's registered uninstaller. Follow its prompts and return here when it finishes.\n\n" +
            app.Name + "\n" + app.Publisher + "\n\nThe utility will not force-close or silently remove the application.",
            "Confirm application uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        try
        {
            var command = Environment.ExpandEnvironmentVariables(app.Command.Trim());
            ProcessStartInfo psi;
            if (command.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                var args = command.Length > 7 ? command[7..].Trim() : "";
                args = System.Text.RegularExpressions.Regex.Replace(args, @"(?i)(^|\s)/I(?=\s*\{)", "$1/X");
                psi = new ProcessStartInfo("msiexec.exe") { UseShellExecute = true, Arguments = args };
            }
            else if (command.StartsWith("\""))
            {
                var endQuote = command.IndexOf('"', 1);
                if (endQuote < 0) throw new InvalidOperationException("The uninstall command contains invalid quotation marks.");
                var exe = command[1..endQuote];
                var args = command[(endQuote + 1)..].Trim();
                psi = new ProcessStartInfo(exe) { UseShellExecute = true, Arguments = args };
            }
            else
            {
                var split = command.IndexOf(' ');
                var exe = split < 0 ? command : command[..split];
                var args = split < 0 ? "" : command[(split + 1)..];
                psi = new ProcessStartInfo(exe) { UseShellExecute = true, Arguments = args };
            }
            Process.Start(psi);
            _status.Text = "Uninstaller launched. Finish its prompts, then select the same app and run Deep scan leftovers.";
            WriteLog("Launched the registered uninstall command for " + app.Name);
        }
        catch (Exception ex)
        {
            WriteLog("Uninstall launch failed: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Uninstaller", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ScanLeftoversAsync()
    {
        var app = _selected;
        if (app == null) { MessageBox.Show(this, "Select the application you have just uninstalled."); return; }
        if (MessageBox.Show(this,
            "Scan common application-data folders and the application-specific Software registry locations for matches to:\n\n" +
            app.Name + "\n\nThis scan is read-only. Results are suggestions and may include shared vendor data.",
            "Deep leftover scan", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

        _leftovers.Items.Clear();
        _remove.Enabled = false;
        SetBusy(true, "Scanning possible leftovers for " + app.Name + "...");
        try
        {
            var candidates = await Task.Run(() => FindLeftovers(app));
            foreach (var candidate in candidates)
            {
                var row = new ListViewItem(candidate.Path);
                row.SubItems.Add(candidate.Kind);
                row.SubItems.Add(candidate.Description);
                row.Tag = candidate;
                row.Checked = false;
                _leftovers.Items.Add(row);
            }
            _status.Text = candidates.Count + " possible leftover(s) found. Review carefully; none are preselected.";
            _remove.Enabled = candidates.Count > 0;
            WriteLog("Deep scan finished: " + candidates.Count + " candidate(s). The scan did not modify files or registry.");
        }
        catch (Exception ex)
        {
            _status.Text = "Leftover scan failed.";
            WriteLog("Leftover scan error: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Leftover scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { SetBusy(false); }
    }

    private static List<LeftoverCandidate> FindLeftovers(AppEntry app)
    {
        var found = new List<LeftoverCandidate>();
        var tokens = GetMatchTokens(app.Name);
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        }.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
         .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(dir);
                    if (MatchesProductName(name, app.Name, tokens))
                        found.Add(new LeftoverCandidate(dir, "Folder", "Application data / install folder", null, null));
                }
            }
            catch { }
        }

        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var software = baseKey.OpenSubKey(@"SOFTWARE");
                if (software == null) continue;
                foreach (var vendorName in software.GetSubKeyNames())
                {
                    try
                    {
                        using var vendor = software.OpenSubKey(vendorName);
                        if (vendor == null) continue;
                        var productKeys = vendor.GetSubKeyNames();
                        if (productKeys.Length == 0 && MatchesProductName(vendorName, app.Name, tokens))
                            found.Add(new LeftoverCandidate(ToRegistryPath(hive, @"SOFTWARE\" + vendorName), "Registry key", hive + " · " + view, hive, view));
                        foreach (var productName in productKeys)
                        {
                            if (!MatchesProductName(productName, app.Name, tokens)) continue;
                            var relative = @"SOFTWARE\" + vendorName + @"\" + productName;
                            found.Add(new LeftoverCandidate(ToRegistryPath(hive, relative), "Registry key", hive + " · " + view, hive, view));
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        return found.GroupBy(x => x.Path + "|" + x.View, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(x => x.Kind, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string[] GetMatchTokens(string name)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "microsoft", "windows", "update", "the", "and", "for", "with", "edition", "version", "redistributable", "runtime", "package", "installer", "setup", "x64", "x86", "64bit", "32bit" };
        return name.Split(new[] { ' ', '-', '_', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && !ignored.Contains(w)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool MatchesProductName(string candidate, string appName, string[] tokens)
    {
        if (candidate.Equals(appName, StringComparison.OrdinalIgnoreCase)) return true;
        if (tokens.Length == 0) return false;
        return tokens.All(token => candidate.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToRegistryPath(RegistryHive hive, string relative) =>
        (hive == RegistryHive.CurrentUser ? "HKCU\\" : "HKLM\\") + relative;

    private void RemoveChecked()
    {
        var rows = _leftovers.CheckedItems.Cast<ListViewItem>().ToList();
        if (rows.Count == 0) { MessageBox.Show(this, "Tick only the leftover candidates you have personally verified."); return; }
        var registryCount = rows.Count(r => (r.Tag as LeftoverCandidate)?.Kind == "Registry key");
        var folderCount = rows.Count - registryCount;
        var answer = MessageBox.Show(this,
            "Move " + folderCount + " folder(s) to the Recycle Bin and remove " + registryCount +
            " registry key(s)?\n\nRegistry keys will first be exported to a dated backup folder under Documents. Shared vendor settings may affect other products. Continue only with entries you have verified.",
            "Confirm leftover cleanup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;

        var backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "EXE COMPUTERS", "Uninstaller Registry Backups", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        foreach (var row in rows)
        {
            if (row.Tag is not LeftoverCandidate candidate) continue;
            try
            {
                if (candidate.Kind == "Folder")
                {
                    if (Directory.Exists(candidate.Path))
                        FileSystem.DeleteDirectory(candidate.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    WriteLog("Sent folder to Recycle Bin: " + candidate.Path);
                }
                else if (candidate.Hive.HasValue && candidate.View.HasValue)
                {
                    Directory.CreateDirectory(backupRoot);
                    var backupFile = Path.Combine(backupRoot, SafeFileName(candidate.Path + "_" + candidate.View) + ".reg");
                    var export = new ProcessStartInfo("reg.exe")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Arguments = "export \"" + candidate.Path + "\" \"" + backupFile + "\" /y " + (candidate.View == RegistryView.Registry32 ? "/reg:32" : "/reg:64")
                    };
                    using var process = Process.Start(export);
                    process?.WaitForExit();
                    if (process == null || process.ExitCode != 0 || !File.Exists(backupFile))
                        throw new InvalidOperationException("Registry export failed; key was not deleted.");
                    var relative = candidate.Path.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)
                        ? candidate.Path[5..] : candidate.Path[5..];
                    using var baseKey = RegistryKey.OpenBaseKey(candidate.Hive.Value, candidate.View.Value);
                    baseKey.DeleteSubKeyTree(relative, false);
                    WriteLog("Backed up and removed registry key: " + candidate.Path);
                }
                _leftovers.Items.Remove(row);
            }
            catch (Exception ex)
            {
                WriteLog("Could not clean " + candidate.Path + ": " + ex.Message);
                MessageBox.Show(this, "Could not clean:\n" + candidate.Path + "\n\n" + ex.Message,
                    "Cleanup item skipped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        if (Directory.Exists(backupRoot))
            WriteLog("Registry backup files saved to: " + backupRoot);
        _status.Text = "Selected cleanup finished. Refresh the inventory to verify uninstall status.";
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
        return name.Length > 110 ? name[^110..] : name;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        _refresh.Enabled = !busy;
        _uninstall.Enabled = !busy && _selected != null;
        _scan.Enabled = !busy && _selected != null;
        _remove.Enabled = !busy && _leftovers.Items.Count > 0;
        _search.Enabled = !busy;
        if (!string.IsNullOrWhiteSpace(message)) _status.Text = message;
    }

    private sealed record AppEntry(string Name, string Publisher, string Version, string Location,
        string Command, string InstallDate, string Size, RegistryHive Hive, RegistryView View, string RegistryKeyName);
    private sealed record LeftoverCandidate(string Path, string Kind, string Description, RegistryHive? Hive, RegistryView? View);
}

internal sealed class DuplicateFinderForm : AdvancedToolForm
{
    private readonly string _userRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private readonly TextBox _folder = new() { Location = new Point(20, 100), Width = 620, Height = 28, Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
    private readonly CheckBox _recursive = new() { Text = "Include subfolders", Checked = true, AutoSize = true, Location = new Point(20, 140) };
    private readonly ComboBox _typeFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175, 136), Width = 150 };
    private readonly Button _clearFilters = new() { Text = "Clear filters", Location = new Point(335, 135), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(29, 40, 56), ForeColor = Color.White };
    private readonly ListView _results = new();
    private readonly Label _summary = new() { Location = new Point(22, 174), AutoSize = true, ForeColor = Color.FromArgb(151, 166, 185) };
    private readonly Button _scan = new() { Text = "Scan", Location = new Point(930, 98), Size = new Size(90, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(29, 40, 56), ForeColor = Color.White };
    private readonly Button _selectCandidates;
    private readonly Button _recycleCandidates;
    private readonly List<DuplicateRecord> _allResults = new();
    private int _sortColumn = 1;
    private bool _sortDescending = true;

    public DuplicateFinderForm() : base("Duplicate File Finder", "Scans user-owned files only. Windows, application, browser-profile and hidden/system folders are excluded.")
    {
        Controls.Add(_folder); Controls.Add(_recursive); Controls.Add(_summary);
        AddButton("Browse", 830, 98, 90, Browse);
        _scan.Click += async (_, _) => await ScanAsync(); Controls.Add(_scan);
        _typeFilter.Items.AddRange(new object[] { "All file types", "Documents", "Images", "Videos", "Audio", "Archives", "Other" });
        _typeFilter.SelectedIndex = 0;
        _typeFilter.SelectedIndexChanged += (_, _) => RenderResults();
        Controls.Add(_typeFilter);
        _clearFilters.Click += (_, _) => ClearFilters();
        Controls.Add(_clearFilters);

        _results.Location = new Point(20, 205); _results.Size = new Size(920, 275); _results.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleList(_results); _results.CheckBoxes = true;
        _results.Columns.Add("File path", 650); _results.Columns.Add("Size", 110); _results.Columns.Add("Group", 80);
        _results.ColumnClick += (_, e) =>
        {
            if (_sortColumn == e.Column) _sortDescending = !_sortDescending;
            else { _sortColumn = e.Column; _sortDescending = e.Column == 1; }
            RenderResults();
        };
        Controls.Add(_results);
        _selectCandidates = AddButton("Select duplicate candidates", 20, 485, 210, SelectDuplicates);
        _recycleCandidates = AddButton("Recycle checked files", 240, 485, 190, RecycleChecked);
        Resize += (_, _) => LayoutDuplicateFinder();
        Shown += (_, _) => LayoutDuplicateFinder();
        LayoutDuplicateFinder();
    }

    private void LayoutDuplicateFinder()
    {
        _folder.Width = Math.Max(220, ClientSize.Width - 350);
        _folder.Top = 100;
        _scan.Left = ClientSize.Width - _scan.Width - 20;
        _results.Width = ClientSize.Width - 40;
        _selectCandidates.Top = LogBox.Top - _selectCandidates.Height - 10;
        _recycleCandidates.Top = _selectCandidates.Top;
        _results.Height = Math.Max(120, _selectCandidates.Top - _results.Top - 12);
    }

    private void Browse()
    {
        using var d = new FolderBrowserDialog { Description = "Choose a folder inside your Windows user profile. Application and hidden/system folders are excluded.", SelectedPath = _userRoot };
        if (d.ShowDialog(this) == DialogResult.OK)
        {
            if (!IsWithinUserProfile(d.SelectedPath))
            {
                MessageBox.Show(this, "For safety, Duplicate Finder scans only folders inside your Windows user profile.", "Folder not allowed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _folder.Text = d.SelectedPath;
        }
    }

    private bool IsWithinUserProfile(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(_userRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!(full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(full, root, StringComparison.OrdinalIgnoreCase))) return false;
        var relative = Path.GetRelativePath(root, full);
        return relative == "." || !relative.Split(Path.DirectorySeparatorChar).Any(part =>
            ProtectedDirectoryNames.Contains(part) || part.StartsWith(".", StringComparison.Ordinal));
    }

    private async Task ScanAsync()
    {
        if (!Directory.Exists(_folder.Text)) { MessageBox.Show(this, "Choose an existing folder."); return; }
        if (!IsWithinUserProfile(_folder.Text))
        {
            MessageBox.Show(this, "For safety, scans are limited to your Windows user profile. Windows and Program Files locations are not scanned.", "Protected location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _scan.Enabled = false; _results.Items.Clear(); _allResults.Clear(); _summary.Text = "Scanning user files and calculating hashes...";
        try
        {
            var groups = await Task.Run(() => ScanDuplicates(_folder.Text, _recursive.Checked));
            int groupNo = 0;
            foreach (var group in groups)
            {
                groupNo++;
                var sorted = group.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                // Keep the first file in each group and list only additional copies as candidates.
                for (int i = 1; i < sorted.Length; i++)
                {
                    try
                    {
                        var info = new FileInfo(sorted[i]);
                        _allResults.Add(new DuplicateRecord(sorted[i], info.Length, groupNo));
                    }
                    catch { }
                }
            }
            RenderResults();
            WriteLog("Scan finished. " + _allResults.Count + " user-file duplicate candidate(s) across " + groups.Count + " groups. Protected folders were skipped.");
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

    private static readonly HashSet<string> ProtectedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppData", "Application Data", "Local Settings", "ProgramData", "Windows", "Program Files",
        "Program Files (x86)", "Packages", "Microsoft", "Google", "Mozilla", "BraveSoftware",
        "Opera Software", "Temp", "Cache", "Caches", "Code Cache", "GPUCache", "Service Worker",
        "CrashDumps", "INetCache", "Cookies", "History", "Profiles"
    };

    private static IEnumerable<string> Enumerate(string root, bool recursive)
    {
        var stack = new Stack<string>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files;
            try { files = Directory.GetFiles(dir); } catch { continue; }
            foreach (var file in files)
            {
                try
                {
                    var attrs = File.GetAttributes(file);
                    if ((attrs & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0) continue;
                }
                catch { continue; }
                yield return file;
                /* file attributes have been checked before yielding */
            }
            if (!recursive) continue;
            string[] dirs;
            try { dirs = Directory.GetDirectories(dir); } catch { continue; }
            foreach (var child in dirs)
            {
                try
                {
                    var attrs = File.GetAttributes(child);
                    var name = Path.GetFileName(child);
                    if ((attrs & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0) continue;
                    if (ProtectedDirectoryNames.Contains(name) || name.StartsWith(".", StringComparison.Ordinal)) continue;
                    stack.Push(child);
                }
                catch { }
            }
        }
    }

    private static string Category(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (new[] { ".doc", ".docx", ".pdf", ".txt", ".rtf", ".odt", ".xls", ".xlsx", ".ppt", ".pptx", ".csv" }.Contains(ext)) return "Documents";
        if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".svg" }.Contains(ext)) return "Images";
        if (new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpeg", ".mpg" }.Contains(ext)) return "Videos";
        if (new[] { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma" }.Contains(ext)) return "Audio";
        if (new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2" }.Contains(ext)) return "Archives";
        return "Other";
    }

    private void RenderResults()
    {
        var filtered = _allResults.AsEnumerable();
        if (_typeFilter.SelectedIndex > 0)
        {
            var wanted = _typeFilter.SelectedItem?.ToString();
            filtered = filtered.Where(x => Category(x.Path) == wanted);
        }
        IOrderedEnumerable<DuplicateRecord> ordered = _sortColumn switch
        {
            0 => _sortDescending ? filtered.OrderByDescending(x => x.Path, StringComparer.OrdinalIgnoreCase) : filtered.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase),
            1 => _sortDescending ? filtered.OrderByDescending(x => x.Size) : filtered.OrderBy(x => x.Size),
            _ => _sortDescending ? filtered.OrderByDescending(x => x.Group) : filtered.OrderBy(x => x.Group)
        };
        _results.BeginUpdate();
        _results.Items.Clear();
        long bytes = 0;
        foreach (var item in ordered)
        {
            var row = new ListViewItem(item.Path);
            row.SubItems.Add(FormatSize(item.Size));
            row.SubItems.Add(item.Group.ToString());
            row.Tag = item.Path;
            _results.Items.Add(row);
            bytes += item.Size;
        }
        _results.EndUpdate();
        _summary.Text = _results.Items.Count + " duplicate candidate(s) shown; " + FormatSize(bytes) + " potentially reclaimable. Click column headings to sort.";
    }

    private void ClearFilters()
    {
        _typeFilter.SelectedIndex = 0;
        _sortColumn = 1;
        _sortDescending = true;
        RenderResults();
    }

    private void SelectDuplicates()
    {
        foreach (ListViewItem row in _results.Items) row.Checked = true;
    }

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
    private sealed record DuplicateRecord(string Path, long Size, int Group);
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
