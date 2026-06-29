using Gtk;
using Adw;
using GLib;
using Nettle;

namespace Nettle;

public class PreferencesDialog : Adw.PreferencesDialog
{
    public PreferencesDialog()
    {
        Title = "Preferences";

        var page = Adw.PreferencesPage.New();
        page.Title = "Preferences";
        page.IconName = "nettle-cogwheel-symbolic";

        Add(page);

        var general = Adw.PreferencesGroup.New();
        general.Title = "General";

        page.Add(general);

        var autoStart = Adw.SwitchRow.New();
        autoStart.Title = "Auto Start";
        autoStart.Subtitle = "Start Nettle with your system";
        autoStart.Active = Data.AutoStart;

        autoStart.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "active")
            {
                return;
            }

            Data.AutoStart = autoStart.Active;

            if (Data.AutoStart)
                AutoStart.Enable();
            else
                AutoStart.Disable();

            Data.Save();
        };

        general.Add(autoStart);

        var autoPause = Adw.SwitchRow.New();
        autoPause.Title = "Auto Pause";
        autoPause.Subtitle = "Pause playback when an app is fullscreen";
        autoPause.Active = Data.AutoPause;

        autoPause.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "active")
            {
                return;
            }

            Data.AutoPause = autoPause.Active;
            Data.Save();
        };

        general.Add(autoPause);

        var audio = Adw.PreferencesGroup.New();
        audio.Title = "Audio";

        page.Add(audio);

        var volumeRow = Adw.ActionRow.New();
        volumeRow.Title = "Volume";
        volumeRow.Subtitle = "Wallpaper playback volume";

        var volumeScale = Gtk.Scale.NewWithRange(
            Gtk.Orientation.Horizontal,
            0,
            100,
            1
        );

        volumeScale.WidthRequest = 180;
        volumeScale.Hexpand = false;
        volumeScale.SetDrawValue(true);
        volumeScale.SetValue(Data.Volume);

        volumeScale.OnValueChanged += (_, _) =>
        {
            Data.Volume = (int)Math.Round(volumeScale.GetValue());
            Data.Save();

            foreach (var Playing in Data.Playing)
            {
                _ = MPV.UpdateVolume(Playing.Token);
            }
        };

        volumeRow.AddSuffix(volumeScale);
        volumeRow.ActivatableWidget = volumeScale;

        audio.Add(volumeRow);

        var muted = Adw.SwitchRow.New();
        muted.Title = "Muted";
        muted.Subtitle = "Disable wallpaper audio";
        muted.Active = Data.Muted;

        muted.OnNotify += (sender, args) =>
        {
            if (args.Pspec.GetName() != "active")
            {
                return;
            }

            Data.Muted = muted.Active;
            Data.Save();

            foreach (var Playing in Data.Playing)
            {
                _ = MPV.Mute(Playing.Token);
            }
        };

        audio.Add(muted);

        var experimental = Adw.PreferencesGroup.New();
        experimental.Title = "Experimental";
        page.Add(experimental);

        var workspacePatch = Adw.SwitchRow.New();
        workspacePatch.Title = "Workspace Preview Patch";
        workspacePatch.Subtitle = "Set wallpaper as GNOME overview workspace preview actor";
        workspacePatch.Active = Extension.Schema.PatchWorkspace;

        workspacePatch.OnNotify += (sender, args) =>
        {
            if (args.Pspec.GetName() != "active")
            {
                return;
            }

            Extension.Schema.PatchWorkspace = workspacePatch.Active;
        };

        experimental.Add(workspacePatch);

        var liveOverview = Adw.SwitchRow.New();
        liveOverview.Title = "Live Overview";
        liveOverview.Subtitle = "Replace default overview background with the wallpaper";
        liveOverview.Active = Extension.Schema.ReplaceOverview;

        liveOverview.OnNotify += (sender, args) =>
        {
            if (args.Pspec.GetName() != "active")
            {
                return;
            }

            Extension.Schema.ReplaceOverview = liveOverview.Active;
        };

        experimental.Add(liveOverview);
    }
}