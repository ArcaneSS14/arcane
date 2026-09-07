using Content.Shared._Arcane.Speech;
using Content.Shared.Emoting;
using Content.Shared.Humanoid;
using Content.Shared.Speech.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;

namespace Content.Shared.Speech.EntitySystems;

public sealed class NatureSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NatureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NatureComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, NatureComponent component, ComponentStartup args)
    {
        if (component.EmoteTag != null)
        {
            component.AddedTag = _tagSystem.AddTag(uid, component.EmoteTag.Value);
        }

        if (!TryComp<VocalComponent>(uid, out var vocal))
            return;

        if (vocal.Sounds != null)
        {
            component.OriginalSounds = new Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>(vocal.Sounds);
        }
        component.OriginalEmoteSounds = vocal.EmoteSounds;

        if (component.NewSounds != null)
        {
            vocal.Sounds = new Dictionary<Sex, ProtoId<Content.Shared.Chat.Prototypes.EmoteSoundsPrototype>>(component.NewSounds);

            if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            {
                if (vocal.Sounds.TryGetValue(humanoid.Sex, out var protoId))
                {
                    vocal.EmoteSounds = protoId;
                }
            }
            Dirty(uid, vocal);
        }
    }
    private void OnShutdown(EntityUid uid, NatureComponent component, ref ComponentShutdown args)
    {
        // При удалении компача
        if (component.EmoteTag != null && component.AddedTag)
        {
            _tagSystem.RemoveTag(uid, component.EmoteTag.Value);
        }

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
