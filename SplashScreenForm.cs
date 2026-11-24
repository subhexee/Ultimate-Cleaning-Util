namespace UltimateCleaningUtility;

internal sealed class SplashScreenForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1450 };

    public SplashScreenForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 330);
        BackColor = Color.FromArgb(15, 22, 34);
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Icon = Branding.CreateIcon();

        Controls.Add(new PictureBox
        {
            Image = Branding.Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(132, 132),
            Location = new Point(214, 8),
            BackColor = Color.Transparent
        });

        Controls.Add(new Label
        {
            Text = "ULTIMATE CLEANING UTILITY REDESIGNED",
            AutoSize = false,
            Width = 520,
            Height = 42,
            Location = new Point(20, 145),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        Controls.Add(new Label
        {
            Text = "MADE BY EXE COMPUTERS",
            AutoSize = false,
            Width = 520,
            Height = 24,
            Location = new Point(20, 190),
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
            Location = new Point(20, 222),
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
            Location = new Point(20, 267),
            ForeColor = Color.FromArgb(115, 132, 151),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });

        Shown += (_, _) => _timer.Start();
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
        FormClosed += (_, _) => _timer.Dispose();
    }
}
