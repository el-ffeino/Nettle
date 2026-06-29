using Gtk;
using Gdk;
using GdkPixbuf;
using System.IO;
using Nettle;

namespace Nettle;

public class Video : Gtk.Overlay
{
    public Video(string VideoPath)
    {
        var image = Thumbnailer.Create(VideoPath);
        var pixbuf = Pixbuf.NewFromFileAtScale(image, Thumbnailer.Width, Thumbnailer.Height, false);
        var texture = Gdk.Texture.NewForPixbuf(pixbuf!);
        var img = Gtk.Picture.NewForPaintable(texture);

        img.SetSizeRequest(Thumbnailer.Width, Thumbnailer.Height);
        img.Hexpand = true;
        img.Vexpand = true;
        img.Halign = Align.Fill;
        img.Valign = Align.Fill;
        img.CssClasses = new string[] { "rounded", "video-picture" };

        SetSizeRequest(Thumbnailer.Width, Thumbnailer.Height);
        Hexpand = false;
        Vexpand = false;
        Halign = Align.Start;
        Valign = Align.Start;
        Child = img;
        AddCssClass("video-thumb");
        
        var hover = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
        hover.Hexpand = true;
        hover.Vexpand = true;
        hover.Halign = Align.Fill;
        hover.Valign = Align.Fill;
        hover.CanTarget = false;
        hover.AddCssClass("video-hover");

        var playIcon = Gtk.Image.NewFromIconName("nettle-play-symbolic");
        playIcon.Halign = Align.Center;
        playIcon.Valign = Align.Center;
        playIcon.Hexpand = true;
        playIcon.Vexpand = true;
        playIcon.AddCssClass("video-play-icon");

        hover.Append(playIcon);
        AddOverlay(hover);

        var OnClick = Gtk.GestureClick.New();
        OnClick.SetButton(1);
        OnClick.OnPressed += async (_, _) =>
        {
            var AvailableDisplays = Data.Displays.Count;
            var SelectedDisplays = new List<Display>();

            if (AvailableDisplays < 1)
            {
                Console.WriteLine($"(Video.cs) Video[Onclick.OnPressed]: You don't have any displays to play this on");
                return;
            }

            SelectedDisplays.Add(Data.Displays[0]);

            if (AvailableDisplays > 1)
            {
                SelectedDisplays = await DisplayChooser.Open();
            }

            foreach (var Display in SelectedDisplays)
            {
                _ = MPV.Play(VideoPath, Display);
            }
        };

        var contextMenu = new WallpaperContextMenu();

        contextMenu.OnDelete += () =>
        {
            this.Remove(VideoPath);
        };

        contextMenu.OnShowInFiles += () =>
        {
            Nautilus.Show(VideoPath);
        };

        contextMenu.OnSetWallpaper += async () =>
        {
            // This can be turned into a function as it's used on click itself as well
            var AvailableDisplays = Data.Displays.Count;
            var SelectedDisplays = new List<Display>();

            if (AvailableDisplays < 1)
            {
                Console.WriteLine($"(Video.cs) Video[Onclick.OnPressed]: You don't have any displays to play this on");
                return;
            }

            SelectedDisplays.Add(Data.Displays[0]);

            if (AvailableDisplays > 1)
            {
                SelectedDisplays = await DisplayChooser.Open();
            }

            foreach (var Display in SelectedDisplays)
            {
                _ = MPV.Play(VideoPath, Display);
            }
        };

        contextMenu.SetParent(this);

        var OnContext = Gtk.GestureClick.New();
        OnContext.SetButton(3);
        OnContext.OnPressed += (_, args) =>
        {
            var x = (int)Math.Round(args.X);
            var y = (int)Math.Round(args.Y);

            var area = new Gdk.Rectangle
            {
                X = x,
                Y = y,
                Width = 1,
                Height = 1
            };

            contextMenu.SetPointingTo(area);
            contextMenu.Popup();
        };

        AddController(OnClick);
        AddController(OnContext);
    }
}