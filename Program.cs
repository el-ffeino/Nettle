using Gtk;
using Adw;
using Gdk;
using Gio;
using System.IO;
using Nettle;
using System.Text.Json;

var app = Adw.Application.New("io.ffeine.Nettle", Gio.ApplicationFlags.FlagsNone);

app.OnActivate += (sender, args) =>
{
    if (App.Window is not null)
    {
        App.Window.Present();
        return;
    }

    Resources.LoadIcons();
    Resources.LoadCss();

    var window = new AppWindow((Adw.Application)sender);
    App.Init((Adw.Application)sender, window);

    Data.Load();
    
    Wallpaper.LoadAll();
    AutoPause.Initialize();
    
    window.AddDragDrop();
    window.Present();

    Extension.Enable();
    Extension.InitializeDbus();
};

return app.RunWithSynchronizationContext(null);