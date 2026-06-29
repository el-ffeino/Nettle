using Adw;
using Gtk;
using Nettle;

namespace Nettle;

public class AppWindow : Adw.ApplicationWindow
{
    public Gtk.Overlay RootOverlay { get; private set; }
    public WallpaperGrid WallpaperGrid { get; private set; }
    public Gtk.Stack LibraryStack { get; private set; }

    public AppWindow(Adw.Application app)
    {
        app.AddWindow(this);

        Name = "Nettle";
        Title = "Nettle";
        SetDefaultSize(950, 610);
        SetHideOnClose(true);

        // Root overlay for display chooser
        RootOverlay = Gtk.Overlay.New();
        SetContent(RootOverlay);

        // Modern ToolbarView
        var toolbarView = Adw.ToolbarView.New();
        RootOverlay.Child = toolbarView;

        // Header with ViewSwitcher
        var header = Adw.HeaderBar.New();
        var viewSwitcher = Adw.ViewSwitcher.New();
        var viewStack = Adw.ViewStack.New();

        header.PackStart(new Import());
        header.PackEnd(new MainMenu());
        header.TitleWidget = viewSwitcher;

        viewSwitcher.Stack = viewStack;
        viewSwitcher.Policy = Adw.ViewSwitcherPolicy.Wide;

        viewStack.Hexpand = true;
        viewStack.Vexpand = true;

        toolbarView.AddTopBar(header);

        /* Wallpapers page */
        LibraryStack = Gtk.Stack.New();
        LibraryStack.Hexpand = true;
        LibraryStack.Vexpand = true;

        var emptyPage = Adw.StatusPage.New();
        emptyPage.Title = "No Wallpapers";
        emptyPage.Description = "Import some wallpapers to begin!";
        emptyPage.IconName = "nettle-library-symbolic";

        var wallpapersContent = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
        wallpapersContent.Hexpand = true;
        wallpapersContent.Vexpand = true;

        WallpaperGrid = new WallpaperGrid();

        //
        var scroll = Gtk.ScrolledWindow.New();
        scroll.Hexpand = true;
        scroll.Vexpand = true;
        scroll.SetPolicy(Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
        scroll.Child = WallpaperGrid;

        wallpapersContent.Append(scroll);

        // 
        LibraryStack.AddNamed(emptyPage, "empty");
        LibraryStack.AddNamed(wallpapersContent, "content");

        viewStack.AddTitledWithIcon(LibraryStack, "page1", "Wallpapers", "nettle-library-symbolic");

        /* Explore page */
        var content2 = Adw.StatusPage.New();
        content2.Title = "Explore";
        content2.IconName = "nettle-compass2-symbolic";
        content2.Child = Gtk.Label.New("Under construction :P");
        viewStack.AddTitledWithIcon(content2, "page2", "Explore", "nettle-compass2-symbolic");

        toolbarView.Content = viewStack;
    }
}