using Content.Shared.Emoting;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System;

namespace Content.Shared._Arcane.Speech;

public sealed class CatNatureSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!; // Тэги наше всё
    [Dependency] private readonly SharedAudioSystem _audio = default!; // Аудио система

    // Переделал Айди под замечание зайца (
    private static readonly ProtoId<EmotePrototype> MeowEmoteId = "Meow";
    private static readonly ProtoId<EmotePrototype> MewEmoteId = "Mew";
    private static readonly ProtoId<EmotePrototype> GrowlEmoteId = "Growl";
    private static readonly ProtoId<EmotePrototype> PurrEmoteId = "Purr";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CatNatureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CatNatureComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CatNatureComponent, BeforeEmoteEvent>(OnBeforeEmote);
    }

    private void OnStartup(EntityUid uid, CatNatureComponent component, ref ComponentStartup args)
    {
        // Выдача тэга при наличии компача
        _tagSystem.AddTag(uid, "FelinidEmotes");
    }

    private void OnShutdown(EntityUid uid, CatNatureComponent component, ref ComponentShutdown args)
    {
        // При удалении компача
        _tagSystem.RemoveTag(uid, "FelinidEmotes");
    }

    private void OnBeforeEmote(EntityUid uid, CatNatureComponent component, ref BeforeEmoteEvent args)
    {
        if (args.Cancelled)
            return;

        SoundSpecifier? soundToPlay = null;

        // Проверяем ID эмоции
        if (args.Emote.ID == MeowEmoteId)
            soundToPlay = component.MeowSound;
        else if (args.Emote.ID == MewEmoteId)
            soundToPlay = component.MewSound;
        else if (args.Emote.ID == GrowlEmoteId)
            soundToPlay = component.GrowlSound;
        else if (args.Emote.ID == PurrEmoteId)
            soundToPlay = component.PurrSound;

        // Воспроизводим кастомный звук из нашего компонента
        if (soundToPlay != null)
        {
            _audio.PlayPvs(soundToPlay, uid);
        }
    }
}
