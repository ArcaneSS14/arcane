// SPDX-FileCopyrightText: 2024 ArchPigeon <bookmaster3@gmail.com>
// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Krunklehorn <42424291+Krunklehorn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Morb <14136326+Morb0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Actions;
using Content.Server.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Toggleable;
using Content.Shared.Flicking;
using Robust.Shared.Prototypes;

namespace Content.Server.Flicking;

public sealed class TongueFlickingSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TongueFlickingComponent, MapInitEvent>(OnTongueFlickingMapInit);
        SubscribeLocalEvent<TongueFlickingComponent, ComponentShutdown>(OnTongueFlickingShutdown);
        SubscribeLocalEvent<TongueFlickingComponent, ToggleActionEvent>(OnTongueFlickingToggle);
        SubscribeLocalEvent<TongueFlickingComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentStartup>(OnHumanoidStartup);
    }

    private void OnHumanoidStartup(EntityUid uid, HumanoidAppearanceComponent component, ComponentStartup args)
    {
        Robust.Shared.Timing.Timer.Spawn(100, () =>
        {
            if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
                return;

            if (!humanoid.MarkingSet.TryGetCategory(MarkingCategories.Face, out var faceMarkings))
                return;

            foreach (var marking in faceMarkings)
            {
                if (marking.MarkingId.StartsWith("ForkedTongue"))
                {
                    EnsureComp<TongueFlickingComponent>(uid);
                    return;
                }
            }
        });
    }

    private void OnTongueFlickingMapInit(EntityUid uid, TongueFlickingComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
    }

    private void OnTongueFlickingShutdown(EntityUid uid, TongueFlickingComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionEntity);
    }

    private void OnTongueFlickingToggle(EntityUid uid, TongueFlickingComponent component, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Action != component.ActionEntity)
            return;

        if (TryToggleTongueFlicking(uid, tongueFlicking: component))
            args.Handled = true;
    }

    private void OnMobStateChanged(EntityUid uid, TongueFlickingComponent component, MobStateChangedEvent args)
    {
        if (component.TongueOut)
            TryToggleTongueFlicking(uid, tongueFlicking: component);
    }

    public bool TryToggleTongueFlicking(EntityUid uid, TongueFlickingComponent? tongueFlicking = null, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref tongueFlicking, ref humanoid))
            return false;

        if (!humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Face, out var markings))
            return false;

        var tongueIndex = -1;

        for (var idx = 0; idx < markings.Count; idx++)
        {
            if (markings[idx].MarkingId.StartsWith("ForkedTongue"))
            {
                tongueIndex = idx;
                break;
            }
        }

        if (tongueIndex == -1)
            return false;

        var currentMarkingId = markings[tongueIndex].MarkingId;
        string newMarkingId;

        if (!tongueFlicking.TongueOut)
        {
            newMarkingId = $"{currentMarkingId}{tongueFlicking.Suffix}";
        }
        else
        {
            newMarkingId = currentMarkingId[..^tongueFlicking.Suffix.Length];
        }

        if (!_prototype.HasIndex<MarkingPrototype>(newMarkingId))
            return false;

        tongueFlicking.TongueOut = !tongueFlicking.TongueOut;

        _actions.SetToggled(tongueFlicking.ActionEntity, tongueFlicking.TongueOut);

        _humanoidAppearance.SetMarkingId(
            uid,
            MarkingCategories.Face,
            tongueIndex,
            newMarkingId,
            humanoid: humanoid);

        return true;
    }
}