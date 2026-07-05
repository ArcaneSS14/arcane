using Content.Shared._Arcane.Speech;
using Content.Shared.Emoting;
using Content.Shared.Humanoid;
using Content.Shared.Speech.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;

namespace Content.Shared.Speech.EntitySystems;

public sealed class CatNatureSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CatNatureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CatNatureComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, CatNatureComponent component, ComponentStartup args)
    {
        _tagSystem.AddTag(uid, "FelinidEmotes");

        if (!TryComp<VocalComponent>(uid, out var vocal))
            return;

        vocal.Sounds = new Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>
        {
            { Sex.Male, "MaleFelinid" },
            { Sex.Female, "FemaleFelinid" },
            { Sex.Unsexed, "MaleFelinid" }
        };

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            if (vocal.Sounds.TryGetValue(humanoid.Sex, out var protoId))
            {
                vocal.EmoteSounds = protoId;
            }
        }
        Dirty(uid, vocal);
    }
    private void OnShutdown(EntityUid uid, CatNatureComponent component, ref ComponentShutdown args)
    {
        // При удалении компача
        _tagSystem.RemoveTag(uid, "FelinidEmotes");

        if (TryComp<VocalComponent>(uid, out var vocal))
        {
            if (component.OriginalSounds != null)
                vocal.Sounds = component.OriginalSounds;

            if (component.OriginalEmoteSounds != null)
                vocal.EmoteSounds = component.OriginalEmoteSounds;

            Dirty(uid, vocal);
        }
    }
}
