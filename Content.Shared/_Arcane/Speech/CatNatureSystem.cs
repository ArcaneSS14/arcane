using Content.Shared._Arcane.Speech;
using Robust.Shared.GameObjects;

namespace Content.Shared._Arcane.Speech;

public sealed class CatNatureSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CatNatureComponent, GetAdditionalEmotesEvent>(OnGetAdditionalEmotes);
    }

    private void OnGetAdditionalEmotes(EntityUid uid, CatNatureComponent component, ref GetAdditionalEmotesEvent args)
    {
        args.Emotes.Add("Meow");
        args.Emotes.Add("Mew");
        args.Emotes.Add("Growl");
        args.Emotes.Add("Purr");
    }
}
