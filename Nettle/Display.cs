namespace Nettle;

public class Display
{
    public string Connector { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    public int X { get; set; }
    public int Y { get; set; }
    public int Index { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public string GetIdentifier()
    {
        return $"{Width}x{Height}:{X}+{Y}";
    }
}