using Content.Shared.Emoting;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using System;

namespace Content.Shared._Arcane.Speech;

public sealed class CatNatureSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!; // Тэги наше всё
    [Dependency] private readonly SharedAudioSystem _audio = default!; // Аудио система

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
        if (args.Emote.ID.Equals("Meow", StringComparison.OrdinalIgnoreCase))
            soundToPlay = component.MeowSound;
        else if (args.Emote.ID.Equals("Mew", StringComparison.OrdinalIgnoreCase))
            soundToPlay = component.MewSound;
        else if (args.Emote.ID.Equals("Growl", StringComparison.OrdinalIgnoreCase))
            soundToPlay = component.GrowlSound;
        else if (args.Emote.ID.Equals("Purr", StringComparison.OrdinalIgnoreCase))
            soundToPlay = component.PurrSound;

        // Воспроизводим кастомный звук из нашего компонента
        if (soundToPlay != null)
        {
            _audio.PlayPvs(soundToPlay, uid);
        }
    }
}
