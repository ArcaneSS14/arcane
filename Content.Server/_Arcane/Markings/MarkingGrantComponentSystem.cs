using System.Linq;
using Content.Server.Actions;
using Content.Server.Wagging;
using Content.Shared._Shitmed.Humanoid.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Wagging;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Markings;

public sealed class HumanoidMarkingsUpdatedEvent : EntityEventArgs;

[RegisterComponent]
public sealed partial class MarkingGrantedComponentsComponent : Component
{
    [DataField]
    public HashSet<string> Granted = new();
}

/// <summary>
/// Grants and revokes components declared on active marking prototypes.
/// </summary>
public sealed class MarkingGrantComponentSystem : EntitySystem
{
    private const string AnimatedSuffix = "Animated";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly WaggingSystem _wagging = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, ProfileLoadFinishedEvent>(OnProfileLoaded);
        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidMarkingsUpdatedEvent>(OnMarkingsUpdated);
    }

    private void OnProfileLoaded(Entity<HumanoidAppearanceComponent> ent, ref ProfileLoadFinishedEvent args) =>
        UpdateMarkingGrants(ent, ent.Comp);

    private void OnMarkingsUpdated(Entity<HumanoidAppearanceComponent> ent, ref HumanoidMarkingsUpdatedEvent args) =>
        UpdateMarkingGrants(ent, ent.Comp);

    private void UpdateMarkingGrants(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        var desired = GetDesiredComponents(humanoid);
        var granted = EnsureComp<MarkingGrantedComponentsComponent>(uid);

        foreach (var (componentName, entry) in desired)
        {
            if (granted.Granted.Contains(componentName))
                continue;

            var registration = _componentFactory.GetRegistration(componentName);
            if (HasComp(uid, registration.Type))
                continue;

            EntityManager.AddComponents(uid, new ComponentRegistry { { componentName, entry } }, removeExisting: false);
            granted.Granted.Add(componentName);

            if (registration.Type == typeof(WaggingComponent)
                && TryComp(uid, out WaggingComponent? wagging)
                && wagging.ActionEntity == null)
            {
                _actions.AddAction(uid, ref wagging.ActionEntity, wagging.Action, uid);
            }
        }

        foreach (var componentName in granted.Granted.ToList())
        {
            if (desired.ContainsKey(componentName))
                continue;

            var registration = _componentFactory.GetRegistration(componentName);
            if (registration.Type == typeof(WaggingComponent)
                && TryComp(uid, out WaggingComponent? wagging)
                && wagging.Wagging)
            {
                _wagging.TryToggleWagging(uid, wagging);
            }

            RemComp(uid, registration.Type);
            granted.Granted.Remove(componentName);
        }

        if (granted.Granted.Count == 0)
            RemComp<MarkingGrantedComponentsComponent>(uid);
    }

    private Dictionary<string, EntityPrototype.ComponentRegistryEntry> GetDesiredComponents(HumanoidAppearanceComponent humanoid)
    {
        var desired = new Dictionary<string, EntityPrototype.ComponentRegistryEntry>();

        foreach (var markings in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markings)
            {
                foreach (var (name, entry) in GetMarkingComponents(marking.MarkingId))
                {
                    // We check if this component has been added before.
                    if (desired.TryGetValue(name, out var existingEntry))
                    {
                        // We output a warning to the log. We show the component name and old/new data for debugging.
                        Logger.Warning($"Component conflict in GetDesiredComponents! Component '{name}' is being overwritten. " +
                                    $"Existing entry data: {existingEntry.Component}, New entry from marking '{marking.MarkingId}': {entry.Component}");
                    }

                    desired[name] = entry;
                }
            }
        }

        return desired;
    }
    private IEnumerable<KeyValuePair<string, EntityPrototype.ComponentRegistryEntry>> GetMarkingComponents(string markingId)
    {
        if (!_prototype.TryIndex<MarkingPrototype>(markingId, out var prototype))
            yield break;

        if (prototype.Components.Count > 0)
        {
            foreach (var pair in prototype.Components)
                yield return pair;

            yield break;
        }

        if (!markingId.EndsWith(AnimatedSuffix))
            yield break;

        if (!_prototype.TryIndex<MarkingPrototype>(markingId[..^AnimatedSuffix.Length], out var basePrototype))
            yield break;

        foreach (var pair in basePrototype.Components)
            yield return pair;
    }
}
