using Gtk;

namespace Nettle;

public class WallpaperContextMenu : Gtk.Popover
{
    public event Action? OnDelete;
    public event Action? OnShowInFiles;
    public event Action? OnSetWallpaper;

    public WallpaperContextMenu()
    {
        HasArrow = false;

        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 5);
        box.MarginBottom = 6;
        box.MarginEnd = 6;
        box.MarginStart = 6;
        box.MarginTop = 6;

        var openButton = Gtk.Button.NewWithLabel("Show in Files");
        openButton.AddCssClass("flat");

        openButton.OnClicked += (_, _) =>
        {
            Popdown();
            OnShowInFiles?.Invoke();
        };

        var setButton = Gtk.Button.NewWithLabel("Set as Wallpaper");
        setButton.AddCssClass("flat");

        setButton.OnClicked += (_, _) =>
        {
            Popdown();
            OnSetWallpaper?.Invoke();
        };

        var deleteButton = Gtk.Button.NewWithLabel("Delete");
        deleteButton.AddCssClass("flat");
        deleteButton.AddCssClass("destructive-action");

        deleteButton.OnClicked += (_, _) =>
        {
            Popdown();
            OnDelete?.Invoke();
        };

        box.Append(setButton);
        box.Append(openButton);
        box.Append(Gtk.Separator.New(Gtk.Orientation.Vertical));
        box.Append(deleteButton);
        Child = box;
    }
}