using System.IO;
using Gtk;
using Gio;
using Gdk;

namespace Nettle;

public static class Resources
{
    public static void LoadIcons()
    {
        var resourcePath = Path.Combine(AppContext.BaseDirectory, "Nettle.gresource");
        if (System.IO.File.Exists(resourcePath))
        {
            var resource = Gio.Resource.Load(resourcePath);
            Gio.Functions.ResourcesRegister(resource);
        }

        var iconTheme = Gtk.IconTheme.GetForDisplay(Gdk.Display.GetDefault()!);
        iconTheme.AddResourcePath("/io/ffeine/Nettle/icons");
    }

    public static void LoadCss()
    {
        var cssProvider = Gtk.CssProvider.New();

        var resourcePath = Path.Combine(AppContext.BaseDirectory, "style.css");
        var content = System.IO.File.ReadAllText(resourcePath);

        cssProvider.LoadFromString(content);

        Gtk.StyleContext.AddProviderForDisplay(Gdk.Display.GetDefault()!, cssProvider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_APPLICATION);
    }
}