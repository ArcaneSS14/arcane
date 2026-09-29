using Content.Server._Arcane.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Speech;

namespace Content.Server._Arcane.Speech.EntitySystems;

public sealed class XenophobeAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenophobeAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<XenophobeAccentComponent> ent, ref AccentGetEvent args)
    {
        var message = args.Message;

        if (TryComp<HumanoidAppearanceComponent>(ent, out var humanoid)
            && ent.Comp.SpeciesAccents.TryGetValue(humanoid.Species, out var speciesAccent))
            message = _replacement.ApplyReplacements(message, speciesAccent);

        args.Message = _replacement.ApplyReplacements(message, ent.Comp.Accent);
    }
}
