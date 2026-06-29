using Gtk;
using Gio;

namespace Nettle;

public static class FileChooser
{
    public static async Task<List<string>> Open(string Title = "Select file(s)", string AcceptLabel = "Accept")
    {
        var selected = new List<string>();

        var dialog = Gtk.FileDialog.New();
        dialog.Title = Title;
        dialog.Modal = true;
        dialog.AcceptLabel = AcceptLabel;

        Gio.ListModel? files;

        try
        {
            files = await dialog.OpenMultipleAsync(App.Window);
        }
        catch (GLib.GException)
        {
            return new List<string>();
        }

        if (files is null)
        {
            return new List<string>();
        }

        for (uint i = 0; i < files.GetNItems(); i++)
        {
            if (files.GetObject(i) is not Gio.File file)
            {
                continue;
            }

            selected.Add(file.GetPath() ?? file.GetUri());
        }

        return selected;
    }
}