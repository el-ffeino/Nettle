using Adw;
using Gtk;
using Nettle;

namespace Nettle;

public static class DisplayChooser
{
    public static Task<List<Display>> Open()
    {
        var Selected = new TaskCompletionSource<List<Display>>();

        var RootOverlay = App.Window.RootOverlay;
        var Picker = new DisplayChooserDialog();

        Picker.OnCancel += () =>
        {
            RootOverlay.RemoveOverlay(Picker);
            Selected.TrySetResult(new List<Display>());
        };

        Picker.OnSelect += (Displays) =>
        {
            RootOverlay.RemoveOverlay(Picker);
            Selected.TrySetResult(Displays);
        };

        RootOverlay.AddOverlay(Picker);

        return Selected.Task;
    }
}