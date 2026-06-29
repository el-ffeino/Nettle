using Gtk;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nettle;

public class DisplayChooserDialog : Gtk.Box
{
    private readonly Dictionary<Display, Gtk.ToggleButton> Monitors = new();

    public event Action? OnCancel;
    public event Action<List<Display>>? OnSelect;

    public DisplayChooserDialog()
    {
        SetOrientation(Gtk.Orientation.Vertical);
        Spacing = 0;
        Halign = Align.Center;
        Valign = Align.Center;
        SetSizeRequest(680, 460);

        AddCssClass("display-picker-card");

        var titlebar = Gtk.CenterBox.New();
        titlebar.AddCssClass("display-picker-titlebar");
        titlebar.MarginTop = 4;
        titlebar.MarginBottom = 4;
        titlebar.MarginStart = 8;
        titlebar.MarginEnd = 8;

        var Cancel = Gtk.Button.NewWithLabel("Cancel");
        Cancel.MarginBottom = 4;
        Cancel.MarginTop = 4;
        Cancel.OnClicked += (_, _) => OnCancel?.Invoke();

        var title = Gtk.Label.New("Choose display");
        title.AddCssClass("heading");
        title.Halign = Align.Center;

        var Select = Gtk.Button.NewWithLabel("Select");
        Select.AddCssClass("suggested-action");
        Select.MarginBottom = 4;
        Select.MarginTop = 4;
        Select.Sensitive = false;
        Select.OnClicked += (_, _) =>
        {
            var selected = GetSelectedDisplays();

            if (selected.Count < 1)
                return;

            OnSelect?.Invoke(selected);
        };

        titlebar.StartWidget = Cancel;
        titlebar.CenterWidget = title;
        titlebar.EndWidget = Select;

        var handle = Gtk.WindowHandle.New();
        handle.Child = titlebar;

        Append(handle);

        var description = Gtk.Label.New("Select which screen you'd like to play it on");
        description.Halign = Align.Center;
        description.MarginTop = 12;
        description.MarginBottom = 20;
        description.AddCssClass("display-picker-description");

        Append(description);

        var frame = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
        frame.Hexpand = true;
        frame.Vexpand = true;
        frame.MarginStart = 36;
        frame.MarginEnd = 36;
        frame.MarginBottom = 36;
        frame.AddCssClass("display-picker-frame");

        Append(frame);

        var monitorBox = Gtk.FlowBox.New();
        monitorBox.Halign = Align.Center;
        monitorBox.Valign = Align.Center;
        monitorBox.Hexpand = true;
        monitorBox.Vexpand = true;
        monitorBox.MinChildrenPerLine = 1;
        monitorBox.MaxChildrenPerLine = 3;
        monitorBox.SelectionMode = Gtk.SelectionMode.None;
        monitorBox.ColumnSpacing = 18;
        monitorBox.RowSpacing = 18;
        monitorBox.MarginTop = 24;
        monitorBox.MarginBottom = 24;
        monitorBox.MarginStart = 24;
        monitorBox.MarginEnd = 24;

        frame.Append(monitorBox);

        foreach (var display in Data.Displays)
        {
            var button = CreateMonitor(display);

            button.OnToggled += (_, _) =>
            {
                Select.Sensitive = GetSelectedDisplays().Count > 0;
            };

            Monitors[display] = button;
            monitorBox.Append(button);
        }
    }

    private Gtk.ToggleButton CreateMonitor(Display display)
    {
        var button = Gtk.ToggleButton.New();
        button.SetSizeRequest(250, 140);
        button.AddCssClass("display-card");

        var label = Gtk.Label.New($"{display.Connector}\n{display.GetIdentifier()}");
        label.Halign = Align.Center;
        label.Justify = Justification.Center;
        label.Valign = Align.Center;
        label.Wrap = true;

        button.Child = label;

        return button;
    }

    private List<Display> GetSelectedDisplays()
    {
        return Monitors.Where(pair => pair.Value.Active).Select(pair => pair.Key).ToList();
    }
}