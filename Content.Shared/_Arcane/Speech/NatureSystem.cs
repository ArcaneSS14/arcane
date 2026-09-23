using Content.Shared._Arcane.Speech;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emoting;
using Content.Shared.Humanoid;
using Content.Shared.Speech.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Speech.EntitySystems;

public sealed class NatureSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NatureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NatureComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NatureComponent, EmoteEvent>(OnNatureEmote);
    }

    private void OnStartup(EntityUid uid, NatureComponent component, ComponentStartup args)
    {
        if (component.emoteTag != null)
        {
            component.AddedTag = _tagSystem.AddTag(uid, component.emoteTag.Value);
        }
    }

    private void OnShutdown(EntityUid uid, NatureComponent component, ref ComponentShutdown args)
    {
        if (component.emoteTag != null && component.AddedTag)
        {
            _tagSystem.RemoveTag(uid, component.emoteTag.Value);
        }
    }

    private void OnNatureEmote(EntityUid uid, NatureComponent component, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        // The species' own sounds always win; the trait only adds sounds for emotes the
        // race has none of (e.g. Meow for a human, while an IPC keeps its Beep/Boop).
        if (TryComp<VocalComponent>(uid, out var vocal)
            && vocal.EmoteSounds is { } raceId
            && _proto.TryIndex(raceId, out var raceSounds)
            && _chat.TryPlayEmoteSound(uid, raceSounds, args.Emote))
        {
            args.Handled = true;
            return;
        }

        if (component.newSounds != null
            && component.newSounds.TryGetValue(humanoid.Sex, out var traitId)
            && _proto.TryIndex(traitId, out var traitSounds)
            && _chat.TryPlayEmoteSound(uid, traitSounds, args.Emote))
        {
            args.Handled = true;
        }
    }
}