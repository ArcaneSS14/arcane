using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class AdminLogLabel : RichTextLabel
{
    public AdminLogLabel(ref SharedAdminLog log, HSeparator separator)
    {
        Log = log;
        Separator = separator;
        // Arcane-Start
        var impactColor = GetImpactColor(log.Impact);
        var impactText = $"[color={impactColor}]█[/color]";

        var formatted = new FormattedMessage();
        formatted.AddMarkupOrThrow($"{impactText} [bold]{log.Date:HH:mm:ss}[/bold]: {FormattedMessage.EscapeText(log.Message)}");
        // Arcane-End

        SetMessage(formatted); // Arcane-Edit
        OnVisibilityChanged += VisibilityChanged;
    }

    public new SharedAdminLog Log { get; }

    public HSeparator Separator { get; }

    private void VisibilityChanged(Control control)
    {
        Separator.Visible = Visible;
    }

    // Arcane-Start
    private static string GetImpactColor(LogImpact impact) => impact switch
    {
        LogImpact.Extreme => "red",
        LogImpact.High => "orange",
        LogImpact.Medium => "yellow",
        LogImpact.Low => "lightgreen",
        _ => "gray"
    };
    // Arcane-End
}
