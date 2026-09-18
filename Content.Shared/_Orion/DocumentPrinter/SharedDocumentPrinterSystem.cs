using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Orion.DocumentPrinter;

public sealed class SharedDocumentPrinterSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DocumentPrinterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(Entity<DocumentPrinterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        var enabled = !ent.Comp.IsOnAutocomplete;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(enabled ? "printer-autofill-on" : "printer-autofill-off"),
            Act = () => TrySetAutocomplete(ent.AsNullable(), enabled, user),
        });
    }

    public bool TrySetAutocomplete(Entity<DocumentPrinterComponent?> ent, bool enabled, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.IsOnAutocomplete == enabled)
            return false;

        ent.Comp.IsOnAutocomplete = enabled;
        Dirty(ent);
        _audio.PlayPredicted(ent.Comp.SwitchSound, ent, user);
        return true;
    }
}
