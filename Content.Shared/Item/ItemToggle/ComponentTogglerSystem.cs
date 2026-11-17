// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Network;

namespace Content.Shared.Item.ItemToggle;

/// <summary>
/// Handles <see cref="ComponentTogglerComponent"/> component manipulation.
/// </summary>
public sealed class ComponentTogglerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netManager = default!; // Arcane

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComponentTogglerComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(Entity<ComponentTogglerComponent> ent, ref ItemToggledEvent args)
    {
        ToggleComponent(ent, args.Activated);
    }

    // Goobstation - Make this system more flexible
    public void ToggleComponent(EntityUid uid, bool activate)
    {
        if (!TryComp<ComponentTogglerComponent>(uid, out var component))
            return;


        if (activate)
        {
            var target = component.Parent ? Transform(uid).ParentUid : uid;

            if (TerminatingOrDeleted(target))
                return;

            component.Target = target;

            EntityManager.AddComponents(target, component.Components);
        }
        else
        {
            var target = component.Target; // Arcane

            // Arcane-Edit-Start
            if (target == null)
            {
                if (!_netManager.IsClient)
                    return;

                target = component.Parent ? Transform(uid).ParentUid : uid;
            }
            // Arcane-Edit-End

            if (TerminatingOrDeleted(target.Value)) // Arcane-Edit
                return;

            EntityManager.RemoveComponents(target.Value, component.RemoveComponents ?? component.Components); // Arcane-Edit
        }
    }
}
