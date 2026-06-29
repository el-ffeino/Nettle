namespace Nettle;

public class WindowState
{
    public string Title { get; set; } = string.Empty;
    public string wmClass { get; set; } = string.Empty;
    public bool Maximized { get; set; }
    public bool Fullscreen { get; set; }
    public bool Active { get; set; }
    public int MonitorIndex { get; set; }
}