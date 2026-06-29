using Nettle;
using System.IO;
using System.Text.Json;

namespace Nettle;

public static class Data
{
    private static readonly string dataFile = Path.Combine(AppContext.BaseDirectory, "data.json");
    private static readonly JsonSerializerOptions prettyPrint = new JsonSerializerOptions { WriteIndented = true };

    public static List<Display> Displays { get; set; } = new();
    public static List<string> Wallpapers { get; set; } = new();
    public static List<Wallpaper.Playing> Playing { get; set; } = new();
    public static int Volume { get; set; } = 35;
    public static bool AutoStart { get; set; }
    public static bool AutoPause { get; set; }
    public static bool Muted { get; set; }

    public static void Load()
    {
        Displays = GdkDisplay.GetAll();

        if (!File.Exists(dataFile))
        {
            Save();
            return;
        }

        try
        {
            var content = File.ReadAllText(dataFile);
            var settings = JsonSerializer.Deserialize<Settings>(content);

            if (settings is null)
            {
                Save();
                return;
            }

            Wallpapers = settings.Wallpapers ?? new();
            Playing = settings.Playing ?? new();
            Volume = settings.Volume;
            AutoStart = settings.AutoStart;
            AutoPause = settings.AutoPause;
            Muted = settings.Muted;

            if (AutoStart && Playing.Count > 0)
            {
                foreach(var Player in Playing)
                {
                    var Display = Data.Displays.FirstOrDefault(d => d.Connector == Player.Connector);
                    
                    if (Display is null)
                    {
                        continue;
                    }

                    _ = MPV.Play(Player.Wallpaper, Display);
                }
            }

            Playing = new();
            Save();
        } 
        catch (Exception ex)
        {
            Console.WriteLine($"(Data.cs) Init: dataFile might be corrupted, setting default values - {ex}");
            Save();
        }
    }

    public static void Save()
    {
        var settings = new Settings
        {
            Wallpapers = Wallpapers,
            Playing = Playing,
            Volume = Volume,
            AutoStart = AutoStart,
            AutoPause = AutoPause,
            Muted = Muted
        };

        var content = JsonSerializer.Serialize(settings, prettyPrint);
        File.WriteAllText(dataFile, content);
    }

    private class Settings
    {
        public List<string> Wallpapers { get; set; } = new();
        public List<Wallpaper.Playing> Playing { get; set; } = new();
        public int Volume { get; set; } = 35;
        public bool AutoStart { get; set; }
        public bool AutoPause { get; set; }
        public bool Muted { get; set; }
    }
}