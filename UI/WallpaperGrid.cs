using Gtk;

namespace Nettle;

public class WallpaperGrid : Gtk.FlowBox
{
    public WallpaperGrid()
    {
        SelectionMode = Gtk.SelectionMode.None;
        RowSpacing = 16;
        ColumnSpacing = 16;
        MarginTop = 24;
        MarginBottom = 24;
        MarginStart = 24;
        MarginEnd = 24;
        Hexpand = true;
        Vexpand = true;
        Valign = Align.Start;
        Halign = Align.Fill;
        MinChildrenPerLine = 1;
        MaxChildrenPerLine = 100;
        AddCssClass("wallpaper-grid");
        ActivateOnSingleClick = false;
    }
}