using Content.Client._Arcane.ERP.Preferences;
using Content.Client.Inventory;
using Content.Client.Lobby;
using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ERP.Organs;
using Content.Shared._Arcane.ERP.OrgansAppearance;
using Content.Shared._Arcane.ERP.Preferences;
using Content.Shared._Shitmed.Humanoid.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.ERP.OrgansAppearance;

public sealed class ErpOrganVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly ClientErpOrganPreferencesManager _erpPrefs = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    // (slot, species) → prototype; built from erpOrganVisual prototypes at Initialize.
    private readonly Dictionary<(string slot, string species), ErpOrganVisualPrototype> _speciesLookup = new();
    // slot → fallback prototype (species list was empty).
    private readonly Dictionary<string, ErpOrganVisualPrototype> _fallbackLookup = new();
    // slot → layer key ("erp_{slot}"); derived from prototypes so new slots need no C#.
    private readonly Dictionary<string, string> _slotToLayerKey = new();
    // slot -> sprite draw order; lower values render below higher values.
    private readonly Dictionary<string, int> _slotDrawOrder = new();
    private readonly List<string> _orderedSlots = new();
    // preview entity → character slot index; set by RefreshPreview so OnPreviewProfileLoaded uses the correct slot.
    private readonly Dictionary<EntityUid, int> _previewSlots = new();
    // preview entity -> last explicit preferences; used when preview has no backing character slot.
    private readonly Dictionary<EntityUid, ErpOrganPreferences> _previewPrefs = new();

    public override void Initialize()
    {
        base.Initialize();

        BuildLookupTables();
        _proto.PrototypesReloaded += _ => BuildLookupTables();

        SubscribeLocalEvent<ErpOrganVisualsComponent, AfterAutoHandleStateEvent>(OnOrganState);
        SubscribeLocalEvent<ErpOrganVisualsComponent, ComponentShutdown>(OnOrganShutdown);

        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidVisualStateUpdatedEvent>(OnHumanoidState);
        SubscribeLocalEvent<ArousalComponent, AfterAutoHandleStateEvent>(OnArousalState);

        // Editor preview: client-side dummy entity, no server state.
        SubscribeLocalEvent<HumanoidAppearanceComponent, ProfileLoadFinishedEvent>(OnPreviewProfileLoaded);
    }

    private void BuildLookupTables()
    {
        _speciesLookup.Clear();
        _fallbackLookup.Clear();
        _slotToLayerKey.Clear();
        _slotDrawOrder.Clear();
        _orderedSlots.Clear();

        foreach (var proto in _proto.EnumeratePrototypes<ErpOrganVisualPrototype>())
        {
            _slotToLayerKey.TryAdd(proto.Slot, proto.LayerKey);
            _slotDrawOrder.TryAdd(proto.Slot, proto.DrawOrder);
            if (!_orderedSlots.Contains(proto.Slot))
                _orderedSlots.Add(proto.Slot);

            if (proto.Species.Count == 0)
                _fallbackLookup[proto.Slot] = proto;
            else
                foreach (var species in proto.Species)
                    _speciesLookup[(proto.Slot, species)] = proto;
        }

        _orderedSlots.Sort(CompareSlotsByDrawOrder);
    }

    private ErpOrganVisualPrototype? GetProto(string slot, string species)
    {
        if (_speciesLookup.TryGetValue((slot, species), out var proto))
            return proto;

        _fallbackLookup.TryGetValue(slot, out var fallback);
        return fallback;
    }

    public void RefreshPreview(EntityUid uid, ErpOrganPreferences prefs, int? slot, ArousalPhase phase = ArousalPhase.Calm)
    {
        if (!IsClientSide(uid))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (slot is { } slotIndex)
            _previewSlots[uid] = slotIndex;
        else
            _previewSlots.Remove(uid);

        _previewPrefs[uid] = prefs;

        var humanoid = CompOrNull<HumanoidAppearanceComponent>(uid);
        var visuals = EnsureComp<ErpOrganVisualsComponent>(uid);
        visuals.Organs = BuildPreviewOrgans(prefs, humanoid);
        visuals.CoveredSlots = GetPreviewCoveredSlots(uid);
        visuals.HideWhenFlaccid = GetPreviewHideWhenFlaccid(uid);

        ApplyOrganLayers((uid, visuals), humanoid, sprite, phase);
    }

    private void OnPreviewProfileLoaded(Entity<HumanoidAppearanceComponent> ent, ref ProfileLoadFinishedEvent args)
    {
        if (!IsClientSide(ent))
            return;

        if (!HasComp<EroticOrgansComponent>(ent))
        {
            var disabledVisuals = EnsureComp<ErpOrganVisualsComponent>(ent);
            disabledVisuals.Organs = [];
            disabledVisuals.CoveredSlots = [];
            disabledVisuals.HideWhenFlaccid = [];

            if (TryComp<SpriteComponent>(ent, out var disabledSprite))
                ApplyOrganLayers((ent, disabledVisuals), ent.Comp, disabledSprite);

            return;
        }

        var organPrefs = _previewSlots.TryGetValue(ent, out var slot)
            ? _erpPrefs.GetSlot(slot)
            : _previewPrefs.GetValueOrDefault(ent, ErpOrganPreferences.Default());

        var visuals = EnsureComp<ErpOrganVisualsComponent>(ent);
        visuals.Organs = BuildPreviewOrgans(organPrefs, ent.Comp);
        visuals.CoveredSlots = GetPreviewCoveredSlots(ent);
        visuals.HideWhenFlaccid = GetPreviewHideWhenFlaccid(ent);

        if (TryComp<SpriteComponent>(ent, out var sprite))
            ApplyOrganLayers((ent, visuals), ent.Comp, sprite);
    }

    private Dictionary<string, ErpOrganConfig> BuildPreviewOrgans(
        ErpOrganPreferences prefs,
        HumanoidAppearanceComponent? humanoid)
    {
        var sex = humanoid?.Sex ?? Sex.Male;
        var definitions = ErpOrganEditorDefinitions.GetForSpecies(humanoid?.Species, sex, _proto, _componentFactory);
        var normalized = ErpOrganPreferencesNormalizer.Normalize(prefs, definitions);

        foreach (var definition in definitions)
            normalized.Organs.TryAdd(definition.SlotId, ErpOrganEditorDefinitions.CreateDefaultConfig(definition));

        return FilterOrgansBySex(normalized.Organs, sex);
    }

    private static Dictionary<string, ErpOrganConfig> FilterOrgansBySex(
        Dictionary<string, ErpOrganConfig> organs, Sex sex)
    {
        var result = new Dictionary<string, ErpOrganConfig>();
        foreach (var (slotId, cfg) in organs)
        {
            if (ErpOrganSlots.SexFilter.TryGetValue(slotId, out var allowed) && Array.IndexOf(allowed, sex) < 0)
                continue;
            result[slotId] = cfg;
        }
        return result;
    }

    private HashSet<string> GetPreviewHideWhenFlaccid(EntityUid uid)
        => TryComp<EroticOrgansComponent>(uid, out var organs) ? new HashSet<string>(organs.HideWhenFlaccid) : [];

    private HashSet<string> GetPreviewCoveredSlots(EntityUid uid)
    {
        var covered = new HashSet<string>();
        var enumerator = _inventory.GetSlotEnumerator(uid);
        while (enumerator.NextItem(out var item))
        {
            if (!TryComp<HideLayerClothingComponent>(item, out var hideLayer) ||
                !TryComp<ClothingComponent>(item, out var clothing))
            {
                continue;
            }

            var inSlot = clothing.InSlotFlag ?? SlotFlags.NONE;
            if (inSlot == SlotFlags.NONE || !IsHideLayerEnabled(item, hideLayer))
                continue;

            foreach (var (layer, validSlots) in hideLayer.Layers)
            {
                if (validSlots.HasFlag(inSlot))
                    AddCoveredSlotsForLayer(covered, layer);
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (hideLayer.Slots is { } slots && clothing.Slots.HasFlag(inSlot))
#pragma warning restore CS0618 // Type or member is obsolete
            {
                foreach (var layer in slots)
                    AddCoveredSlotsForLayer(covered, layer);
            }
        }

        return covered;
    }

    private static void AddCoveredSlotsForLayer(HashSet<string> covered, HumanoidVisualLayers layer)
    {
        if (layer == HumanoidVisualLayers.ErpGroin)
        {
            covered.Add(ErpOrganSlots.Penis);
            covered.Add(ErpOrganSlots.Testicles);
            covered.Add(ErpOrganSlots.Vagina);
            covered.Add(ErpOrganSlots.Anus);
            covered.Add(ErpOrganSlots.Butt);
        }

        if (layer == HumanoidVisualLayers.ErpChest)
            covered.Add(ErpOrganSlots.Breasts);
    }

    private bool IsHideLayerEnabled(EntityUid uid, HideLayerClothingComponent hideLayer)
    {
        if (!hideLayer.HideOnToggle)
            return true;

        if (!TryComp<MaskComponent>(uid, out var mask))
            return true;

        return !mask.IsToggled;
    }

    private void OnOrganState(Entity<ErpOrganVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ApplyOrganLayers(ent, CompOrNull<HumanoidAppearanceComponent>(ent), sprite);
    }

    private void OnHumanoidState(Entity<HumanoidAppearanceComponent> ent, ref HumanoidVisualStateUpdatedEvent args)
    {
        if (!TryComp<ErpOrganVisualsComponent>(ent, out var visuals))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ApplyOrganLayers((ent, visuals), ent.Comp, sprite);
    }

    private void OnArousalState(Entity<ArousalComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<ErpOrganVisualsComponent>(ent, out var visuals))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ApplyOrganLayers((ent, visuals), CompOrNull<HumanoidAppearanceComponent>(ent), sprite);
    }

    private void OnOrganShutdown(Entity<ErpOrganVisualsComponent> ent, ref ComponentShutdown args)
    {
        _previewSlots.Remove(ent);
        _previewPrefs.Remove(ent);

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        RemoveOrganLayers(ent, sprite);
    }

    private void ApplyOrganLayers(
        Entity<ErpOrganVisualsComponent> ent,
        HumanoidAppearanceComponent? humanoid,
        SpriteComponent sprite,
        ArousalPhase? phaseOverride = null)
    {
        var phase = phaseOverride ?? CompOrNull<ArousalComponent>(ent)?.CurrentPhase ?? ArousalPhase.Calm;
        var species = humanoid?.Species ?? string.Empty;

        if (!OrganLayerOrderMatches(ent, sprite))
            RemoveOrganLayers(ent, sprite);

        foreach (var slotId in _orderedSlots)
        {
            if (!_slotToLayerKey.TryGetValue(slotId, out var layerKey))
                continue;

            var proto = GetProto(slotId, species);
            if (proto == null)
            {
                // No RSI registered — remove any stale layer (index is stable here since we process in slot order,
                // not sprite index order; caller rebuilds from scratch after OrganLayerOrderMatches fails).
                if (_sprite.LayerMapTryGet((ent, sprite), layerKey, out var staleIdx, false))
                {
                    _sprite.LayerSetVisible((ent, sprite), staleIdx, false);
                    _sprite.LayerMapRemove((ent, sprite), layerKey);
                    _sprite.RemoveLayer((ent, sprite), staleIdx);
                }
                continue;
            }

            if (!ent.Comp.Organs.TryGetValue(slotId, out var cfg))
            {
                if (_sprite.LayerMapTryGet((ent, sprite), layerKey, out var hiddenIdx, false))
                    _sprite.LayerSetVisible((ent, sprite), hiddenIdx, false);
                continue;
            }

            var rsiPath = proto.Rsi;
            var stateName = ResolveStateName(proto, cfg, phase);
            var visible = !ent.Comp.CoveredSlots.Contains(slotId)
                         && (!ent.Comp.HideWhenFlaccid.Contains(slotId) || phase >= ArousalPhase.Aroused);

            if (!_sprite.LayerMapTryGet((ent, sprite), layerKey, out var index, false))
            {
                index = _sprite.AddLayer(
                    (ent, sprite),
                    new SpriteSpecifier.Rsi(new ResPath(rsiPath), stateName),
                    GetOrganLayerInsertIndex(ent, sprite, slotId));
                _sprite.LayerMapSet((ent, sprite), layerKey, index);
            }

            _sprite.LayerSetRsi((ent, sprite), index, new ResPath(rsiPath), stateName);
            _sprite.LayerSetColor((ent, sprite), index, cfg.Color ?? humanoid?.SkinColor ?? Color.FromHex("#C0967F"));
            _sprite.LayerSetVisible((ent, sprite), index, visible);
        }
    }

    private int? GetOrganLayerInsertIndex(Entity<ErpOrganVisualsComponent> ent, SpriteComponent sprite, string slotId)
    {
        var insertIdx = GetFirstEquipmentLayerIndex(ent.Owner, sprite);

        var reachedSlot = false;
        foreach (var otherSlot in _orderedSlots)
        {
            if (otherSlot == slotId)
            {
                reachedSlot = true;
                continue;
            }

            if (!_slotToLayerKey.TryGetValue(otherSlot, out var otherLayerKey))
                continue;

            if (!_sprite.LayerMapTryGet((ent, sprite), otherLayerKey, out var otherIdx, false))
                continue;

            if (!reachedSlot)
            {
                insertIdx = otherIdx + 1;
                continue;
            }

            return insertIdx.HasValue
                ? Math.Min(insertIdx.Value, otherIdx)
                : otherIdx;
        }

        return insertIdx;
    }

    private bool OrganLayerOrderMatches(Entity<ErpOrganVisualsComponent> ent, SpriteComponent sprite)
    {
        var previousIdx = -1;
        var clothingLayer = GetFirstEquipmentLayerIndex(ent.Owner, sprite);

        foreach (var slotId in _orderedSlots)
        {
            if (!_slotToLayerKey.TryGetValue(slotId, out var layerKey))
                continue;

            if (!_sprite.LayerMapTryGet((ent, sprite), layerKey, out var index, false))
                continue;

            if (clothingLayer != null && index >= clothingLayer)
                return false;

            if (index < previousIdx)
                return false;

            previousIdx = index;
        }

        return true;
    }

    private void RemoveOrganLayers(Entity<ErpOrganVisualsComponent> ent, SpriteComponent sprite)
    {
        var toRemove = new List<(string Key, int Index)>();
        foreach (var layerKey in _slotToLayerKey.Values)
        {
            if (_sprite.LayerMapTryGet((ent, sprite), layerKey, out var index, false))
                toRemove.Add((layerKey, index));
        }
        // Remove highest indices first so lower indices are not shifted by earlier removals.
        toRemove.Sort(static (a, b) => b.Index.CompareTo(a.Index));
        foreach (var (key, index) in toRemove)
        {
            _sprite.LayerSetVisible((ent, sprite), index, false);
            _sprite.LayerMapRemove((ent, sprite), key);
            _sprite.RemoveLayer((ent, sprite), index);
        }
    }

    private int? GetFirstEquipmentLayerIndex(EntityUid uid, SpriteComponent sprite)
    {
        if (!TryComp<InventorySlotsComponent>(uid, out var inventorySlots))
            return null;

        int? firstIdx = null;
        foreach (var layerKeys in inventorySlots.VisualLayerKeys.Values)
        {
            foreach (var layerKey in layerKeys)
            {
                if (!_sprite.LayerMapTryGet((uid, sprite), layerKey, out var idx, false))
                    continue;

                firstIdx = firstIdx.HasValue ? Math.Min(firstIdx.Value, idx) : idx;
            }
        }

        return firstIdx;
    }

    private int CompareSlotsByDrawOrder(string left, string right)
    {
        var orderCompare = GetDrawOrder(left).CompareTo(GetDrawOrder(right));
        return orderCompare != 0
            ? orderCompare
            : string.CompareOrdinal(left, right);
    }

    private int GetDrawOrder(string slotId)
        => _slotDrawOrder.TryGetValue(slotId, out var order) ? order : 0;

    private static string ResolveStateName(ErpOrganVisualPrototype proto, ErpOrganConfig cfg, ArousalPhase phase)
    {
        return proto.StateMode switch
        {
            ErpStateMode.Fixed => proto.FixedState,

            ErpStateMode.SizeString => Math.Clamp(cfg.Size, 1, 99).ToString(),

            ErpStateMode.SizeIndexed when proto.SizeStates.Count > 0 =>
                proto.SizeStates[Math.Clamp(cfg.Size - 1, 0, proto.SizeStates.Count - 1)],

            ErpStateMode.Arousal =>
                phase >= ArousalPhase.Aroused && proto.ArousalStates.TryGetValue(phase.ToString(), out var aroused)
                    ? aroused
                    : proto.FlaccidState,

            ErpStateMode.Variant =>
                proto.VariantAliases.TryGetValue(cfg.Variant, out var alias) ? alias : cfg.Variant,

            ErpStateMode.SizeIndexedArousal when proto.SizeStates.Count > 0 =>
                phase >= ArousalPhase.Aroused
                    ? proto.SizeStates[Math.Clamp(cfg.Size - 1, 0, proto.SizeStates.Count - 1)]
                    : proto.FlaccidState,

            _ => cfg.Variant,
        };
    }
}
