using System.IO;
using System.Text;
using Nettle;

namespace Nettle;

public static class AutoStart
{
    private static string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string Config = Path.Combine(Home, ".config");
    private static string Applications = Path.Combine(Home, ".local", "share", "applications");

    private static string desktopFile = "Nettle.desktop";
    private static string exePath = Path.Combine(AppContext.BaseDirectory, "Nettle");
    private static string autoStart = Path.Combine(Config, "autostart");
    private static string autostartFile = Path.Combine(autoStart, desktopFile);
    private static string desktopEntry = Path.Combine(Applications, desktopFile);

    public static void Enable()
    {
        Directory.CreateDirectory(autoStart);
        File.WriteAllText(autostartFile, startupFileContent(), new UTF8Encoding(false));
    }

    public static void Disable()
    {
        if (File.Exists(autostartFile))
        {
            File.Delete(autostartFile);
        }
    }

    public static void AddDesktopEntry()
    {
        if (!File.Exists(desktopEntry))
        {
            File.WriteAllText(desktopEntry, startupFileContent(), new UTF8Encoding(false));
        }
    }

    private static string startupFileContent()
    {
        var Content = $"""
        [Desktop Entry]
        Type=Application
        Name=Nettle
        Comment=Nettle live wallpapers for Gnome
        Exec="{exePath}"
        Terminal=false
        Hidden=false
        X-GNOME-Autostart-enabled=true
        """;

        Content += "\n";

        return Content;
    }
}