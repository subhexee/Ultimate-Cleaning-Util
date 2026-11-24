using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Security.Principal;

namespace UltimateCleaningUtility;

public sealed class MainForm : Form
{
    private readonly FlowLayoutPanel _operations = new();
    private readonly RichTextBox _log = new();
    private readonly Label _status = new();
    private readonly Label _selectionCount = new();
    private readonly Label _spaceLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _runButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _cancelButton = new();
    private readonly List<OperationCard> _cards = new();
    private bool _dark = true;
    private CancellationTokenSource? _runCts;
    private Panel _sidebar = new();
    private Panel _main = new();
    private Panel _console = new();
    private Label _pageTitle = new();

    private readonly Color DarkBg = Color.FromArgb(15, 21, 31);
    private readonly Color DarkPanel = Color.FromArgb(24, 33, 47);
    private readonly Color DarkCard = Color.FromArgb(29, 40, 56);
    private readonly Color DarkText = Color.FromArgb(235, 241, 248);
    private readonly Color DarkMuted = Color.FromArgb(151, 166, 185);
    private readonly Color Accent = Color.FromArgb(30, 184, 126);

    public MainForm()
    {
        Text = "Ultimate Cleaning Utility V3.1";
        MinimumSize = new Size(1050, 720);
        Size = new Size(1280, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BuildShell();
        AddOperations();
        ApplyTheme();
        UpdateSelection();
        Log("READY", "Ultimate Cleaning Utility V3.1 initialized.");
        Log("INFO", "Tick the tweaks you want, then press Run tweaks. Nothing runs automatically.");
        RefreshDiskSpace();
    }

    private void BuildShell()
    {
        _sidebar.Dock = DockStyle.Left;
        _sidebar.Width = 218;
        _sidebar.Padding = new Padding(16);
        Controls.Add(_sidebar);

        var brand = new Panel { Dock = DockStyle.Top, Height = 82 };
        var mark = new Label
        {
            Text = "EX", Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = Color.White, BackColor = Accent, TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(42, 42), Location = new Point(0, 3)
        };
        brand.Controls.Add(mark);
        brand.Controls.Add(new Label { Text = "ULTIMATE", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(53, 5) });
        brand.Controls.Add(new Label { Text = "CLEANING UTILITY", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, Location = new Point(54, 27) });
        brand.Controls.Add(new Label { Text = "V E R S I O N  3 . 1", AutoSize = true, Font = new Font("Segoe UI", 7), ForeColor = Color.Gray, Location = new Point(54, 47) });
        _sidebar.Controls.Add(brand);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 280, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        foreach (var item in new[] { "Overview", "Cleanup", "Repair", "Settings" })
        {
            var b = NavButton(item);
            nav.Controls.Add(b);
            b.Click += (_, _) => ShowPage(item);
        }
        _sidebar.Controls.Add(nav);

        var sideBottom = new Panel { Dock = DockStyle.Bottom, Height = 95 };
        var theme = new Button { Text = "◐   Toggle appearance", Height = 38, Dock = DockStyle.Top, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft };
        theme.FlatAppearance.BorderSize = 0;
        theme.Click += (_, _) => { _dark = !_dark; ApplyTheme(); };
        sideBottom.Controls.Add(theme);
        sideBottom.Controls.Add(new Label { Text = "Made by SubhExe", AutoSize = true, ForeColor = Color.Gray, Location = new Point(3, 52) });
        _sidebar.Controls.Add(sideBottom);

        _main.Dock = DockStyle.Fill;
        _main.Padding = new Padding(26, 22, 26, 14);
        Controls.Add(_main);

        var top = new Panel { Dock = DockStyle.Top, Height = 82 };
        _pageTitle = new Label { Text = "Overview", AutoSize = true, Font = new Font("Segoe UI", 22, FontStyle.Bold), Location = new Point(0, 0) };
        top.Controls.Add(_pageTitle);
        top.Controls.Add(new Label { Text = "A clearer, safer way to maintain Windows.", AutoSize = true, ForeColor = Color.Gray, Location = new Point(2, 42) });
        var version = new Label { Text = "V3.1  •  ADMINISTRATOR", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(800, 12), ForeColor = Accent, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        top.Controls.Add(version);
        _main.Controls.Add(top);

        var summary = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(0, 4, 0, 12) };
        var diskCard = SummaryCard("SYSTEM DRIVE", "C:", "Available storage", 0);
        var selectedCard = SummaryCard("SELECTED OPERATIONS", "0", "Ready to execute", 1);
        var safeCard = SummaryCard("SAFETY", "USER CONTROLLED", "Nothing runs automatically", 2);
        summary.Controls.Add(diskCard);
        summary.Controls.Add(selectedCard);
        summary.Controls.Add(safeCard);
        _main.Controls.Add(summary);

        var contentHeader = new Panel { Dock = DockStyle.Top, Height = 58 };
        contentHeader.Controls.Add(new Label { Text = "Cleanup operations", AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold), Location = new Point(0, 5) });
        contentHeader.Controls.Add(new Label { Text = "Select only the tasks you want to run.", AutoSize = true, ForeColor = Color.Gray, Location = new Point(1, 31) });
        var selectAll = new Button { Text = "Select safe tasks", Width = 130, Height = 32, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(790, 10), FlatStyle = FlatStyle.Flat };
        selectAll.Click += (_, _) => { foreach (var c in _cards.Where(x => x.IsSafeDefault)) c.Selected = true; UpdateSelection(); };
        contentHeader.Controls.Add(selectAll);
        _main.Controls.Add(contentHeader);

        _operations.Dock = DockStyle.Fill;
        _operations.AutoScroll = true;
        _operations.WrapContents = true;
        _operations.FlowDirection = FlowDirection.LeftToRight;
        _operations.Padding = new Padding(0, 2, 6, 8);
        _main.Controls.Add(_operations);

        // Persistent action bar: selections stay visible while browsing the tools.
        var actionBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            Padding = new Padding(12, 8, 12, 8)
        };
        _selectionCount.AutoSize = true;
        _selectionCount.Location = new Point(12, 21);
        _selectionCount.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        actionBar.Controls.Add(_selectionCount);

        var actionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 292,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _clearButton.Text = "Clear tweaks";
        _clearButton.Width = 112;
        _clearButton.Height = 36;
        _clearButton.Margin = new Padding(0, 2, 10, 0);
        _clearButton.FlatStyle = FlatStyle.Flat;
        _clearButton.Click += (_, _) =>
        {
            foreach (var card in _cards) card.Selected = false;
            UpdateSelection();
            Log("SELECTION", "All tweak selections cleared.");
        };
        actionButtons.Controls.Add(_clearButton);

        _runButton.Text = "Run tweaks  →";
        _runButton.Width = 160;
        _runButton.Height = 36;
        _runButton.Margin = new Padding(0, 2, 0, 0);
        _runButton.BackColor = Accent;
        _runButton.ForeColor = Color.White;
        _runButton.FlatStyle = FlatStyle.Flat;
        _runButton.FlatAppearance.BorderSize = 0;
        _runButton.Click += async (_, _) => await RunSelectedAsync();
        actionButtons.Controls.Add(_runButton);
        actionBar.Controls.Add(actionButtons);
        _main.Controls.Add(actionBar);

        _console.Dock = DockStyle.Bottom;
        _console.Height = 220;
        _console.Padding = new Padding(14, 10, 14, 10);
        Controls.Add(_console);

        var consoleHeader = new Panel { Dock = DockStyle.Top, Height = 30 };
        consoleHeader.Controls.Add(new Label { Text = "EXECUTION CONSOLE", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(0, 5) });
        _status.Text = "Ready";
        _status.AutoSize = true;
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _status.Location = new Point(900, 5);
        consoleHeader.Controls.Add(_status);
        _console.Controls.Add(consoleHeader);

        _log.Dock = DockStyle.Fill;
        _log.BorderStyle = BorderStyle.None;
        _log.ReadOnly = true;
        _log.Font = new Font("Cascadia Mono", 9);
        _log.BackColor = Color.FromArgb(10, 15, 23);
        _log.ForeColor = Color.FromArgb(185, 205, 220);
        _console.Controls.Add(_log);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46 };
        _progress.Dock = DockStyle.Left;
        _progress.Width = 260;
        _progress.Height = 24;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Margin = new Padding(0, 8, 12, 0);
        bottom.Controls.Add(_progress);

        _spaceLabel.Text = "Disk space: measuring...";
        _spaceLabel.AutoSize = true;
        _spaceLabel.Location = new Point(275, 13);
        _spaceLabel.ForeColor = Color.Gray;
        bottom.Controls.Add(_spaceLabel);

        _cancelButton.Text = "Cancel";
        _cancelButton.Width = 85;
        _cancelButton.Height = 32;
        _cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _cancelButton.Location = new Point(790, 5);
        _cancelButton.Enabled = false;
        _cancelButton.Click += (_, _) => _runCts?.Cancel();
        bottom.Controls.Add(_cancelButton);

        _console.Controls.Add(bottom);

        // Docked controls are laid out in reverse z-order. Keep the fixed
        // console header/footer outside the fill area occupied by the log.
        _console.Controls.SetChildIndex(_log, 0);
        _console.Controls.SetChildIndex(bottom, 1);
        _console.Controls.SetChildIndex(consoleHeader, 2);

        // Reserve sidebar and console first; the main workspace fills what remains.
        Controls.SetChildIndex(_main, 0);
        Controls.SetChildIndex(_console, 1);
        Controls.SetChildIndex(_sidebar, 2);

        // Keep the three top sections above the operation grid's fill area.
        _main.Controls.SetChildIndex(_operations, 0);
        _main.Controls.SetChildIndex(actionBar, 1);
        _main.Controls.SetChildIndex(contentHeader, 2);
        _main.Controls.SetChildIndex(summary, 3);
        _main.Controls.SetChildIndex(top, 4);
    }

    private Button NavButton(string text) => new()
    {
        Text = "   " + text, Width = 180, Height = 42, TextAlign = ContentAlignment.MiddleLeft,
        FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 3, 0, 3), Font = new Font("Segoe UI", 9.5f)
    };

    private Panel SummaryCard(string heading, string value, string sub, int index)
    {
        var p = new Panel { Width = 275, Height = 78, Location = new Point(index * 290, 0), Padding = new Padding(12) };
        p.Controls.Add(new Label { Text = heading, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, Location = new Point(12, 8) });
        var v = new Label { Text = value, AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold), Location = new Point(12, 26) };
        p.Controls.Add(v);
        p.Controls.Add(new Label { Text = sub, AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = Color.Gray, Location = new Point(13, 54) });
        if (index == 0) _diskValue = v;
        if (index == 1) _selectionValue = v;
        return p;
    }

    private Label? _diskValue;
    private Label? _selectionValue;

    private void AddOperations()
    {
        AddCard("Temporary files", "User temp", "Removes files from your account's temporary folder. Files in use are skipped.", "LOW RISK", true, CleanupEngine.CleanUserTempAsync);
        AddCard("Temporary files", "Windows temp", "Removes eligible files from the Windows temporary folder. Requires administrator access.", "LOW RISK", true, CleanupEngine.CleanWindowsTempAsync);
        AddCard("Caches", "Thumbnail cache", "Removes rebuildable Explorer thumbnail databases. Windows recreates them.", "LOW RISK", true, CleanupEngine.CleanThumbnailCacheAsync);
        AddCard("Caches", "Internet cache", "Clears the current user's legacy Windows INetCache folder.", "LOW RISK", false, CleanupEngine.CleanInternetCacheAsync);
        AddCard("Network", "Flush DNS cache", "Clears the local DNS resolver cache. This does not delete personal data.", "SAFE", false, CleanupEngine.FlushDnsAsync);
        AddCard("Maintenance", "DISM component cleanup", "Runs Microsoft's component-store cleanup operation. It can take several minutes.", "MAINTENANCE", false, CleanupEngine.RunDismCleanupAsync);
        AddCard("Maintenance", "System File Checker", "Scans protected Windows system files and attempts repairs when needed.", "MAINTENANCE", false, CleanupEngine.RunSfcAsync);
        AddCard("Windows Update", "Update download cache", "Not enabled in this first build. Service-aware cleanup will be added after testing.", "COMING SOON", false, null, true);
    }

    private void AddCard(string category, string title, string description, string risk, bool defaultSelected, Func<IProgress<string>, CancellationToken, Task>? action, bool disabled = false)
    {
        var card = new OperationCard(category, title, description, risk, defaultSelected, action, disabled)
        {
            Width = 300, Height = 146, Margin = new Padding(0, 0, 12, 12)
        };
        card.Tag = category;
        card.SelectionChanged += (_, _) => UpdateSelection();
        _cards.Add(card);
        _operations.Controls.Add(card);
    }

    private void ShowPage(string page)
    {
        _pageTitle.Text = page;
        foreach (var card in _cards)
        {
            card.Visible = page switch
            {
                "Cleanup" => Equals(card.Tag, "Temporary files") || Equals(card.Tag, "Caches") || Equals(card.Tag, "Network"),
                "Repair" => Equals(card.Tag, "Maintenance"),
                _ => true
            };
        }
        if (page == "Settings")
            Log("SETTINGS", "Use Toggle appearance in the sidebar to switch themes. All cleanup actions remain manual and opt-in.");
        else
            Log("NAV", $"Showing {page} operations.");
        _operations.PerformLayout();
    }

    private void UpdateSelection()
    {
        var count = _cards.Count(c => c.Selected && !c.Disabled);
        _selectionCount.Text = count == 1 ? "1 tweak selected" : $"{count} tweaks selected";
        if (_selectionValue != null) _selectionValue.Text = count.ToString();
        _runButton.Enabled = count > 0 && _runCts == null;
    }

    private async Task RunSelectedAsync()
    {
        var selected = _cards.Where(c => c.Selected && !c.Disabled && c.Action != null).ToList();
        if (selected.Count == 0) return;
        var names = string.Join(", ", selected.Select(x => x.OperationTitle));
        if (MessageBox.Show($"Run {selected.Count} selected operation(s)?\n\n{names}\n\nSome operations modify Windows caches or system components. Continue?", "Confirm operations", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var runCts = new CancellationTokenSource();
        _runCts = runCts;
        _runButton.Enabled = false;
        _cancelButton.Enabled = true;
        _progress.Value = 0;
        _progress.Maximum = selected.Count;
        _status.Text = "Running...";
        Log("START", $"Starting {selected.Count} operation(s).");

        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                runCts.Token.ThrowIfCancellationRequested();
                var card = selected[i];
                Log("TASK", card.OperationTitle);
                var progress = new Progress<string>(message => Log("INFO", message));
                try
                {
                    await card.Action!(progress, runCts.Token);
                    Log("DONE", $"{card.OperationTitle} completed.");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Log("ERROR", $"{card.OperationTitle}: {ex.Message}"); }
                _progress.Value = Math.Min(i + 1, _progress.Maximum);
            }
            _status.Text = "Completed";
            Log("DONE", "Selected operations finished.");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Cancelled";
            Log("WARN", "Execution cancelled. The current operation may have completed partially.");
        }
        finally
        {
            runCts.Dispose();
            if (ReferenceEquals(_runCts, runCts)) _runCts = null;
            _cancelButton.Enabled = false;
            _runButton.Enabled = true;
            UpdateSelection();
            RefreshDiskSpace();
        }
    }

    private void RefreshDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            var free = drive.AvailableFreeSpace / 1024d / 1024 / 1024;
            _diskValue?.SetText($"{free:0.0} GB");
            _spaceLabel.Text = $"C: {free:0.0} GB available";
        }
        catch { _spaceLabel.Text = "Disk space unavailable"; }
    }

    private void Log(string level, string message)
    {
        if (_log.IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void ApplyTheme()
    {
        BackColor = _dark ? DarkBg : Color.FromArgb(245, 247, 250);
        ForeColor = _dark ? DarkText : Color.FromArgb(30, 39, 52);
        _sidebar.BackColor = _dark ? Color.FromArgb(18, 25, 37) : Color.FromArgb(235, 239, 244);
        _main.BackColor = BackColor;
        _console.BackColor = _dark ? DarkPanel : Color.White;
        foreach (Control c in Controls) ThemeControl(c);
        _sidebar.BackColor = _dark ? Color.FromArgb(18, 25, 37) : Color.FromArgb(235, 239, 244);
        _main.BackColor = _dark ? DarkBg : Color.FromArgb(245, 247, 250);
        _console.BackColor = _dark ? DarkPanel : Color.White;
        _log.BackColor = _dark ? Color.FromArgb(10, 15, 23) : Color.FromArgb(242, 245, 248);
        _log.ForeColor = _dark ? Color.FromArgb(185, 205, 220) : Color.FromArgb(45, 57, 70);
        foreach (var card in _cards) card.SetTheme(_dark, DarkCard, Color.White, DarkText, Color.FromArgb(30, 39, 52), DarkMuted, Accent);
        Invalidate(true);
    }

    private void ThemeControl(Control control)
    {
        if (control is OperationCard) return;
        if (control is Button b)
        {
            if (b == _runButton)
            {
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = Accent;
            }
            else
            {
                b.BackColor = _dark ? DarkCard : Color.FromArgb(232, 237, 243);
                b.ForeColor = _dark ? DarkText : Color.FromArgb(30, 39, 52);
                b.FlatAppearance.BorderColor = _dark ? Color.FromArgb(48, 62, 82) : Color.FromArgb(210, 218, 228);
            }
        }
        else if (control is RichTextBox) { }
        else if (control is Panel && control != _sidebar && control != _main && control != _console)
            control.BackColor = _dark ? DarkPanel : Color.White;
        else if (control is Label l)
            l.ForeColor = _dark ? DarkText : Color.FromArgb(30, 39, 52);
        foreach (Control child in control.Controls) ThemeControl(child);
    }
}

internal sealed class OperationCard : Panel
{
    private readonly CheckBox _check = new();
    private readonly Label _category = new();
    private readonly Label _title = new();
    private readonly Label _description = new();
    private readonly Label _risk = new();
    public event EventHandler? SelectionChanged;
    public string OperationTitle { get; }
    public bool IsSafeDefault { get; }
    public bool Disabled { get; }
    public Func<IProgress<string>, CancellationToken, Task>? Action { get; }
    

[System.ComponentModel.Browsable(false)]
[System.ComponentModel.DesignerSerializationVisibility(
    System.ComponentModel.DesignerSerializationVisibility.Hidden)]
public bool Selected
{
    get => _check.Checked;
    set => _check.Checked = value;
}



    public OperationCard(string category, string title, string description, string risk, bool selected,
        Func<IProgress<string>, CancellationToken, Task>? action, bool disabled)
    {
        OperationTitle = title; IsSafeDefault = selected; Action = action; Disabled = disabled;
        BorderStyle = BorderStyle.FixedSingle;
        Padding = new Padding(12);
        _category.Text = category.ToUpperInvariant();
        _category.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        _category.Location = new Point(12, 10); _category.AutoSize = true;
        _title.Text = title; _title.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _title.Location = new Point(12, 31); _title.AutoSize = true;
        _description.Text = description; _description.Font = new Font("Segoe UI", 8.5f);
        _description.Location = new Point(12, 57); _description.Size = new Size(270, 42);
        _description.AutoEllipsis = true;
        _risk.Text = risk; _risk.Font = new Font("Segoe UI", 7, FontStyle.Bold);
        _risk.Location = new Point(12, 111); _risk.AutoSize = true;
        _check.Text = "";
        _check.AutoSize = false;
        _check.Size = new Size(24, 24);
        _check.Checked = selected;
        _check.Enabled = !disabled;
        _check.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _check.Location = new Point(260, 9);
        _check.FlatStyle = FlatStyle.Standard;
        _check.Cursor = Cursors.Hand;
        _check.CheckedChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        Controls.AddRange(new Control[] { _category, _title, _description, _risk, _check });

        // Clicking the card's text or empty space toggles its visible checkbox.
        Click += (_, _) => ToggleSelection();
        foreach (Control part in new Control[] { _category, _title, _description, _risk })
            part.Click += (_, _) => ToggleSelection();
        if (disabled) _risk.Text = "COMING SOON";
    }

    private void ToggleSelection()
    {
        if (!Disabled && _check.Enabled)
            _check.Checked = !_check.Checked;
    }

    public void SetTheme(bool dark, Color darkCard, Color lightCard, Color darkText, Color lightText, Color muted, Color accent)
    {
        BackColor = dark ? darkCard : lightCard;
        ForeColor = dark ? darkText : lightText;
        _category.ForeColor = accent;
        _title.ForeColor = ForeColor;
        _description.ForeColor = dark ? muted : Color.FromArgb(95, 108, 124);
        _risk.ForeColor = dark ? muted : Color.FromArgb(95, 108, 124);
        _check.ForeColor = ForeColor;
        _check.BackColor = BackColor;
    }
}

internal static class LabelExtensions
{
    public static void SetText(this Label label, string value)
    {
        if (label.InvokeRequired) label.Invoke(() => label.Text = value);
        else label.Text = value;
    }
}
