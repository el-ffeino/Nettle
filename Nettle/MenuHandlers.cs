using Gio;
using Adw;
using Nettle;

namespace Nettle;

public static class MenuHandlers
{
    private static Gio.SimpleAction? PausePlayers;
    private static Gio.SimpleAction? QuitPlayers;

    public static void RegisterHandlers(this Adw.Application app)
    {
        PausePlayers = Gio.SimpleAction.New("pause-wallpapers", null);
        PausePlayers.Enabled = Data.Playing.Count > 0;
        PausePlayers.OnActivate += (_, _) =>
        {
            foreach (var Playing in Data.Playing)
            {
                _ = MPV.Pause(Playing.Token);
            }
        };

        app.AddAction(PausePlayers);

        QuitPlayers = Gio.SimpleAction.New("quit-wallpapers", null);
        QuitPlayers.Enabled = Data.Playing.Count > 0;
        QuitPlayers.OnActivate += (_, _) =>
        {
            foreach (var Playing in Data.Playing.ToList())
            {
                _ = MPV.Quit(Playing.Token);
            }

            Data.Playing.Clear();
            MenuHandlers.Refresh();
            Data.Save();
        };

        app.AddAction(QuitPlayers);

        var Preferences = Gio.SimpleAction.New("preferences", null);

        Preferences.OnActivate += (_, _) =>
        {
            var preferencesDialog = new PreferencesDialog();
            preferencesDialog.Present(App.Window);
        };

        app.AddAction(Preferences);
    }

    public static void Refresh()
    {
        var IsPlaying = Data.Playing.Count > 0;

        PausePlayers!.Enabled = IsPlaying;
        QuitPlayers!.Enabled = IsPlaying;
    }
}