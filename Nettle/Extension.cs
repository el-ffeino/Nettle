using Gtk;
using Adw;
using System.IO;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Nettle;

namespace Nettle;

public static class Extension
{
    public static string Name = "nettle-extension@ffeine";
    public static string Schemas = Path.Combine(AppContext.BaseDirectory, "Schemas");
    public static string Packaged = Path.Combine(AppContext.BaseDirectory, "nettle-extension@ffeine.shell-extension.zip");

    private const string BusName = "io.ffeine.NettleExtension";
    private const string ObjectPath = "/io/ffeine/NettleExtension";
    private const string InterfaceName = "io.ffeine.NettleExtension";

    private static Gio.DBusProxy? Dbus;
    private static bool Subscribed;

    public static event Action<WindowState>? OnWindowStateChanged;

    public static bool IsInstalled()
    {
        var (Success, _, Output) = GetOutput("info", Name);

        if (!Success)
        {
            Console.WriteLine($"(Extension.cs: IsInstalled()) Error: {Output}");
        }

        return (Success && Output.Contains("Version"));
    }

    public static bool IsEnabled()
    {
        var (Success, _, Output) = GetOutput("info", Name);

        if (!Success)
        {
            Console.WriteLine($"(Extension.cs: IsEnabled()) Error: {Output}");
        }

        return (Success && Output.Contains("State: ACTIVE"));
    }

    public static void Install()
    {
        var Command = GetOutput("install", "-f", Packaged);
        Console.WriteLine($"{"'" + Packaged.Replace("'", "'\\''") + "'"}");

        if (Command.Success && Command.exitCode == 0)
        {
            var successDialog = Adw.AlertDialog.New("Success", "Extension was installed, reset your Gnome session or reboot your machine");

            successDialog.AddResponse("okay", "Okay");
            successDialog.CloseResponse = "okay";

            successDialog.OnResponse += (_, args) =>
            {
                if (args.Response == "okay")
                {
                    Process.Start("gnome-session-quit");
                }
            };

            successDialog.Present(App.Window);

            Console.WriteLine("(Extension.cs: Install()) Success: Installed extension, try relogging");
        }
        else 
        {
            Console.WriteLine($"(Extension.cs: Install()) Error: Failed to install extension - {Command.Success.ToString()} {Command.exitCode.ToString()} {Command.Output}");
        }
    }

    public static void Enable()
    {
        if (!IsInstalled())
        {
            var installDialog = new ExtensionInstallDialog();

            installDialog.OnResponse += (_, args) =>
            {
                if (args.Response == "install")
                {
                    Install();
                }
                else
                {
                    Console.WriteLine("(ExtensionInstallDialog.cs) Error: Nettle requires the extension installed to function properly.");
                    Environment.Exit(1);
                }
            };

            installDialog.Present(App.Window);
            return;
        }

        if (IsEnabled())
        {
            return;
        }

        var Command = GetOutput("enable", Name);

        if (Command.Success && Command.exitCode == 0)
        {
            Console.WriteLine($"(Extension.cs: Enable()) Success: Enabled extension");
        }
        else
        {
            Console.WriteLine($"(Extension.cs: Enable()) Error: Failed to enable extension - {Command.Success.ToString()} {Command.exitCode.ToString()} {Command.Output}");
        }
    }

    public static (bool Success, int exitCode, string Output) GetOutput(params string[] Args)
    {
        try
        {
            using var Process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gnome-extensions",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            Process.Arguments(Args);

            Process.Start();

            string stdOutput = Process.StandardOutput.ReadToEnd();
            string stdError = Process.StandardError.ReadToEnd();

            Process.WaitForExit();

            return (true, Process.ExitCode, stdOutput.Trim() + stdError.Trim());
        }
        catch (Exception ex)
        {
            return (false, -1, ex.Message);
        }
    }

    public static void InitializeDbus()
    {
        if (Dbus != null) return;

        Dbus = Gio.DBusProxy.NewForBusSync(Gio.BusType.Session, Gio.DBusProxyFlags.None, null, BusName, ObjectPath, InterfaceName, null);

        if (Dbus != null && !Subscribed)
        {
            Dbus.OnGSignal += (_, args) =>
            {
                if (args.SignalName != "WindowStateChanged") return;

                var Params = args.Parameters;

                try
                {
                    nuint titleLength;
                    nuint wmClassLength;

                    var WindowState = new WindowState
                    {
                        Title = Params.GetChildValue(0).GetString(out titleLength),
                        wmClass = Params.GetChildValue(1).GetString(out wmClassLength),
                        Maximized = Params.GetChildValue(2).GetBoolean(),
                        Fullscreen = Params.GetChildValue(3).GetBoolean(),
                        Active = Params.GetChildValue(4).GetBoolean(),
                        MonitorIndex = Params.GetChildValue(5).GetInt32()
                    } ;

                    OnWindowStateChanged?.Invoke(WindowState);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"(Extension.cs: WindowStateChanged) Failed to parse signal: {ex}");
                }
            };

            Subscribed = true;
        }
    }

    public static void Attach(Display Display)
    {
        InitializeDbus();

        var Params = GLib.Variant.NewTuple(new GLib.Variant[]
        {
            GLib.Variant.NewString(Display.Token),
            GLib.Variant.NewInt32(Display.X),
            GLib.Variant.NewInt32(Display.Y),
            GLib.Variant.NewInt32(Display.Width),
            GLib.Variant.NewInt32(Display.Height)
        });

        Dbus!.CallSync("Attach", Params, Gio.DBusCallFlags.None, -1, null);
    }

    public static async Task<bool> WaitForSocketInit(string Sock)
    {
        var Start = DateTime.UtcNow;
        var Timeout = 5000;

        while ((DateTime.UtcNow - Start).TotalMilliseconds < Timeout)
        {
            if (File.Exists(Sock))
            {
                try
                {
                    using var Socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await Socket.ConnectAsync(new UnixDomainSocketEndPoint(Sock));
                    return true;
                } 
                catch { }
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static GLib.Variant NewTuple(params GLib.Variant[] children)
    {
        return GLib.Variant.NewTuple(children);
    }

    public static class Schema
    {
        private const string Name = "org.gnome.shell.extensions.nettle";
        private const string ReplaceOverviewKey = "overview-background";
        private const string PatchWorkspaceKey = "workspace-patch";

        private static Gio.Settings? _Settings;

        private static Gio.Settings Settings
        {
            get
            {
                if (_Settings is not null)
                {
                    return _Settings;
                }

                var Compiled = Path.Combine(Schemas, "gschemas.compiled");

                if (!File.Exists(Compiled))
                {
                    throw new FileNotFoundException($"Could not find compiled GSettings schema: {Compiled}");
                }

                var Source = Gio.SettingsSchemaSource.NewFromDirectory(
                    Schemas,
                    Gio.SettingsSchemaSource.GetDefault(),
                    false
                );

                var schema = Source.Lookup(Name, true);

                if (schema is null)
                {
                    throw new InvalidOperationException(
                        $"GSettings schema '{Name}' was not found in {Schemas}"
                    );
                }

                _Settings = Gio.Settings.NewFull(schema, null, null);
                return _Settings;
            }
        }

        public static bool ReplaceOverview
        {
            get => Settings.GetBoolean(ReplaceOverviewKey);
            set => Settings.SetBoolean(ReplaceOverviewKey, value);
        }

        public static bool PatchWorkspace
        {
            get => Settings.GetBoolean(PatchWorkspaceKey);
            set => Settings.SetBoolean(PatchWorkspaceKey, value);
        }
    }
}