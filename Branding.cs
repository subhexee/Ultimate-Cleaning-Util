using System.Runtime.InteropServices;

namespace UltimateCleaningUtility;

internal static class Branding
{
    private static Image? _logo;

    public static Image Logo
    {
        get
        {
            if (_logo != null) return _logo;
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "EXEComputersLogo.png");
            if (!File.Exists(path))
                throw new FileNotFoundException("The EXE COMPUTERS logo asset is missing.", path);
            _logo = Image.FromFile(path);
            return _logo;
        }
    }

    public static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(Logo, new Size(64, 64));
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
