using System.IO;
using System.Diagnostics;
using Nettle;

namespace Nettle;

public static class Thumbnailer
{
    public static string Thumbnails = Path.Combine(AppContext.BaseDirectory, "Thumbnails");
    public static int Width = 290;
    public static int Height = 164;

    public static string Create(string VideoPath)
    {
        Directory.CreateDirectory(Thumbnails);

        var ThumbnailPath = CreatePath(VideoPath);

        if (File.Exists(ThumbnailPath)) return ThumbnailPath;

        var Process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        Process.Arguments(
            "-y",
            "-ss", "00:00:01",
            "-i", VideoPath,
            "-frames:v", "1",
            "-vf", $"scale={Width}:{Height}:force_original_aspect_ratio=increase,crop={Width}:{Height}",
            "-q:v", "3",
            ThumbnailPath
        );

        Process.Start();

        var stdOut = Process.StandardOutput.ReadToEnd();
        var stdErr = Process.StandardError.ReadToEnd();

        Process.WaitForExit();

        if (Process.ExitCode != 0 || !File.Exists(ThumbnailPath))
        {
            throw new Exception($"(Thumbnailer.cs: Create) ffmpeg failed to create thumbnail:\n{stdOut}\n{stdErr}");
        }

        return ThumbnailPath;
    }

    private static string CreatePath(string VideoPath)
    {
        var Info = new FileInfo(VideoPath);
        var Key = $"{Info.FullName}|{Info.LastWriteTimeUtc.Ticks}|{Info.Length}|{Width}x{Height}";
        var Hash = Key.Sha256();

        return Path.Combine(Thumbnails, Hash + ".png");
    }
}