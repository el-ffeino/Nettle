using Gdk;
using Nettle;

namespace Nettle;

public static class GdkDisplay
{
    public static List<Display> GetAll()
    {
        var Displays = new List<Display>();
        var gdkDisplay = Gdk.Display.GetDefault();

        if (gdkDisplay is null) return Displays;

        var Monitors = gdkDisplay.GetMonitors();
        var Count = Monitors.GetNItems();

        for (uint i = 0; i < Count; i++)
        {
            var Monitor = Monitors.GetObject(i);

            if (Monitor is null) continue;

            try
            {
                if (Monitor is not Gdk.Monitor monitor)
                {
                    Console.WriteLine($"(GdkDisplay.cs) Warning: monitor {i} is {Monitor.GetType().FullName}, not Gdk.Monitor");
                    continue;
                }

                monitor.GetGeometry(out var Geometry);

                Displays.Add(new Display
                {
                    Token = $"nettle-{i}",
                    Connector = monitor.GetConnector() ?? string.Empty,
                    X = Geometry.X,
                    Y = Geometry.Y,
                    Index = (int)i,
                    Width = Geometry.Width,
                    Height = Geometry.Height
                });
            }
            finally
            {
                Monitor.Dispose();
            }
        }

        return Displays.OrderBy(d => d.X).ToList();
    }
}