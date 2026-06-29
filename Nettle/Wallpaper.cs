using Nettle;
using System.IO;

namespace Nettle;

public static class Wallpaper
{
    public static string Storage = Path.Combine(AppContext.BaseDirectory, "Wallpapers");

    public static void LoadAll()
    {
        foreach(string filePath in Data.Wallpapers)
        {
            UI.AddWallpaper(filePath);
        }
    }

    public static void Import(string VideoPath)
    {
        Directory.CreateDirectory(Storage);

        if (!File.Exists(VideoPath))
        {
            Console.WriteLine($"(Wallpaper.cs) Import: File {VideoPath} does not exist, skipping.");
            return;
        }

        var Metadata = new FileInfo(VideoPath);
        var Key = $"{Metadata.FullName}|{Metadata.LastWriteTimeUtc.Ticks}|{Metadata.Length}";
        var Hash = Key.Sha256();

        var filePath = Path.Combine(Storage, Hash + Metadata.Extension);

        if (!File.Exists(filePath))
        {
            File.Copy(VideoPath, filePath);
        }

        if (!Data.Wallpapers.Contains(filePath))
        {
            Data.Wallpapers.Add(filePath);
            UI.AddWallpaper(filePath);
        }

        Data.Save();
    }

    public static void Remove(this Video Video, string VideoPath)
    {
        Data.Wallpapers.Remove(VideoPath);
        Data.Save();

        UI.RemoveWallpaper(Video);
    }

    public static void Play(string Video, Display Display)
    {
        
    }

    public class Playing
    {
        public string Connector { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Wallpaper { get; set; } = string.Empty;
        public int MonitorIndex { get; set; }
    }
}