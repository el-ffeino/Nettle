using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.IO;
using Nettle;

namespace Nettle;

public static class MPV
{
    public static async Task Play(string Video, Display Display)
    {
        var Playing = Data.Playing.FirstOrDefault(p => p.Token == Display.Token);
        var Sock = $"/run/user/{App.UserId}/{Display.Token}.sock";

        if (Playing != null)
        {
            if (File.Exists(Sock))
            {
                await Load(Display.Token, Video);
                return;
            }
            else
            {
                Playing = null;
            }
        }
        else
        {
            if (File.Exists(Sock))
            {
                File.Delete(Sock);
            }
        }

        var Process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "mpv",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                RedirectStandardInput = false,
                CreateNoWindow = true
            }
        };

        var Arguments = Process.StartInfo.ArgumentList;

        Process.Arguments(
            //"--force-window=yes",
            $"--title={Display.Token}",
            "--fullscreen",
            $"--fs-screen-name={Display.Connector}",
            "--no-border",
            "--no-osc",
            "--no-osd-bar",
            "--input-cursor=no",
            "--cursor-autohide=no",
            "--no-input-default-bindings",
            "--input-vo-keyboard=no",
            "--loop-file=inf",
            $"--geometry={Display.Width}x{Display.Height}",
            "--keepaspect=yes",
            "--msg-level=all=warn",
            "--panscan=1.0",
            "--video-unscaled=no",
            $"--volume={Data.Volume}",
            $"--input-ipc-server={Sock}",
            "--no-config",
            "--load-scripts=no",
            "--osd-level=0",
            "--hwdec=vaapi", // 290MB Memory ~8% GPU usage for 2 instances | Literally 100% better performance
            Video
        );

        bool existingAudio = Data.Playing.Any(p => p.Wallpaper == Video);

        if (Data.Muted || existingAudio)
        {
            Arguments.Add("--no-audio");
        }

        Process.Start();

        bool Attachable = await Extension.WaitForSocketInit(Sock);
        
        if (!Attachable)
        {
            Console.WriteLine($"(MPV.cs) Play: Unable to attach MPV to gnome-shell");
            Process.Kill();
            return;
        }

        Console.WriteLine($"Attaching display '{Display.Connector} | Index: {Display.Index}' at {Display.X},{Display.Y}");
        Extension.Attach(Display);

        if (Playing is null)
        {
            Data.Playing.Add(new Wallpaper.Playing()
            {
                Connector = Display.Connector,
                Token = Display.Token,
                Wallpaper = Video,
                MonitorIndex = Display.Index
            });
        }
        else
        {
            Playing.Wallpaper = Video;
        }

        MenuHandlers.Refresh();
        Data.Save();
    }

    public static async Task Command(string Token, params object[] Args)
    {
        try
        {
            using var Socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            var Sock = $"/run/user/{App.UserId}/{Token}.sock";
            await Socket.ConnectAsync(new UnixDomainSocketEndPoint(Sock));

            var Command = new
            {
                command = Args
            };

            var Json = JsonSerializer.Serialize(Command) + "\n";
            var Bytes = Encoding.UTF8.GetBytes(Json);

            _ = Socket.SendAsync(Bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(MPV.cs: Command) Failed: {ex.Message}");
        }
    }

    public static async Task<T?> GetProperty<T>(string Token, string Property)
    {
        try
        {
            using var Socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            var Sock = $"/run/user/{App.UserId}/{Token}.sock";
            await Socket.ConnectAsync(new UnixDomainSocketEndPoint(Sock));

            var Command = new
            {
                command = new object[]
                {
                    "get_property",
                    Property
                }
            };

            var Json = JsonSerializer.Serialize(Command) + "\n";
            var Bytes = Encoding.UTF8.GetBytes(Json);

            await Socket.SendAsync(Bytes);

            var Buffer = new byte[8192];
            var Builder = new StringBuilder();

            while (true)
            {
                var Received = await Socket.ReceiveAsync(Buffer, SocketFlags.None);

                if (Received <= 0)
                {
                    break;
                }

                Builder.Append(Encoding.UTF8.GetString(Buffer, 0, Received));

                if (Builder.ToString().Contains('\n'))
                {
                    break;
                }
            }

            var Response = Builder.ToString().Trim();

            if (string.IsNullOrWhiteSpace(Response))
            {
                return default;
            }

            using var Document = JsonDocument.Parse(Response);
            var Root = Document.RootElement;

            if (!Root.TryGetProperty("error", out var Error)
            || Error.GetString() != "success"
            || !Root.TryGetProperty("data", out var Data))
            {
                return default;
            }

            return Data.Deserialize<T>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(MPV.cs: GetProperty) Failed: {ex.Message}");
            return default;
        }
    }

    public static async Task Load(string Token, string Video)
    {
        _ = Command(Token, "loadfile", Video, "replace");
    }

    public static async Task UpdateVolume(string Token)
    {
        _ = Command(Token, "set_property", "volume", Data.Volume);
    }

    public static async Task Pause(string Token)
    {
        var Paused = await GetProperty<bool>(Token, "pause");
        _ = Command(Token, "set_property", "pause", !Paused);
    }
    
    public static async Task Mute(string Token)
    {
        _ = Command(Token, "set_property", "mute", Data.Muted);
    }

    public static async Task Quit(string Token)
    {
        _ = Command(Token, "quit");
    }
}