using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;

namespace UltimateCleaningUtility;

internal static class ThemeManager
{
    public static bool IsDark { get; set; } = true;
    public static void Apply(Control root)
    {
        bool dark = IsDark;
        Color panel = dark ? Color.FromArgb(24, 33, 47) : Color.White;
        Color card = dark ? Color.FromArgb(29, 40, 56) : Color.FromArgb(232, 237, 243);
        Color fg = dark ? Color.FromArgb(235, 241, 248) : Color.FromArgb(30, 39, 52);
        Color bg = dark ? Color.FromArgb(15, 21, 31) : Color.FromArgb(245, 247, 250);
        ApplyControl(root, bg, panel, card, fg, dark);
    }
    private static void ApplyControl(Control c, Color bg, Color panel, Color card, Color fg, bool dark)
    {
        if (c is Form) { c.BackColor = bg; c.ForeColor = fg; }
        else if (c is ListView lv) { lv.BackColor = panel; lv.ForeColor = fg; }
        else if (c is DataGridView grid) {
            grid.BackgroundColor = panel; grid.DefaultCellStyle.BackColor = panel; grid.DefaultCellStyle.ForeColor = fg;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 115, 88); grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.BackColor = card; grid.ColumnHeadersDefaultCellStyle.ForeColor = fg; grid.EnableHeadersVisualStyles = false;
        }
        else if (c is TextBoxBase tb) { tb.BackColor = panel; tb.ForeColor = fg; }
        else if (c is Button button) { button.BackColor = card; button.ForeColor = fg; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = dark ? Color.FromArgb(65, 78, 96) : Color.FromArgb(205, 214, 225); }
        else if (c is Label label) { label.BackColor = Color.Transparent; label.ForeColor = fg; }
        else if (c is LinkLabel link) { link.LinkColor = Color.FromArgb(30, 184, 126); link.ActiveLinkColor = Color.FromArgb(30, 184, 126); }
        else if (c is Panel || c is GroupBox || c is TabPage) c.BackColor = panel;
        else { c.BackColor = panel; c.ForeColor = fg; }
        foreach (Control child in c.Controls) ApplyControl(child, bg, panel, card, fg, dark);
    }
}
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
        // Advanced tools contain dense inventories and review tables; start larger to avoid cramped controls.
        MinimumSize = new Size(1180, 820);
        Size = new Size(1440, 980);
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
            ThemeManager.Apply(this);
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
    private readonly Button _refresh = new() { Text = "Refresh", Location = new Point(20, 415), Size = new Size(92, 34) };
    private readonly Button _uninstall = new() { Text = "Uninstall selected", Location = new Point(120, 415), Size = new Size(155, 34) };
    private readonly Button _scan = new() { Text = "Deep scan leftovers", Location = new Point(285, 415), Size = new Size(160, 34) };
    private readonly Button _remove = new() { Text = "Remove checked", Location = new Point(455, 415), Size = new Size(145, 34) };
    private readonly Label _leftoversHeading = new() { Text = "LEFTOVERS  ·  Review every path and registry key before removal", AutoSize = true, Location = new Point(22, 460), ForeColor = Color.FromArgb(151, 166, 185), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
    private readonly List<AppEntry> _allApps = new();
    private AppEntry? _selected;
    private bool _busy;

    public AppUninstallerForm() : base("App Uninstaller", "Uninstall desktop apps, then inspect matching leftover folders and registry keys. Every cleanup candidate starts unchecked.")
    {
        _search.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _search.TextChanged += (_, _) => RenderApps();
        Controls.Add(_search);

        _apps.Location = new Point(20, 130);
        _apps.Size = new Size(920, 250);
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
            _uninstall.Enabled = _selected != null && !string.IsNullOrWhiteSpace(_selected.Command) && !_busy;
            _scan.Enabled = _selected != null && !_busy;
        };
        Controls.Add(_apps);

        _status.Location = new Point(22, 388);
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

        Controls.Add(_leftoversHeading);
        _leftovers.Location = new Point(20, 485);
        _leftovers.Size = new Size(920, 120);
        _leftovers.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleList(_leftovers);
        _leftovers.CheckBoxes = true;
        _leftovers.Columns.Add("Candidate", 610);
        _leftovers.Columns.Add("Kind", 115);
        _leftovers.Columns.Add("Details", 180);
        Controls.Add(_leftovers);

        Resize += (_, _) => LayoutUninstaller();
        Shown += (_, _) => LayoutUninstaller();
        Load += async (_, _) => await LoadAppsAsync();
        _uninstall.Enabled = false;
        _scan.Enabled = false;
        _remove.Enabled = false;
    }

    private void LayoutUninstaller()
    {
        if (IsDisposed || ClientSize.Width <= 0) return;
        var contentWidth = Math.Max(700, ClientSize.Width - 40);
        _search.Width = contentWidth;
        _apps.Width = contentWidth;
        _leftovers.Width = contentWidth;

        _apps.Height = 250;
        _status.Top = _apps.Bottom + 8;
        var actionTop = _status.Bottom + 10;
        foreach (var button in new[] { _refresh, _uninstall, _scan, _remove })
            button.Top = actionTop;
        _leftoversHeading.Top = actionTop + 45;
        _leftovers.Top = _leftoversHeading.Bottom + 8;
        _leftovers.Height = Math.Max(110, LogBox.Top - _leftovers.Top - 12);

        var widths = new[]
        {
            (int)(contentWidth * 0.25),
            (int)(contentWidth * 0.17),
            (int)(contentWidth * 0.10),
            (int)(contentWidth * 0.11),
            (int)(contentWidth * 0.09)
        };
        for (int i = 0; i < widths.Length; i++)
            if (_apps.Columns.Count > i) _apps.Columns[i].Width = widths[i];
        if (_apps.Columns.Count > 5)
            _apps.Columns[5].Width = Math.Max(150, contentWidth - widths.Sum());

        if (_leftovers.Columns.Count >= 3)
        {
            _leftovers.Columns[0].Width = (int)(contentWidth * 0.66);
            _leftovers.Columns[1].Width = (int)(contentWidth * 0.13);
            _leftovers.Columns[2].Width = contentWidth - _leftovers.Columns[0].Width - _leftovers.Columns[1].Width;
        }
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
                        var command = key.GetValue("UninstallString") as string ?? key.GetValue("QuietUninstallString") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        command ??= "";
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
        _uninstall.Enabled = _selected != null && !string.IsNullOrWhiteSpace(_selected.Command) && !_busy;
        _scan.Enabled = _selected != null && !_busy;
    }

    private async void UninstallSelected()
    {
        var app = _selected;
        if (app == null) { MessageBox.Show(this, "Select an application first."); return; }
        if (string.IsNullOrWhiteSpace(app.Command))
        {
            MessageBox.Show(this, "This application has no registered uninstall command. Use the forced leftover scan to inspect its files and registry entries.", "Uninstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(this,
            "EXE COMPUTERS will launch the application's uninstall command. Save your work in the application first.\n\n" +
            app.Name + "\n" + app.Publisher + "\n\nThe application will only be marked as uninstalled after its uninstall registration disappears.",
            "Confirm application uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;

        try
        {
            var psi = BuildUninstallStartInfo(app);
            if (app.Hive == RegistryHive.LocalMachine && !IsAdministrator())
                psi.Verb = "runas";

            _uninstall.Enabled = false;
            _refresh.Enabled = false;
            _scan.Enabled = false;
            _status.Text = "Starting the registered uninstaller for " + app.Name + "...";
            WriteLog("Starting uninstall command: " + psi.FileName + " " + psi.Arguments);

            using var uninstallProcess = Process.Start(psi);
            if (uninstallProcess == null)
                throw new InvalidOperationException("Windows did not return an uninstall process. Check permissions and the registered uninstall command.");

            _status.Text = "Uninstaller is running. Complete its prompts; this window will verify the result...";
            await uninstallProcess.WaitForExitAsync();
            var exitCode = uninstallProcess.ExitCode;
            WriteLog("Uninstaller process exited for " + app.Name + " (exit code " + exitCode + "). Verifying the installed-app registration...");

            // Some vendor launchers exit before their child uninstaller finishes. Give the
            // uninstall registration time to disappear instead of treating process exit as success.
            var removed = false;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                if (IsDisposed) return;
                removed = await Task.Run(() => !IsRegistrationPresent(app));
                if (removed) break;
                await Task.Delay(1000);
            }

            if (IsDisposed) return;
            if (!removed)
            {
                _status.Text = "Uninstall was not verified — the application is still registered.";
                WriteLog("Uninstall not verified. The uninstall registry entry still exists; the app may have opened Modify/Repair mode or the vendor uninstaller may have failed.");
                MessageBox.Show(this,
                    "The uninstaller process closed, but Windows still lists " + app.Name + " as installed.\n\n" +
                    "This is not being reported as a successful uninstall. The vendor uninstaller may have opened Modify/Repair mode, failed, or started another process.\n\n" +
                    "Try again using the application's own uninstall option, then refresh this inventory.",
                    "Uninstallation not verified", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await LoadAppsAsync();
                return;
            }

            _status.Text = "Uninstallation verified for " + app.Name + ".";
            WriteLog("Uninstallation verified: the application's registered uninstall entry was removed.");
            if (ShowUninstallCompletedPrompt(app))
                await ScanLeftoversAsync(app);
            else
                await LoadAppsAsync();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _status.Text = "Uninstall cancelled.";
            WriteLog("Uninstall cancelled at the Windows permission prompt.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _status.Text = "Uninstall could not be started or verified.";
                WriteLog("Uninstall failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Uninstaller", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                _refresh.Enabled = !_busy;
                _uninstall.Enabled = _selected != null && !string.IsNullOrWhiteSpace(_selected.Command) && !_busy;
                _scan.Enabled = _selected != null && !_busy;
            }
        }
    }

    private static ProcessStartInfo BuildUninstallStartInfo(AppEntry app)
    {
        var command = Environment.ExpandEnvironmentVariables(app.Command.Trim());
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("This application has no registered uninstall command.");

        string exe;
        string args;
        if (command.StartsWith("\""))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote < 0)
                throw new InvalidOperationException("The registered uninstall command has invalid quotation marks.");
            exe = command[1..endQuote];
            args = command[(endQuote + 1)..].Trim();
        }
        else
        {
            // Unquoted paths can contain spaces. The first .exe boundary is a safer
            // executable delimiter than splitting at the first whitespace.
            var exeEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeEnd >= 0)
            {
                exeEnd += 4;
                exe = command[..exeEnd].Trim();
                args = command[exeEnd..].Trim();
            }
            else
            {
                var split = command.IndexOf(' ');
                exe = split < 0 ? command : command[..split];
                args = split < 0 ? "" : command[(split + 1)..].Trim();
            }
        }

        var isMsi = string.Equals(Path.GetFileNameWithoutExtension(exe), "msiexec", StringComparison.OrdinalIgnoreCase);
        if (isMsi)
        {
            // Use the product code from the registry subkey whenever possible. This
            // avoids accidentally opening Windows Installer's Modify/Repair workflow.
            var productCode = Guid.TryParse(app.RegistryKeyName, out var registryGuid)
                ? registryGuid.ToString("B")
                : System.Text.RegularExpressions.Regex.Match(args, @"\{[0-9A-Fa-f-]{36}\}").Value;
            if (string.IsNullOrWhiteSpace(productCode))
                throw new InvalidOperationException("This Windows Installer entry has no readable product code. The uninstall command was not run.");
            exe = "msiexec.exe";
            args = "/x " + productCode;
        }

        if (!File.Exists(exe) && !string.Equals(Path.GetFileName(exe), "msiexec.exe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("The registered uninstaller executable could not be found.", exe);

        return new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = File.Exists(exe) ? Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory : Environment.CurrentDirectory
        };
    }

    private static bool IsRegistrationPresent(AppEntry app)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(app.Hive, app.View);
            using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            using var entry = uninstall?.OpenSubKey(app.RegistryKeyName);
            return entry != null;
        }
        catch { return true; }
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private bool ShowUninstallCompletedPrompt(AppEntry app)
    {
        using var prompt = new Form
        {
            Text = "Uninstallation completed",
            Icon = Branding.CreateIcon(),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(440, 165),
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            BackColor = BackColor,
            ForeColor = ForeColor,
            Font = Font
        };
        prompt.Controls.Add(new Label
        {
            Text = "The uninstaller has closed for:\n" + app.Name + "\n\nWould you like to scan for leftover files and registry entries?",
            Location = new Point(18, 16),
            Size = new Size(404, 78),
            ForeColor = ForeColor
        });
        var scanButton = new Button
        {
            Text = "Scan leftovers",
            Location = new Point(190, 112),
            Size = new Size(130, 34),
            DialogResult = DialogResult.Yes,
            BackColor = Color.FromArgb(35, 105, 75),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(330, 112),
            Size = new Size(90, 34),
            DialogResult = DialogResult.No,
            BackColor = Color.FromArgb(29, 40, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        prompt.Controls.Add(scanButton);
        prompt.Controls.Add(closeButton);
        prompt.AcceptButton = scanButton;
        prompt.CancelButton = closeButton;
        return prompt.ShowDialog(this) == DialogResult.Yes;
    }

    private async Task ScanLeftoversAsync(AppEntry? target = null)
    {
        var app = target ?? _selected;
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
        _uninstall.Enabled = !busy && _selected != null && !string.IsNullOrWhiteSpace(_selected.Command);
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
    private readonly Button _browse;
    private readonly Button _selectCandidates;
    private readonly Button _recycleCandidates;
    private readonly List<DuplicateRecord> _allResults = new();
    private int _sortColumn = 1;
    private bool _sortDescending = true;

    public DuplicateFinderForm() : base("Duplicate File Finder", "Scans user-owned files only. Windows, application, browser-profile and hidden/system folders are excluded.")
    {
        Controls.Add(_folder); Controls.Add(_recursive); Controls.Add(_summary);
        _browse = AddButton("Browse", 830, 98, 90, Browse);
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
        _folder.Top = 100;
        _scan.Left = ClientSize.Width - _scan.Width - 20;
        _browse.Left = _scan.Left - _browse.Width - 10;
        _folder.Width = Math.Max(220, _browse.Left - _folder.Left - 10);
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
        "CrashDumps", "INetCache", "Cookies", "History", "Profiles", "Steam", "Epic Games",
        "Riot Games", "Battle.net", "XboxGames", "GOG Galaxy", "Ubisoft Game Launcher",
        "EA Games", "Electronic Arts", "Rockstar Games", "NVIDIA", "AMD", "Intel"
    };

    // Restrict cleanup candidates to personal content, never executable or application payloads.
    private static readonly HashSet<string> ExcludedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msp", ".msix", ".appx", ".appxbundle", ".dll", ".sys", ".drv",
        ".ocx", ".com", ".scr", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".js", ".wsf",
        ".reg", ".lnk", ".url", ".crx", ".xpi", ".sqlite", ".sqlite3", ".db", ".dat",
        ".tmp", ".cache", ".log", ".etl", ".cab", ".iso", ".img", ".vhd", ".vhdx"
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
                    if (ExcludedFileExtensions.Contains(Path.GetExtension(file))) continue;
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
        _results.Columns[0].Text = _sortColumn == 0 ? "File path " + (_sortDescending ? "▼" : "▲") : "File path";
        _results.Columns[1].Text = _sortColumn == 1 ? "Size " + (_sortDescending ? "▼" : "▲") : "Size";
        _results.Columns[2].Text = _sortColumn == 2 ? "Group " + (_sortDescending ? "▼" : "▲") : "Group";
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
    private readonly ComboBox _preset = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(24, 105), Width = 250 };
    private readonly CheckedListBox _items = new() { Location = new Point(24, 158), Size = new Size(900, 500), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, CheckOnClick = true, IntegralHeight = false };
    private readonly Label _description = new() { Location = new Point(292, 109), Size = new Size(900, 40), ForeColor = Color.FromArgb(151, 166, 185), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
    private readonly List<Tweak> _all = new();
    private readonly string _backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ultimate Cleaning Utility", "DebloatBackups");
    private readonly Button _apply;
    private readonly Button _restore;
    private readonly Button _selectAll;
    private bool _allSelected;

    public DebloatForm() : base("Windows Debloat", "CTT-inspired Windows privacy and quality-of-life tweaks. Registry changes are limited to the current user and backed up for restoration.")
    {
        _preset.Items.AddRange(new object[] { "Minimal", "Standard (Recommended)", "Advanced (Safe only)" });
        _preset.SelectedIndex = 1;
        _preset.SelectedIndexChanged += (_, _) => LoadPreset();
        Controls.AddRange(new Control[] { _preset, _items, _description });

        _selectAll = AddButton("Select all", 24, 700, 120, ToggleAll);
        _apply = AddButton("Apply selected tweaks", 154, 700, 190, ApplySelected);
        _restore = AddButton("Restore latest backup", 354, 700, 190, RestoreLatest);

        _all.AddRange(new[]
        {
            // Minimal: low-impact, reversible suggestion controls.
            new Tweak("Disable Windows consumer suggestions", 0, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, "Stops a common source of promoted app suggestions."),
            new Tweak("Disable tips and suggested content", 0, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, "Turns off Windows tips and suggestion notifications."),
            new Tweak("Disable Start recommendations", 0, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0, "Hides Start recommendations where supported by this Windows version."),

            // Standard: privacy, telemetry personalization and user-facing promotions.
            new Tweak("Disable advertising ID", 1, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, "Disables the per-user advertising identifier."),
            new Tweak("Disable tailored experiences", 1, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, "Stops personalized consumer experiences based on diagnostic data."),
            new Tweak("Disable app launch tracking", 1, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0, "Stops Windows tracking app launches for Start suggestions."),
            new Tweak("Disable feedback prompts", 1, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, "Disables periodic Windows feedback prompts."),
            new Tweak("Disable activity history publishing", 1, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "PublishUserActivities", 0, "Stops publishing user activity history; clipboard history is not changed."),
            new Tweak("Disable lock-screen Spotlight suggestions", 1, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled", 0, "Stops promotional tips and overlays on the lock screen."),
            new Tweak("Disable lock-screen Spotlight content", 1, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenEnabled", 0, "Disables rotating Spotlight backgrounds for this user."),
            new Tweak("Disable Start app suggestions", 1, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs", 0, "Stops Windows using recent documents to personalize Start and jump-list suggestions."),
            new Tweak("Disable Search highlights", 1, @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0, "Turns off changing Search box highlights and promotional content."),
            new Tweak("Disable Bing web results in Start search", 1, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, "Keeps Start search focused on local results; web results may no longer appear."),

            // Advanced safe subset: optional interface preferences, not service or security changes.
            new Tweak("Disable Windows welcome experience", 2, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0, "Stops the first-sign-in welcome experience and feature suggestions."),
            new Tweak("Disable suggested content in Settings", 2, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0, "Hides suggested content in the Settings app where supported."),
            new Tweak("Disable tips after Windows updates", 2, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0, "Stops post-update tips and recommendation prompts."),
            new Tweak("Disable File Explorer sync-provider ads", 2, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0, "Hides promotional notifications in File Explorer."),
            new Tweak("Hide Widgets button on taskbar", 2, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0, "Hides the Widgets taskbar button without uninstalling the Widgets app."),
            new Tweak("Hide Task View button on taskbar", 2, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", 0, "Hides the Task View taskbar button; virtual desktops remain available."),
            new Tweak("Enable End Task in taskbar menu", 2, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", 1, "Adds the End Task command to the taskbar right-click menu on supported Windows builds."),
            new Tweak("Disable Copilot button and entry point", 2, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, "Hides/disables the Windows Copilot entry point for this user where policy is honored.")
        });

        _preset.SelectedIndex = 1;
        LoadPreset();
        Resize += (_, _) => LayoutDebloat();
        Shown += (_, _) => LayoutDebloat();
        WriteLog("Safety scope: current-user registry preferences only. No Defender, Windows Update, Store, Xbox, networking, or essential services are disabled. Every original value is backed up before applying.");
    }

    private void LayoutDebloat()
    {
        if (IsDisposed || ClientSize.Width <= 0) return;
        var width = Math.Max(600, ClientSize.Width - 48);
        _items.Width = width;
        _description.Width = Math.Max(260, ClientSize.Width - _description.Left - 24);
        var actionTop = LogBox.Top - 48;
        _selectAll.Top = actionTop;
        _apply.Top = actionTop;
        _restore.Top = actionTop;
        _items.Height = Math.Max(180, actionTop - _items.Top - 12);
        _restore.Left = Math.Max(_apply.Right + 12, ClientSize.Width - _restore.Width - 24);
    }

    private void ToggleAll()
    {
        _allSelected = !_allSelected;
        for (var i = 0; i < _items.Items.Count; i++)
            _items.SetItemChecked(i, _allSelected);
        _selectAll.Text = _allSelected ? "Clear all" : "Select all";
    }

    private void LoadPreset()
    {
        if (_preset.SelectedItem == null) return;
        int level = _preset.SelectedIndex;
        _items.Items.Clear();
        foreach (var tweak in _all.Where(t => t.Level <= level))
            _items.Items.Add(tweak, true);
        _allSelected = true;
        _selectAll.Text = "Clear all";
        _description.Text = level switch
        {
            0 => "Minimal: a small set of low-impact Windows suggestion and recommendation controls.",
            1 => "Standard: recommended privacy, personalization, telemetry prompts and suggestion reductions.",
            _ => "Advanced safe subset: Standard plus optional taskbar, Search, Explorer and Copilot preferences. Review each choice."
        };
        LayoutDebloat();
    }

    private void ApplySelected()
    {
        var selected = _items.CheckedItems.Cast<Tweak>().ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Select at least one tweak."); return; }
        var name = _preset.SelectedItem?.ToString() ?? "Custom";
        var prompt = "Apply " + selected.Count + " " + name + " tweak(s)?\n\n" +
            string.Join("\n", selected.Select(t => "• " + t.Name)) +
            "\n\nOriginal registry values will be backed up first. Some changes may require restarting Explorer or signing out.";
        if (MessageBox.Show(this, prompt, "Confirm debloat tweaks", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            Directory.CreateDirectory(_backupFolder);
            var backup = new List<SavedTweak>();
            foreach (var tweak in selected)
            {
                using var key = Registry.CurrentUser.OpenSubKey(tweak.Path);
                var exists = key?.GetValueNames().Contains(tweak.ValueName) == true;
                backup.Add(new SavedTweak
                {
                    Path = tweak.Path,
                    Name = tweak.ValueName,
                    Exists = exists,
                    Value = exists ? Convert.ToString(key!.GetValue(tweak.ValueName)) : "",
                    Kind = exists ? key!.GetValueKind(tweak.ValueName).ToString() : "DWord"
                });
            }

            var file = Path.Combine(_backupFolder, "debloat-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            File.WriteAllText(file, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));

            foreach (var tweak in selected)
            {
                using var writable = Registry.CurrentUser.CreateSubKey(tweak.Path, true);
                writable?.SetValue(tweak.ValueName, tweak.Value, RegistryValueKind.DWord);
                WriteLog("Applied: " + tweak.Name);
            }
            WriteLog("Backup saved: " + file);
            MessageBox.Show(this, "Selected tweaks applied.\n\nBackup saved to:\n" + file + "\n\nSome settings may require Explorer restart, sign-out or reboot.", "Debloat complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                WriteLog("Restored: " + v.Path + @"\" + v.Name);
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
