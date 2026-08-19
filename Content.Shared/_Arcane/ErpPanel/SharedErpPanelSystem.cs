using Content.Shared._Arcane.ERP;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.ErpPanel;

public sealed class SharedErpPanelSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErpPanelOwnerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(
        Entity<ErpPanelOwnerComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args)
    {

        if (!HasComp<ErpPanelOwnerComponent>(args.User))
            return;

        if (!HasComp<ArousalComponent>(args.User))
            return;

        if (!HasComp<ArousalComponent>(ent.Owner))
            return;

        var user = args.User;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                var ev = new ErpPanelOpenEvent(ent.Owner);
                RaiseLocalEvent(user, ref ev);
            },

            Text = Loc.GetString("erp-panel-open-verb"),

            Icon = new SpriteSpecifier.Texture(
                new("/Textures/_Arcane/Interface/heartIcon.png")),

            Disabled = !args.CanInteract || !args.CanAccess,
            Priority = 2
        };

        args.Verbs.Add(verb);
    }
}
