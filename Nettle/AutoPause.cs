namespace Nettle;

public static class AutoPause
{
    public static void Initialize()
    {
        Extension.OnWindowStateChanged += Window =>
        {
            if (!Data.AutoPause)
            {
                return;
            }

            var Playing = Data.Playing.FirstOrDefault(p => p.MonitorIndex == Window.MonitorIndex);

            if (Playing is null)
            {
                return;
            }

            var Pause = Window.Active && (Window.Fullscreen || Window.Maximized);

            _ = MPV.Command(Playing.Token, "set_property", "pause", Pause);
        };
    }
}