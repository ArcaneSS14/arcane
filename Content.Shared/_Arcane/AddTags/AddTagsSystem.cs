using Content.Shared.Tag;

namespace Content.Shared._Arcane;

public sealed class AddTagsSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddTagsComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<AddTagsComponent> ent, ref ComponentInit args)
    {
        _tag.AddTags(ent.Owner, ent.Comp.Tags);
    }
}
