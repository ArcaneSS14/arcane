using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared._Arcane.Paint;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Arcane.Paint;

public sealed class PaintedVisualizerSystem : VisualizerSystem<ArcanePaintedComponent>
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcanePaintedComponent, HeldVisualsUpdatedEvent>(OnHeldVisualsUpdated);
        SubscribeLocalEvent<ArcanePaintedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ArcanePaintedComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }


    protected override void OnAppearanceChange(EntityUid uid, ArcanePaintedComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !_appearance.TryGetData(uid, PaintVisuals.Painted, out bool isPainted))
            return;

        var shader = _protoMan.Index<ShaderPrototype>(component.ShaderName).Instance();
        var layerIndex = 0;
        foreach (var spriteLayer in args.Sprite.AllLayers)
        {
            if (spriteLayer is not Layer layer)
            {
                layerIndex++;
                continue;
            }

            if (isPainted && (layer.Shader == null || layer.Shader == shader))
            {
                component.LayerColors.TryAdd(layerIndex, layer.Color);
                layer.Shader = shader;
                layer.Color = component.Color;
            }
            else if (!isPainted && layer.Shader == shader)
            {
                layer.Shader = null;
            }

            layerIndex++;
        }
    }

    private void OnShutdown(EntityUid uid, ArcanePaintedComponent component, ref ComponentShutdown args)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (Terminating(uid))
            return;

        var shader = _protoMan.Index<ShaderPrototype>(component.ShaderName).Instance();
        var layerIndex = 0;
        foreach (var spriteLayer in sprite.AllLayers)
        {
            if (spriteLayer is not Layer layer || layer.Shader != shader)
            {
                layerIndex++;
                continue;
            }

            layer.Shader = null;
            if (component.LayerColors.TryGetValue(layerIndex, out var originalColor))
                layer.Color = originalColor;

            layerIndex++;
        }

        component.LayerColors.Clear();
    }

    private void OnHeldVisualsUpdated(EntityUid uid, ArcanePaintedComponent component, HeldVisualsUpdatedEvent args) =>
        UpdateVisuals(component, args);
    private void OnEquipmentVisualsUpdated(EntityUid uid, ArcanePaintedComponent component, EquipmentVisualsUpdatedEvent args) =>
        UpdateVisuals(component, args);
    private void UpdateVisuals(ArcanePaintedComponent component, EntityEventArgs args)
    {
        var layers = new HashSet<string>();
        var entity = EntityUid.Invalid;

        switch (args)
        {
            case HeldVisualsUpdatedEvent hgs:
                layers = hgs.RevealedLayers;
                entity = hgs.User;
                break;
            case EquipmentVisualsUpdatedEvent eqs:
                layers = eqs.RevealedLayers;
                entity = eqs.Equipee;
                break;
        }

        if (layers.Count == 0 || !TryComp(entity, out SpriteComponent? sprite))
            return;

        foreach (var revealed in layers)
        {
            if (!_sprite.LayerMapTryGet((entity, sprite), revealed, out var layer, false))
                continue;

            sprite.LayerSetShader(layer, component.ShaderName);
            _sprite.LayerSetColor((entity, sprite), layer, component.Color);
        }
    }
}
