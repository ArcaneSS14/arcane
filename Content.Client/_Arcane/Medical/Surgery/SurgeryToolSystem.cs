using System.Linq;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Robust.Client.UserInterface;

namespace Content.Client._Arcane.Medical.Surgery;

public sealed class SurgeryToolSystem : SharedSurgeryToolSystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedSurgerySystem _surgery = default!;

    private SurgeryToolOptionsPopup? _popup;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SurgeryToolOptionsEvent>(OnOptions);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ClosePopup();
    }

    private void OnOptions(SurgeryToolOptionsEvent msg)
    {
        if (!TryGetEntity(msg.Part, out var part))
            return;

        var options = msg.Options;
        ClosePopup();

        var entries = options.Select(GetEntry).OrderBy(e => e.Name).ToList();
        var title = options.Count == 1
            ? Loc.GetString("surgery-tool-options-confirm")
            : Loc.GetString("surgery-tool-options-title", ("part", part.Value));

        var popup = new SurgeryToolOptionsPopup();
        popup.Populate(title, entries);
        popup.OnPicked += option => RaisePredictiveEvent(new SurgeryToolOptionPickedEvent(msg.Part, option));
        popup.OnPopupHide += () =>
        {
            popup.Orphan();
            if (_popup == popup)
                _popup = null;
        };

        _popup = popup;
        _ui.ModalRoot.AddChild(popup);
        popup.OpenAtMouse();
    }

    private SurgeryToolOptionEntry GetEntry(SurgeryToolOption option)
    {
        var step = _surgery.GetSingleton(option.Step);
        var stepName = step is { } stepEnt ? Name(stepEnt) : option.Step.Id;
        var toolName = step is { } toolStep ? GetToolName(toolStep) : null;
        var details = toolName == null
            ? stepName
            : Loc.GetString("surgery-tool-options-step", ("step", stepName), ("tool", toolName));

        // An option merged from several surgeries is named by its step, which is what they have in common.
        if (option.Target is not { } target || _surgery.GetSingleton(target) is not { } surgery)
            return new SurgeryToolOptionEntry(option, stepName, details);

        return new SurgeryToolOptionEntry(option, Name(surgery), details);
    }

    private void ClosePopup()
    {
        _popup?.Close();
        _popup = null;
    }
}
