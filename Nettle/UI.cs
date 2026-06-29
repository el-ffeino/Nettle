using Nettle;

namespace Nettle;

public static class UI
{
    public static void AddWallpaper(string VideoPath)
    {
        var Grid = App.Window.WallpaperGrid;
        var Library = App.Window.LibraryStack;
        var Preview = new Nettle.Video(VideoPath);

        Grid.Append(Preview);

        Library.VisibleChildName = "content";
    }

    public static void RemoveWallpaper(Video Video)
    {
        var Grid = App.Window.WallpaperGrid;
        var Library = App.Window.LibraryStack;

        Grid.Remove(Video);

        if (Data.Wallpapers.Count < 1)
        {
            Library.VisibleChildName = "empty";
        }
    }
}