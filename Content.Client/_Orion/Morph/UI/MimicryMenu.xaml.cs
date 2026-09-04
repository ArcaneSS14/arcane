using Content.Client.UserInterface.Controls;
using Content.Shared._Orion.Morph;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
// Arcane-Start
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
// Arcane-End
using System.Numerics;

namespace Content.Client._Orion.Morph.UI;

//
// License-Identifier: AGPL-3.0-or-later
//

public sealed partial class MimicryMenu : RadialMenu
{
    [Dependency] private readonly EntityManager _ent = default!;

    public EntityUid Entity { get; private set; }
    public event Action<EntProtoId>? SendActivateMessageAction; // Arcane-Edit NetEntity > EntProtoId

    public MimicryMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
    }

    public void SetEntity(EntityUid ent)
    {
        Entity = ent;
        UpdateUI();
    }

    // Arcane-Edit-Start
    private void UpdateUI()
    {
        var main = FindControl<RadialContainer>("Main");

        main.RemoveAllChildren();

        if (!_ent.TryGetComponent<MorphComponent>(Entity, out var morph))
            return;

        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var spriteSys = _ent.System<SpriteSystem>();

        foreach (var protoId in morph.MemoryObjects)
        {
            if (!protoManager.TryIndex<EntityPrototype>(protoId, out var prototype))
                continue;

            var button = new EmbeddedEntityMenuButton
            {
                SetSize = new Vector2(64, 64),
                ToolTip = prototype.Name,
                PrototypeId = protoId
            };

            var texture = new TextureRect
            {
                SetSize = new Vector2(64, 64),
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                Texture = spriteSys.Frame0(prototype)
            };
            button.AddChild(texture);

            main.AddChild(button);
        }
        AddAction(main);
    }

    private void AddAction(RadialContainer main)
    {
        foreach (var child in main.Children)
        {
            if (child is not EmbeddedEntityMenuButton castChild)
                continue;

            castChild.OnButtonUp += _ =>
            {
                SendActivateMessageAction?.Invoke(castChild.PrototypeId);
                Close();
            };
        }
    }

    public sealed class EmbeddedEntityMenuButton : RadialMenuButtonWithSector
    {
        public EntProtoId PrototypeId;
    }
    // Arcane-Edit-End
}
