using Gio;
using Gtk;
using Adw;
using Nettle;

namespace Nettle;

public class MainMenu : Gtk.MenuButton
{
    public MainMenu()
    {
        IconName = "nettle-menu-symbolic";
        // TooltipText = "Main Menu"; // It's annoying

        var Menu = Gio.Menu.New();

        var Playback = Gio.Menu.New();
        Playback.Append("Pause/Resume Wallpaper(s)", "app.pause-wallpapers");
        Playback.Append("Quit Wallpaper(s)", "app.quit-wallpapers");

        var Preferences = Gio.Menu.New();
        Preferences.Append("Preferences", "app.preferences");

        Menu.AppendSection(null, Playback);
        Menu.AppendSection(null, Preferences);

        MenuModel = Menu;
    }
}