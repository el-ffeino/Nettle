using System.Diagnostics;
using System.IO;

namespace Nettle;

public static class Nautilus
{
    public static void Show(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"(Nautilus.cs) Show: file doesn't exist: {filePath}");
            return;
        }

        try {
            Process.Start("nautilus", $"--select \"{filePath}\"");
        } catch { 
            Process.Start("xdg-open", $"\"{Path.GetDirectoryName(filePath)}\"");
        }
    }
}