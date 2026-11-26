using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Security.Principal;

namespace UltimateCleaningUtility;

public sealed class MainForm : Form
{
    private readonly RealtimeFlowLayoutPanel _operations = new();
    private readonly FlowLayoutPanel _filterBar = new();
    private readonly FlowLayoutPanel _featuredTools = new();
    private string _activeFilter = "All";
    private readonly RichTextBox _log = new();
    private readonly Label _status = new();
    private readonly Label _selectionCount = new();
    private readonly Label _spaceLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _runButton = new();
    private readonly Button _skipButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _cancelButton = new();
    private readonly List<OperationCard> _cards = new();
    private bool _dark = ThemeManager.IsDark;
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _operationCts;
    private readonly TableLayoutPanel _shell = new();
    private Panel _sidebar = new();
    private Panel _main = new();
    private Panel _console = new();
    private Label _pageTitle = new();
    private Label _toolsSectionTitle = new();
    private Button? _startupFilterButton;
    private Button? _startupFeaturedButton;
    private CheckBox? _selectAllButton;
    private bool _updatingSelectAll;
    private StartupManagerForm? _startupForm;

    private readonly Color DarkBg = Color.FromArgb(15, 21, 31);
    private readonly Color DarkPanel = Color.FromArgb(24, 33, 47);
    private readonly Color DarkCard = Color.FromArgb(29, 40, 56);
    private readonly Color DarkText = Color.FromArgb(235, 241, 248);
    private readonly Color DarkMuted = Color.FromArgb(151, 166, 185);
    private readonly Color Accent = Color.FromArgb(30, 184, 126);

    public MainForm()
    {
        Text = "Ultimate Cleaning Utility ReDesigned";
        Icon = Branding.CreateIcon();
        MinimumSize = new Size(1100, 720);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        // Give the General Tools workspace more room on larger displays while
        // keeping the initial window inside the user's available desktop area.
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
        Size = new Size(
            Math.Min(1600, Math.Max(1100, (int)(workArea.Width * 0.94))),
            Math.Min(1000, Math.Max(720, (int)(workArea.Height * 0.94))));
        Font = new Font("Segoe UI", 9.5f);
        BuildShell();
        AddOperations();
        ApplyTheme();
        UpdateSelection();
        Log("READY", "Ultimate Cleaning Utility ReDesigned initialized.");
        Log("INFO", "Tick the tweaks you want, then press Run tweaks. Nothing runs automatically.");
        RefreshDiskSpace();
    }

    private void BuildShell()
    {
        _sidebar.Dock = DockStyle.Fill;
        _sidebar.Padding = new Padding(16);

        var brand = new Panel { Dock = DockStyle.Top, Height = 82 };
        var logo = new PictureBox
        {
            Image = Branding.Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(66, 66),
            Location = new Point(0, 0),
            BackColor = Color.Transparent
        };
        brand.Controls.Add(logo);
        brand.Controls.Add(new Label { Text = "ULTIMATE CLEANING", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(73, 5) });
        brand.Controls.Add(new Label { Text = "UTILITY REDESIGNED", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, Location = new Point(74, 27) });
        brand.Controls.Add(new Label { Text = "VERSION 3.4", AutoSize = true, Font = new Font("Segoe UI", 7), ForeColor = Color.Gray, Location = new Point(74, 47) });
        _sidebar.Controls.Add(brand);

        // Helpful shortcuts and EXE COMPUTERS links sit directly above appearance controls.
        var sideBottom = new Panel { Dock = DockStyle.Bottom, Height = 248 };
        var shortcut = new Button { Text = "＋   Add desktop shortcut", Height = 34, Dock = DockStyle.Top, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft };
        shortcut.FlatAppearance.BorderSize = 0;
        shortcut.Click += (_, _) => CreateDesktopShortcut();
        sideBottom.Controls.Add(shortcut);

        var website = new LinkLabel { Text = "execomputers.in", AutoSize = true, LinkColor = Accent, ActiveLinkColor = Accent, Location = new Point(4, 46), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        website.LinkClicked += (_, _) => OpenExternal("https://execomputers.in");
        sideBottom.Controls.Add(website);
        sideBottom.Controls.Add(new Label { Text = "FOLLOW EXE COMPUTERS", AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), Location = new Point(4, 75) });

        var instagram = new LinkLabel { Text = "Instagram", AutoSize = true, LinkColor = Accent, ActiveLinkColor = Accent, Location = new Point(4, 98) };
        instagram.LinkClicked += (_, _) => OpenExternal("https://instagram.com/execomputers");
        sideBottom.Controls.Add(instagram);
        var youtube = new LinkLabel { Text = "YouTube", AutoSize = true, LinkColor = Accent, ActiveLinkColor = Accent, Location = new Point(4, 122) };
        youtube.LinkClicked += (_, _) => OpenExternal("https://youtube.com/@ExeComputers");
        sideBottom.Controls.Add(youtube);

        var theme = new Button { Text = "◐   Toggle appearance", Height = 38, Dock = DockStyle.Bottom, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft };
        theme.FlatAppearance.BorderSize = 0;
        theme.Click += (_, _) => { _dark = !_dark; ThemeManager.IsDark = _dark; ApplyTheme(); };
        sideBottom.Controls.Add(theme);
        sideBottom.Controls.Add(new Label { Text = "Made by EXE COMPUTERS", AutoSize = true, ForeColor = Color.Gray, Location = new Point(3, 174) });
        _sidebar.Controls.Add(sideBottom);

        _main.Dock = DockStyle.Fill;
        _main.Padding = new Padding(26, 22, 26, 14);

        var top = new Panel { Dock = DockStyle.Top, Height = 82 };
        _pageTitle = new Label { Text = "Overview", AutoSize = true, Font = new Font("Segoe UI", 22, FontStyle.Bold), Location = new Point(0, 0) };
        top.Controls.Add(_pageTitle);
        top.Controls.Add(new Label { Text = "A clearer, safer way to maintain Windows.", AutoSize = true, ForeColor = Color.Gray, Location = new Point(2, 42) });
        var version = new Label { Text = "V3.4  •  ADMINISTRATOR", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(800, 12), ForeColor = Accent, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        top.Controls.Add(version);
        _main.Controls.Add(top);

        var summary = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(0, 4, 0, 12) };
        var diskCard = SummaryCard("SYSTEM DRIVE", "C:", "Available storage", 0);
        var selectedCard = SummaryCard("SELECTED OPERATIONS", "0", "Ready to execute", 1);
        var safeCard = SummaryCard("SAFETY", "USER CONTROLLED", "Nothing runs automatically", 2);
        summary.Controls.Add(diskCard);
        summary.Controls.Add(selectedCard);
        summary.Controls.Add(safeCard);
        void LayoutSummaryCards()
        {
            var gap = 10;
            var cardWidth = Math.Max(190, (summary.ClientSize.Width - gap * 2) / 3);
            var cards = new[] { diskCard, selectedCard, safeCard };
            for (var i = 0; i < cards.Length; i++)
            {
                cards[i].Left = i * (cardWidth + gap);
                cards[i].Width = cardWidth;
            }
        }
        summary.Resize += (_, _) => LayoutSummaryCards();
        LayoutSummaryCards();
        _main.Controls.Add(summary);

        var advancedToolsHeader = new Panel { Dock = DockStyle.Top, Height = 23 };
        advancedToolsHeader.Controls.Add(new Label { Text = "ADVANCED TOOLS", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Accent, Location = new Point(1, 2) });
        _main.Controls.Add(advancedToolsHeader);

        _featuredTools.Dock = DockStyle.Top;
        _featuredTools.Height = 48;
        _featuredTools.FlowDirection = FlowDirection.LeftToRight;
        _featuredTools.WrapContents = false;
        _featuredTools.AutoScroll = true;
        _featuredTools.Padding = new Padding(0, 1, 0, 3);
        _featuredTools.Margin = new Padding(0);
        AddFeaturedTool("App Uninstaller", () => AdvancedToolForms.ShowUninstaller(this));
        AddFeaturedTool("Duplicate Finder", () => AdvancedToolForms.ShowDuplicateFinder(this));
        AddFeaturedTool("Gaming Mode", () => AdvancedToolForms.ShowGaming(this));
        AddFeaturedTool("Windows Debloat", () => AdvancedToolForms.ShowDebloat(this));
        AddFeaturedTool("Bootable USB", () => AdvancedToolForms.ShowUsbCreator(this));
        AddFeaturedTool("Startup Manager", () => LaunchStartupFeatured());
        _main.Controls.Add(_featuredTools);

        var generalToolsHeader = new Panel { Dock = DockStyle.Top, Height = 23 };
        generalToolsHeader.Controls.Add(new Label { Text = "GENERAL TOOLS", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Accent, Location = new Point(1, 2) });
        _main.Controls.Add(generalToolsHeader);

        _filterBar.Dock = DockStyle.Top;
        _filterBar.Height = 44;
        _filterBar.FlowDirection = FlowDirection.LeftToRight;
        _filterBar.WrapContents = false;
        _filterBar.AutoScroll = true;
        _filterBar.Padding = new Padding(0, 2, 0, 4);
        _filterBar.Margin = new Padding(0);
        foreach (var filter in new[] { "All", "Cleanup", "Maintenance", "Repair", "Diagnostics", "Network", "Security", "System Information" })
        {
            var filterButton = new Button
            {
                Name = "tool-filter:" + filter,
                Text = filter,
                Width = filter == "Maintenance" ? 112 : filter == "System Information" ? 142 : filter == "Diagnostics" ? 104 : filter == "Security" ? 88 : 88,
                Height = 32,
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Tag = filter
            };
            filterButton.FlatAppearance.BorderSize = 1;
            filterButton.Click += (_, _) => ApplyToolFilter(filter);
            _filterBar.Controls.Add(filterButton);
        }

        _startupFilterButton = new Button
        {
            Name = "startup-tool",
            Text = "Startup",
            Width = 92,
            Height = 32,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _startupFilterButton.FlatAppearance.BorderSize = 1;
        _startupFilterButton.Click += (_, _) =>
        {
            _startupFilterButton.Text = "– Launching Startup...";
            _startupFilterButton.Enabled = false;
            BeginInvoke(new Action(() => ShowPage("Startup")));
        };
        // Startup Manager is shown with the featured tools above category filters.
        _main.Controls.Add(_filterBar);

        var contentHeader = new Panel { Dock = DockStyle.Top, Height = 58 };
        _toolsSectionTitle = new Label { Text = "All tools", AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold), Location = new Point(0, 5) };
        contentHeader.Controls.Add(_toolsSectionTitle);
        contentHeader.Controls.Add(new Label { Text = "Click tweaks to select them, then run.", AutoSize = true, ForeColor = Color.Gray, Location = new Point(1, 31) });
        _selectAllButton = new CheckBox { Name = "select-all-tools", Text = "Select all", Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, Width = 112, Height = 32, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(808, 10), FlatStyle = FlatStyle.Flat, Checked = false };
        _selectAllButton.CheckedChanged += (_, _) =>
        {
            if (_updatingSelectAll) return;
            SetVisibleSelection(_selectAllButton.Checked);
        };
        contentHeader.Controls.Add(_selectAllButton);
        contentHeader.Resize += (_, _) => _selectAllButton.Left = contentHeader.ClientSize.Width - _selectAllButton.Width;
        _main.Controls.Add(contentHeader);

        _operations.Dock = DockStyle.Fill;
        _operations.AutoScroll = true;
        _operations.HorizontalScroll.Enabled = false;
        _operations.HorizontalScroll.Visible = false;
        _operations.WrapContents = true;
        _operations.Scroll += (_, _) =>
        {
            // Force a paint immediately while the native scrollbar thumb is moving.
            _operations.Invalidate(true);
            _operations.Update();
        };
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
            Width = 424,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _skipButton.Text = "Skip current";
        _skipButton.Width = 120;
        _skipButton.Height = 36;
        _skipButton.Margin = new Padding(0, 2, 10, 0);
        _skipButton.FlatStyle = FlatStyle.Flat;
        _skipButton.Enabled = false;
        _skipButton.Click += (_, _) =>
        {
            if (_operationCts == null || _operationCts.IsCancellationRequested) return;
            Log("SKIP", "Skip requested for the current operation. It will stop when it is safe to do so.");
            _operationCts.Cancel();
        };
        actionButtons.Controls.Add(_skipButton);

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

        _console.Dock = DockStyle.Fill;
        _console.Padding = new Padding(14, 10, 14, 10);

        var consoleHeader = new Panel { Dock = DockStyle.Top, Height = 32 };
        consoleHeader.Controls.Add(new Label { Text = "EXECUTION CONSOLE", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(0, 7) });
        _status.Text = "Ready";
        _status.AutoSize = true;
        _status.Location = new Point(175, 7);
        consoleHeader.Controls.Add(_status);
        // Declare the footer before console button callbacks capture it.
        Panel bottom = new() { Dock = DockStyle.Bottom, Height = 46 };
        Button ConsoleControl(string text, int width, Action action)
        {
            var button = new Button { Text = text, Width = width, Height = 25, Top = 2, Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8) };
            button.Click += (_, _) => action();
            consoleHeader.Controls.Add(button);
            return button;
        }
        var minimizeConsole = ConsoleControl("Minimize", 78, () =>
        {
            // Always move the console out of fullscreen's row-0 / two-row
            // span before shrinking it. Otherwise it continues covering the
            // workspace and its 40px height is applied to the wrong row.
            _consoleFullScreen = false;
            _consoleMinimized = true;
            _shell.SetRowSpan(_console, 1);
            _shell.SetColumnSpan(_console, 2);
            _shell.SetRow(_console, 1);
            _shell.RowStyles[0].SizeType = SizeType.Percent;
            _shell.RowStyles[0].Height = 100;
            _shell.RowStyles[1].SizeType = SizeType.Absolute;
            _shell.RowStyles[1].Height = 40;
            _console.Visible = true;
            _main.Visible = true;
            _sidebar.Visible = true;

            // Keep the header and its controls visible in the compact strip.
            _console.Padding = Padding.Empty;
            bottom.Visible = false;
            _log.Visible = false;
            _console.PerformLayout();
            _shell.PerformLayout();
            consoleHeader.PerformLayout();
        });
        var maximizeConsole = ConsoleControl("Maximize", 82, () =>
        {
            _consoleFullScreen = false;
            _consoleMinimized = false;
            _shell.SetRowSpan(_console, 1);
            _shell.SetColumnSpan(_console, 2);
            _shell.SetRow(_console, 1);
            _shell.RowStyles[0].SizeType = SizeType.Percent;
            _shell.RowStyles[0].Height = 100;
            _shell.RowStyles[1].SizeType = SizeType.Absolute;
            _shell.RowStyles[1].Height = Math.Min(430, Math.Max(260, ClientSize.Height / 2));
            _console.Visible = true;
            _main.Visible = true;
            _sidebar.Visible = true;
            _console.Padding = new Padding(14, 10, 14, 10);
            bottom.Visible = true;
            _log.Visible = true;
            _console.PerformLayout();
            _shell.PerformLayout();
        });
        var fullscreenConsole = ConsoleControl("Fullscreen", 88, () =>
        {
            if (!_consoleFullScreen)
            {
                _consoleFullScreen = true;
                _consoleMinimized = false;
                _console.Padding = new Padding(14, 10, 14, 10);
                bottom.Visible = true;
                _log.Visible = true;
                _main.Visible = false;
                _sidebar.Visible = false;
                _shell.RowStyles[1].Height = 0;
                _shell.SetRow(_console, 0);
                _shell.SetRowSpan(_console, 2);
            }
            else
            {
                _consoleFullScreen = false;
                _consoleMinimized = false;
                _console.Padding = new Padding(14, 10, 14, 10);
                bottom.Visible = true;
                _shell.SetRowSpan(_console, 1);
                _shell.SetRow(_console, 1);
                _shell.RowStyles[1].Height = _consoleExpanded ? 390 : 220;
                _log.Visible = true;
                _main.Visible = true;
                _sidebar.Visible = true;
            }
        });
        consoleHeader.Resize += (_, _) =>
        {
            fullscreenConsole.Left = consoleHeader.ClientSize.Width - fullscreenConsole.Width;
            maximizeConsole.Left = fullscreenConsole.Left - maximizeConsole.Width - 5;
            minimizeConsole.Left = maximizeConsole.Left - minimizeConsole.Width - 5;
        };
        _console.Controls.Add(consoleHeader);

        _log.Dock = DockStyle.Fill;
        _log.BorderStyle = BorderStyle.None;
        _log.ReadOnly = true;
        _log.Font = new Font("Cascadia Mono", 9);
        _log.BackColor = Color.FromArgb(10, 15, 23);
        _log.ForeColor = Color.FromArgb(185, 205, 220);
        _console.Controls.Add(_log);

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

        // Reserve independent areas for navigation, workspace and console.
        // Console resizing cannot change the workspace z-order or cover its controls.
        _shell.Dock = DockStyle.Fill;
        _shell.Margin = Padding.Empty;
        _shell.Padding = Padding.Empty;
        _shell.ColumnCount = 2;
        _shell.RowCount = 2;
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        _shell.Controls.Add(_sidebar, 0, 0);
        _shell.Controls.Add(_main, 1, 0);
        _shell.Controls.Add(_console, 0, 1);
        _shell.SetColumnSpan(_console, 2);
        Controls.Add(_shell);

        // Keep the three top sections above the operation grid's fill area.
        _main.Controls.SetChildIndex(_operations, 0);
        _main.Controls.SetChildIndex(actionBar, 1);
        _main.Controls.SetChildIndex(contentHeader, 2);
        _main.Controls.SetChildIndex(_filterBar, 3);
        _main.Controls.SetChildIndex(generalToolsHeader, 4);
        _main.Controls.SetChildIndex(_featuredTools, 5);
        _main.Controls.SetChildIndex(advancedToolsHeader, 6);
        _main.Controls.SetChildIndex(summary, 7);
        _main.Controls.SetChildIndex(top, 8);
    }

    private void AddFeaturedTool(string title, Action launch)
    {
        var button = new Button { Text = title, Width = title == "Startup Manager" ? 142 : 132, Height = 38,
            Margin = new Padding(0, 0, 8, 0), FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter };
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => launch();
        if (title == "Startup Manager") _startupFeaturedButton = button;
        _featuredTools.Controls.Add(button);
    }

    private void LaunchStartupFeatured()
    {
        if (_startupFeaturedButton == null) return;
        _startupFeaturedButton.Text = "Launching Startup...";
        _startupFeaturedButton.Enabled = false;
        BeginInvoke(new Action(() => ShowPage("Startup")));
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
        AddCard("Temporary files", "User temp", "Removes files from your account's temporary folder. Files in use are skipped.", "LOW RISK", false, CleanupEngine.CleanUserTempAsync);
        AddCard("Temporary files", "Windows temp", "Removes eligible files from the Windows temporary folder. Requires administrator access.", "LOW RISK", false, CleanupEngine.CleanWindowsTempAsync);
        AddCard("Caches", "Thumbnail cache", "Removes rebuildable Explorer thumbnail databases. Windows recreates them.", "LOW RISK", false, CleanupEngine.CleanThumbnailCacheAsync);
        AddCard("Caches", "Internet cache", "Clears the current user's legacy Windows INetCache folder.", "LOW RISK", false, CleanupEngine.CleanInternetCacheAsync);
        AddCard("Caches", "DirectX shader cache", "Removes rebuildable DirectX shader cache files. Windows and games recreate them as needed.", "LOW RISK", false, CleanupEngine.CleanShaderCacheAsync);
        AddCard("Cleanup", "Windows error reports", "Removes queued and archived Windows Error Reporting files. Does not affect Windows system files.", "LOW RISK", false, CleanupEngine.CleanWindowsErrorReportsAsync);
        AddCard("Windows Update", "Windows Update download cache", "Clears downloaded update files after safely stopping and restarting Windows Update services. Updates may need to download again.", "CAUTION", false, CleanupEngine.CleanWindowsUpdateCacheAsync);
        AddCard("Cleanup", "Recycle Bin", "Permanently empties the Recycle Bin on all drives. Review its contents first; this cannot be undone.", "PERMANENT", false, CleanupEngine.EmptyRecycleBinAsync);
        AddCard("Network", "Flush DNS cache", "Clears the local DNS resolver cache. This does not delete personal data.", "SAFE", false, CleanupEngine.FlushDnsAsync);
        AddCard("Maintenance", "Analyze component store", "Reports whether the Windows component store has cleanup recommendations. Does not modify files.", "DIAGNOSTIC", false, CleanupEngine.RunDismAnalyzeAsync);
        AddCard("Maintenance", "DISM component cleanup", "Cleans up superseded Windows component versions. This can take several minutes.", "MAINTENANCE", false, CleanupEngine.RunDismCleanupAsync);
        AddCard("Repair", "DISM RestoreHealth", "Checks and repairs the Windows component store using Windows Update when needed.", "REPAIR", false, CleanupEngine.RunDismRestoreHealthAsync);
        AddCard("Repair", "System File Checker", "Scans protected Windows system files and attempts repairs when needed.", "REPAIR", false, CleanupEngine.RunSfcAsync);
        AddCard("Maintenance", "Check system drive", "Runs an online scan of the Windows system drive without scheduling an offline repair.", "SCAN", false, CleanupEngine.RunDiskScanAsync);
        // BleachBit-inspired cleaners: only rebuildable caches and old crash artifacts.
        AddCard("Caches", "Browser cache", "Clears cache files for Edge, Chrome and Firefox only. Cookies, passwords, history and open sessions are not touched. Close browsers first.", "LOW RISK", false, CleanupEngine.CleanBrowserCachesAsync);
        AddCard("Cleanup", "Delivery Optimization cache", "Clears Windows peer-to-peer update delivery cache using Microsoft's cleanup cmdlet. Windows can recreate it.", "LOW RISK", false, CleanupEngine.CleanDeliveryOptimizationCacheAsync);
        AddCard("Cleanup", "Old crash dumps", "Removes crash dump files older than 30 days. Keeps recent dumps that may help diagnose current problems.", "CAUTION", false, CleanupEngine.CleanOldCrashDumpsAsync);

        AddCard("Cleanup", "Previous Windows installation scan", "Estimates the size of Windows.old and explains rollback implications. Read-only; does not remove anything.", "READ ONLY", false, CleanupEngine.AnalyzePreviousInstallationAsync);
        AddCard("Windows Update", "Remove previous Windows installation", "Asks Windows Disk Cleanup to remove eligible previous-installation files. This can permanently remove the option to roll back to the previous Windows version.", "PERMANENT", false, CleanupEngine.CleanPreviousInstallationAsync);
        AddCard("Cleanup", "Large files finder", "Finds files at least 512 MB in your user profile and temp folder. Exports a CSV report to Desktop; never deletes files.", "READ ONLY", false, CleanupEngine.ScanLargeFilesAsync);
        AddCard("Cleanup", "Storage usage breakdown", "Measures top-level folders in your user profile and exports a size report. Read-only; may take several minutes on large profiles.", "READ ONLY", false, CleanupEngine.AnalyzeStorageUsageAsync);
        AddCard("Cleanup", "Empty folders finder", "Finds empty folders in Desktop, Documents, Downloads, Pictures, Videos and Music. Exports a review-only report; nothing is deleted.", "READ ONLY", false, CleanupEngine.FindEmptyFoldersAsync);
        AddCard("Caches", "Explorer icon cache", "Removes rebuildable Windows Explorer icon cache databases. Icons are recreated automatically; Explorer may need a restart.", "LOW RISK", false, CleanupEngine.CleanExplorerIconCacheAsync);
        AddCard("Temporary files", "Old user temp files", "Removes only files in your user temp folder that have not been modified for at least 7 days. Newer and locked files are kept.", "LOW RISK", false, CleanupEngine.CleanOldUserTempFilesAsync);

        // Read-only diagnostics and reports.
        AddCard("Diagnostics", "Disk health & SMART", "Reads Windows disk health, status and capacity information. SMART availability depends on the drive and controller.", "READ ONLY", false, CleanupEngine.CheckDiskHealthAsync);
        AddCard("Diagnostics", "Battery health report", "Generates a Windows battery report on your Desktop. Useful for laptops; desktop PCs may not have a battery.", "REPORT", false, CleanupEngine.CreateBatteryReportAsync);
        AddCard("Diagnostics", "Startup apps analyzer", "Lists applications registered to start automatically. Does not disable or change anything.", "READ ONLY", false, CleanupEngine.AnalyzeStartupAppsAsync);
        AddCard("Diagnostics", "Event log analyzer", "Shows recent critical and error events from the System and Application logs. Does not clear event logs.", "READ ONLY", false, CleanupEngine.AnalyzeEventLogsAsync);
        AddCard("Diagnostics", "Installed apps inventory", "Lists installed desktop applications from standard Windows uninstall records. Does not uninstall anything.", "READ ONLY", false, CleanupEngine.ListInstalledAppsAsync);
        AddCard("System Information", "Driver inventory", "Lists third-party driver packages registered with Windows using PnPUtil.", "READ ONLY", false, CleanupEngine.ListDriversAsync);
        AddCard("System Information", "Windows activation status", "Checks Windows licensing status and displays only the partial product key. Does not expose a full key.", "READ ONLY", false, CleanupEngine.CheckActivationAsync);
        AddCard("System Information", "System information report", "Creates a detailed MSInfo32 system report on your Desktop.", "REPORT", false, CleanupEngine.CreateSystemInfoReportAsync);
        AddCard("Network", "Network diagnostics", "Collects IP configuration and tests internet reachability and DNS resolution. Does not reset network settings.", "READ ONLY", false, CleanupEngine.RunNetworkDiagnosticsAsync);
        AddCard("Diagnostics", "Energy efficiency report", "Runs Microsoft's powercfg energy analysis for 60 seconds and saves an HTML report to your Desktop. Useful for power and sleep issues.", "REPORT", false, CleanupEngine.CreateEnergyReportAsync);
        AddCard("Diagnostics", "Boot performance analyzer", "Reads Windows Diagnostics-Performance boot events and reports recorded startup delays. Does not change startup settings.", "READ ONLY", false, CleanupEngine.AnalyzeBootPerformanceAsync);
        AddCard("Diagnostics", "Process & memory analyzer", "Lists the highest-memory processes and reports installed/free physical memory. Read-only; no processes are terminated.", "READ ONLY", false, CleanupEngine.AnalyzeProcessesAsync);
        AddCard("Diagnostics", "Network adapter analyzer", "Lists network adapters, link status, MAC addresses, IP configuration and gateway information.", "READ ONLY", false, CleanupEngine.AnalyzeNetworkAdaptersAsync);
        AddCard("Diagnostics", "Windows Update analyzer", "Lists recent hotfixes and common pending-restart indicators. Does not install, remove or reset updates.", "READ ONLY", false, CleanupEngine.AnalyzeWindowsUpdatesAsync);
        AddCard("Diagnostics", "Volumes & disk space report", "Reports local volumes, filesystems, health status, capacity and free space.", "READ ONLY", false, CleanupEngine.AnalyzeVolumesAsync);

        // Maintenance and security tasks.
        AddCard("Maintenance", "Optimize drives", "Runs Windows Optimize Drives with the /O option so Windows chooses the appropriate operation for SSDs or HDDs.", "MAINTENANCE", false, CleanupEngine.OptimizeSystemDriveAsync);
        AddCard("Maintenance", "SSD TRIM status", "Checks whether Windows has TRIM notifications enabled. This is a read-only status check.", "READ ONLY", false, CleanupEngine.CheckTrimStatusAsync);
        AddCard("Maintenance", "Optimize all fixed drives", "Runs Windows Optimize Drives on every detected fixed local volume. Windows chooses TRIM or HDD optimization automatically.", "MAINTENANCE", false, CleanupEngine.OptimizeAllFixedDrivesAsync);
        AddCard("Maintenance", "Filesystem online scan", "Runs CHKDSK /scan on the Windows system drive. It scans online and does not schedule a reboot-time repair.", "SCAN", false, CleanupEngine.ScanFilesystemAsync);
        AddCard("Maintenance", "Verify system files", "Runs SFC /verifyonly to check protected Windows files without attempting repairs.", "DIAGNOSTIC", false, CleanupEngine.VerifySystemFilesAsync);
        AddCard("Maintenance", "DISM component health check", "Performs a quick component-store corruption status check. Does not repair or modify the component store.", "DIAGNOSTIC", false, CleanupEngine.CheckComponentStoreHealthAsync);
        AddCard("Maintenance", "DISM component deep scan", "Performs a deeper component-store corruption scan. This can take several minutes and does not repair automatically.", "DIAGNOSTIC", false, CleanupEngine.ScanComponentStoreHealthAsync);
        AddCard("Repair", "Create restore point", "Requests a Windows system restore point before major changes. Requires System Protection to be enabled; Windows may limit frequency.", "CAUTION", false, CleanupEngine.CreateRestorePointAsync);
        AddCard("Security", "Microsoft Defender quick scan", "Starts a Microsoft Defender quick scan. Availability depends on Defender being enabled and not managed by another antivirus.", "SECURITY", false, CleanupEngine.RunDefenderQuickScanAsync);
        AddCard("Security", "Update Defender signatures", "Requests the latest Microsoft Defender security intelligence updates.", "SECURITY", false, CleanupEngine.UpdateDefenderSignaturesAsync);
        AddCard("Security", "Defender protection status", "Reports Microsoft Defender real-time protection, antivirus service and signature status. Read-only.", "READ ONLY", false, CleanupEngine.CheckDefenderStatusAsync);
        AddCard("Security", "Windows Firewall status", "Reports firewall profile states and default inbound/outbound actions. Does not change firewall rules.", "READ ONLY", false, CleanupEngine.CheckFirewallStatusAsync);
        AddCard("Security", "BitLocker encryption status", "Reports encryption and protection status for available volumes. Does not enable, disable or decrypt BitLocker.", "READ ONLY", false, CleanupEngine.CheckBitLockerStatusAsync);
        AddCard("Security", "SmartScreen configuration", "Inspects common Windows SmartScreen registry and policy settings. This is not a complete security attestation.", "READ ONLY", false, CleanupEngine.CheckSmartScreenStatusAsync);
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
        if (page != "Startup") return;
        try
        {
            if (_startupForm != null && !_startupForm.IsDisposed)
            {
                if (_startupForm.WindowState == FormWindowState.Minimized)
                    _startupForm.WindowState = FormWindowState.Normal;
                _startupForm.Activate();
            }
            else
            {
                _startupForm = new StartupManagerForm();
                _startupForm.FormClosed += (_, _) =>
                {
                    _startupForm = null;
                    ResetStartupLaunchButtons();
                    Log("STARTUP", "Startup manager closed.");
                };
                _startupForm.ShowDialog(this);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not launch Startup Manager.\\n\\n" + ex.Message, "EXE COMPUTERS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            ResetStartupLaunchButtons();
        }
    }

    private void ResetStartupLaunchButtons()
    {
        if (_startupFilterButton != null)
        {
            _startupFilterButton.Text = "Startup Manager";
            _startupFilterButton.Enabled = true;
        }
        if (_startupFeaturedButton != null)
        {
            _startupFeaturedButton.Text = "Startup Manager";
            _startupFeaturedButton.Enabled = true;
        }
    }

    private bool _consoleExpanded;
    private bool _consoleFullScreen;
    private bool _consoleMinimized;

    private static void OpenExternal(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"Could not open the link.\\n\\n{ex.Message}", "Open link", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void CreateDesktopShortcut()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktop, "Ultimate Cleaning Utility.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
            dynamic shell = Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("Could not create the shortcut service.");
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = Application.ExecutablePath;
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.Description = "Ultimate Cleaning Utility by EXE COMPUTERS";
            shortcut.IconLocation = Application.ExecutablePath + ",0";
            shortcut.Save();
            MessageBox.Show("Desktop shortcut created successfully.", "EXE COMPUTERS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not create the desktop shortcut.\\n\\n{ex.Message}", "EXE COMPUTERS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyToolFilter(string filter)
    {
        _activeFilter = filter;
        if (_toolsSectionTitle != null)
            _toolsSectionTitle.Text = filter == "All" ? "All tools" : $"{filter} tools";
        foreach (var card in _cards)
        {
            card.Visible = filter switch
            {
                "All" => true,
                "Cleanup" => Equals(card.Tag, "Temporary files") || Equals(card.Tag, "Caches") || Equals(card.Tag, "Cleanup") || Equals(card.Tag, "Windows Update"),
                "Maintenance" => Equals(card.Tag, "Maintenance"),
                "Repair" => Equals(card.Tag, "Repair"),
                "Network" => Equals(card.Tag, "Network"),
                "Diagnostics" => Equals(card.Tag, "Diagnostics"),
                "Security" => Equals(card.Tag, "Security"),
                "System Information" => Equals(card.Tag, "System Information"),
                _ => true
            };
        }

        RefreshFilterButtonStyles();
        UpdateSelectAllButtonText();
        _operations.PerformLayout();
    }

    private void ToggleVisibleSelection(Button button)
    {
        // Only cards in the active category are affected; the separate Startup
        // manager is intentionally not part of this selection control.
        var visibleCards = _cards.Where(card => card.Visible && !card.Disabled && card.Action != null).ToList();
        if (visibleCards.Count == 0) return;

        var selectAll = visibleCards.Any(card => !card.Selected);
        SetVisibleSelection(selectAll);
    }

    private void SetVisibleSelection(bool selected)
    {
        var visibleCards = _cards.Where(card => card.Visible && !card.Disabled && card.Action != null).ToList();
        foreach (var card in visibleCards) card.Selected = selected;
        UpdateSelection();
    }

    private void UpdateSelectAllButtonText()
    {
        if (_selectAllButton == null) return;
        var visibleCards = _cards.Where(card => card.Visible && !card.Disabled && card.Action != null).ToList();
        var allSelected = visibleCards.Count > 0 && visibleCards.All(card => card.Selected);
        _updatingSelectAll = true;
        _selectAllButton.Checked = allSelected;
        _selectAllButton.Text = allSelected ? "✓ Deselect all" : "☐ Select all";
        _selectAllButton.Enabled = visibleCards.Count > 0;
        _updatingSelectAll = false;
    }

    private void RefreshFilterButtonStyles()
    {
        foreach (Control control in _featuredTools.Controls)
        {
            if (control is Button featured)
            {
                featured.BackColor = _dark ? DarkCard : Color.FromArgb(232, 237, 243);
                featured.ForeColor = _dark ? DarkText : Color.FromArgb(30, 39, 52);
                featured.FlatAppearance.BorderColor = _dark ? Color.FromArgb(48, 62, 82) : Color.FromArgb(210, 218, 228);
            }
        }
        foreach (Control control in _filterBar.Controls)
        {
            if (control is not Button button) continue;
            var selected = Equals(button.Tag, _activeFilter);
            button.BackColor = selected ? Accent : (_dark ? DarkCard : Color.FromArgb(232, 237, 243));
            button.ForeColor = selected ? Color.White : (_dark ? DarkText : Color.FromArgb(30, 39, 52));
            button.FlatAppearance.BorderColor = selected ? Accent : (_dark ? Color.FromArgb(48, 62, 82) : Color.FromArgb(210, 218, 228));
        }
    }

    private void UpdateSelection()
    {
        var count = _cards.Count(c => c.Selected && !c.Disabled);
        _selectionCount.Text = count == 1 ? "1 tweak selected" : $"{count} tweaks selected";
        if (_selectionValue != null) _selectionValue.Text = count.ToString();
        UpdateSelectAllButtonText();
        _runButton.Enabled = count > 0 && _runCts == null;
        _skipButton.Enabled = _operationCts != null && _runCts != null;
    }

    private async Task RunSelectedAsync()
    {
        var selected = _cards.Where(c => c.Selected && !c.Disabled && c.Action != null).ToList();
        if (selected.Count == 0) return;
        var names = string.Join(", ", selected.Select(x => x.OperationTitle));
        var confirmation = $"Run {selected.Count} selected operation(s)?\n\n{names}\n\nSome operations modify Windows caches or system components. Continue?";
        if (selected.Any(x => Equals(x.Tag, "Maintenance") || Equals(x.Tag, "Repair")))
            confirmation += "\n\nWARNING: MAINTENANCE TOOLS WILL TAKE A WHILE TO COMPLETE. PLEASE BE PATIENT.\n\nDo not close the application or interrupt the process unless absolutely necessary. Keep your PC connected to power. Some repair operations may also download files or take considerably longer depending on your system.";
        if (MessageBox.Show(confirmation, "Confirm operations", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var runCts = new CancellationTokenSource();
        _runCts = runCts;
        _runButton.Enabled = false;
        _cancelButton.Enabled = true;
        _skipButton.Enabled = false;
        _progress.Value = 0;
        _progress.Maximum = selected.Count;
        _status.Text = "Running...";
        Log("START", $"Starting {selected.Count} operation(s).");

        var failures = 0;
        var skipped = 0;
        var completed = 0;
        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                runCts.Token.ThrowIfCancellationRequested();
                var card = selected[i];
                Log("TASK", card.OperationTitle);
                var progress = new Progress<string>(message => Log("INFO", message));
                using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(runCts.Token);
                _operationCts = operationCts;
                _skipButton.Enabled = true;
                try
                {
                    await card.Action!(progress, operationCts.Token);
                    if (operationCts.IsCancellationRequested && !runCts.IsCancellationRequested)
                    {
                        skipped++;
                        Log("SKIP", $"{card.OperationTitle} was skipped at the user's request.");
                    }
                    else
                    {
                        completed++;
                        Log("DONE", $"{card.OperationTitle} completed.");
                    }
                }
                catch (OperationCanceledException) when (!runCts.IsCancellationRequested)
                {
                    skipped++;
                    Log("SKIP", $"{card.OperationTitle} was skipped. Any partial changes are noted above.");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    failures++;
                    Log("ERROR", $"{card.OperationTitle}: {ex.Message}");
                }
                finally
                {
                    _operationCts = null;
                    _skipButton.Enabled = false;
                }
                _progress.Value = Math.Min(i + 1, _progress.Maximum);
            }

            _status.Text = failures == 0 && skipped == 0 ? "Completed" : "Completed with issues";
            Log(failures == 0 && skipped == 0 ? "DONE" : "WARN",
                $"Run finished: {completed} completed, {skipped} skipped, {failures} failed.");
            var allSuccessful = failures == 0 && skipped == 0;
            MessageBox.Show(
                allSuccessful
                    ? $"All {selected.Count} selected tweaks have been completed successfully."
                    : $"Run finished. {completed} operation(s) completed, {skipped} skipped, and {failures} failed. Check the execution log for details.",
                allSuccessful ? "All tweaks completed" : "Tweaks completed with issues",
                MessageBoxButtons.OK,
                allSuccessful ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Cancelled";
            Log("WARN", "Run cancelled. The current operation may have completed partially; remaining operations were not started.");
        }
        finally
        {
            _operationCts = null;
            runCts.Dispose();
            if (ReferenceEquals(_runCts, runCts)) _runCts = null;
            _cancelButton.Enabled = false;
            _skipButton.Enabled = false;
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
        RefreshFilterButtonStyles();
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

internal sealed class RealtimeFlowLayoutPanel : FlowLayoutPanel
{
    private const int WmVScroll = 0x0115;
    private const int WmHScroll = 0x0114;
    private const int WmMouseWheel = 0x020A;

    public RealtimeFlowLayoutPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmVScroll || m.Msg == WmHScroll || m.Msg == WmMouseWheel)
        {
            // Update synchronously on every scroll/track message instead of waiting
            // for the next idle paint, keeping the cards in step with the scrollbar.
            Invalidate(true);
            Update();
        }
    }
}

internal sealed class OperationCard : Panel
{
    private readonly Panel _check = new();
    private bool _selected;
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
    get => _selected;
    set
    {
        if (_selected == value) return;
        _selected = value;
        _check.Invalidate();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}



    public OperationCard(string category, string title, string description, string risk, bool selected,
        Func<IProgress<string>, CancellationToken, Task>? action, bool disabled)
    {
        OperationTitle = title; IsSafeDefault = selected; Action = action; Disabled = disabled;
        BorderStyle = BorderStyle.None;
        DoubleBuffered = true;
        Padding = new Padding(12);
        _category.Text = category.ToUpperInvariant();
        _category.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        _category.Location = new Point(12, 10); _category.AutoSize = true;
        _title.Text = title; _title.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _title.Location = new Point(12, 31); _title.Size = new Size(238, 25);
        _title.AutoEllipsis = true;
        _title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _description.Text = description; _description.Font = new Font("Segoe UI", 8.5f);
        _description.Location = new Point(12, 57); _description.Size = new Size(270, 42);
        _description.AutoEllipsis = true;
        _description.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _risk.Text = risk; _risk.Font = new Font("Segoe UI", 7, FontStyle.Bold);
        _risk.Location = new Point(12, 111); _risk.AutoSize = true;
        _selected = selected;
        _check.Size = new Size(24, 24);
        _check.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _check.Location = new Point(Width - _check.Width - 12, 9);
        _check.BackColor = Color.Transparent;
        _check.Cursor = disabled ? Cursors.Default : Cursors.Hand;
        _check.Enabled = !disabled;
        _check.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(4, 4, 16, 16);
            using var fill = new SolidBrush(_selected ? Color.FromArgb(30, 184, 126) : BackColor);
            using var border = new Pen(Color.White, 1.8f);
            e.Graphics.FillRectangle(fill, box);
            e.Graphics.DrawRectangle(border, box);
        };
        _check.Click += (_, _) => ToggleSelection();
        Controls.AddRange(new Control[] { _category, _title, _description, _risk, _check });
        Resize += (_, _) =>
        {
            _check.Left = ClientSize.Width - _check.Width - 12;
            _title.Width = Math.Max(80, ClientSize.Width - 62);
            _description.Width = Math.Max(100, ClientSize.Width - 30);
        };

        // Clicking the card's text or empty space toggles its visible checkbox.
        Click += (_, _) => ToggleSelection();
        foreach (Control part in new Control[] { _category, _title, _description, _risk })
            part.Click += (_, _) => ToggleSelection();
        if (disabled) _risk.Text = "COMING SOON";
    }

    private void ToggleSelection()
    {
        if (!Disabled && _check.Enabled)
            Selected = !Selected;
    }

    public void SetTheme(bool dark, Color darkCard, Color lightCard, Color darkText, Color lightText, Color muted, Color accent)
    {
        BackColor = dark ? darkCard : lightCard;
        ForeColor = dark ? darkText : lightText;
        _category.ForeColor = accent;
        _title.ForeColor = ForeColor;
        _description.ForeColor = dark ? muted : Color.FromArgb(95, 108, 124);
        _risk.ForeColor = dark ? muted : Color.FromArgb(95, 108, 124);
        _check.BackColor = BackColor;
        _check.Invalidate();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var outline = _selected
            ? Color.FromArgb(30, 184, 126)
            : (BackColor.GetBrightness() < 0.5f
                ? Color.FromArgb(65, 78, 96)
                : Color.FromArgb(205, 213, 223));
        using var pen = new Pen(outline, _selected ? 2.2f : 1.2f);
        var bounds = ClientRectangle;
        bounds.Width -= 2;
        bounds.Height -= 2;
        e.Graphics.DrawRectangle(pen, bounds);
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
