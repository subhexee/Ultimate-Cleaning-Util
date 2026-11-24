namespace UltimateCleaningUtility;

internal sealed class SplashScreenForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1450 };

    public SplashScreenForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 300);
        BackColor = Color.FromArgb(15, 22, 34);
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;

        var mark = new Label
        {
            Text = "EX",
            Size = new Size(66, 66),
            Location = new Point(247, 42),
            BackColor = Color.FromArgb(30, 184, 126),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 27, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(mark);

        Controls.Add(new Label
        {
            Text = "ULTIMATE CLEANING UTILITY",
            AutoSize = false,
            Width = 520,
            Height = 42,
            Location = new Point(20, 127),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        Controls.Add(new Label
        {
            Text = "POWERED BY EXE COMPUTERS",
            AutoSize = false,
            Width = 520,
            Height = 24,
            Location = new Point(20, 174),
            ForeColor = Color.FromArgb(30, 184, 126),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        Controls.Add(new Label
        {
            Text = "Build it. Fix it. Upgrade it.",
            AutoSize = false,
            Width = 520,
            Height = 22,
            Location = new Point(20, 209),
            ForeColor = Color.FromArgb(151, 166, 185),
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleCenter
        });
        Controls.Add(new Label
        {
            Text = "VERSION 3.4",
            AutoSize = false,
            Width = 520,
            Height = 18,
            Location = new Point(20, 255),
            ForeColor = Color.FromArgb(115, 132, 151),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });

        Shown += (_, _) => _timer.Start();
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
        FormClosed += (_, _) => _timer.Dispose();
    }
}
