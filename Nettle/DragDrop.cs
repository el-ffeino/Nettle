using Gtk;
using Gio;
using Gdk;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nettle;

public static class DragDrop
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi",
        ".wmv", ".m4v", ".flv", ".mpg", ".mpeg"
    };

    public static void AddDragDrop(this Adw.ApplicationWindow window)
    {
        var dropTarget = Gtk.DropTarget.New(GObject.Type.String, Gdk.DragAction.Copy | Gdk.DragAction.Move);
        var SelectedFiles = new List<string>();

        dropTarget.OnDrop += (_, args) =>
        {
            var text = args.Value?.GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var handledAny = false;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    continue;
                }

                Gio.File file = line.Contains("://") 
                    ? Gio.FileHelper.NewForUri(line) 
                    : Gio.FileHelper.NewForPath(line);

                var path = file.GetPath();
                if (path is null)
                {
                    Console.WriteLine($"Dropped non-local file: {file.GetUri()}");
                    continue;
                }

                if (VideoExtensions.Contains(Path.GetExtension(path)))
                {
                    Console.WriteLine($"Video dropped: {path}");
                    handledAny = true;
                    Wallpaper.Import(path);
                }
                else
                {
                    Console.WriteLine($"Ignored non-video: {path}");
                }
            }

            return handledAny;
        };

        window.AddController(dropTarget);
    }
}