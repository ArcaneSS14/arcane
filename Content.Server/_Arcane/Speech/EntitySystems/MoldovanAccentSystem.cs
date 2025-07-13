using System.Text;
using Content.Shared.Speech;
using Content.Server.Speech.EntitySystems;
using Content.Server._Arcane.Speech.Components;

namespace Content.Server._Arcane.Speech.EntitySystems;

public sealed class MoldovanAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MoldovanAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        var accentedMessage = new StringBuilder(_replacement.ApplyReplacements(message, "moldovan"));

        return accentedMessage.ToString();
    }

    private void OnAccent(EntityUid uid, MoldovanAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
