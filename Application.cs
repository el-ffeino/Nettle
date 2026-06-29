using Gtk;
using Adw;
using Nettle;
using System.Runtime.InteropServices;

namespace Nettle;

public static class App
{
    public static Adw.Application Application { get; private set; } = null!;
    public static AppWindow Window { get; private set; } = null!;
    public static uint UserId { get; private set; }

    public static void Init(Adw.Application app, AppWindow window)
    {
        Application = app;
        Window = window;
        UserId = getuid();
        app.RegisterHandlers();
        AutoStart.AddDesktopEntry();
    }

    [DllImport("libc", SetLastError = true)]
    private static extern uint getuid();
}