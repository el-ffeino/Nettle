using Gtk;
using Nettle;

namespace Nettle;

public class Import : Gtk.Button
{
    public Import()
    {
        Child = Gtk.Image.NewFromIconName("nettle-plus-large-symbolic");
        TooltipText = "Import video(s)";

        OnClicked += async (_, _) =>
        {
            var files = await FileChooser.Open("Import video(s)", "Import");

            foreach(string file in files)
            {
                Wallpaper.Import(file);
            }
        };
    }
}