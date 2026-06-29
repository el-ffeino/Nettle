using Adw;

namespace Nettle;

public class ExtensionInstallDialog : Adw.AlertDialog
{
    public ExtensionInstallDialog()
    {
        Heading = "Extension";
        Body = "Nettle requires its GNOME helper extension in order to set MPV (video) as a background actor";

        AddResponse("exit", "Exit");
        AddResponse("install", "Install");

        CloseResponse = "exit";
        DefaultResponse = "install";

        SetResponseAppearance("install", Adw.ResponseAppearance.Suggested);
    }
}