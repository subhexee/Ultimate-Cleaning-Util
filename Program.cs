using System;
using System.Windows.Forms;

namespace UltimateCleaningUtility;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using (var splash = new SplashScreenForm()) splash.ShowDialog();
        Application.Run(new MainForm());
    }
}
