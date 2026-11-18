using Content.Server.Preferences.Managers;
using Content.Shared._Arcane.ERP;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server._Arcane.ERP;

public sealed class ErpStatusSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(Entity<HumanoidAppearanceComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!HasComp<ErpAllowedComponent>(ent))
            return;

        var profile = _prefs.GetPreferencesOrNull(args.Player.UserId)?.SelectedCharacter as HumanoidCharacterProfile;
        var preference = profile?.ErpPreference ?? ErpPreference.Ask;

        EnsureComp<ArousalComponent>(ent);

        var comp = EnsureComp<ErpStatusComponent>(ent);
        var oldPreference = comp.Preference;
        comp.Preference = preference;
        Dirty(ent, comp);

        if (oldPreference != preference)
            RaiseLocalEvent(ent, new ErpPreferenceChangedEvent(oldPreference, preference));
    }
}
