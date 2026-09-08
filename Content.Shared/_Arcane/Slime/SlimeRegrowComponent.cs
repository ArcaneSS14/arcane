// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Slime;

/// <summary>
/// Allows slime people to regrow a missing limb at the cost of
/// their own food and water reserves.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class SlimeRegrowComponent : Component
{
    /// <summary>
    /// The action that triggers the limb regrow.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEnt;

    /// <summary>
    /// The action prototype granted when the entity is initialized.
    /// </summary>
    [DataField]
    public EntProtoId ActionId = "ActionSlimeRegrowLimb";

    /// <summary>
    /// Hunger consumed each time a limb is regrown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HungerCost = 60f;

    /// <summary>
    /// Thirst consumed each time a limb is regrown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ThirstCost = 100f;

    /// <summary>
    /// Popup shown when a limb is regrown successfully.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId RegrowPopup = "slime-regrow-limb-success";

    /// <summary>
    /// Popup shown when there is no missing non-vital limb to regrow.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId NoLimbPopup = "slime-regrow-limb-none";

    /// <summary>
    /// Popup shown when the entity does not have enough hunger to regrow a limb.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId TooHungryPopup = "slime-regrow-limb-too-hungry";

    /// <summary>
    /// Popup shown when the entity does not have enough thirst to regrow a limb.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId TooThirstyPopup = "slime-regrow-limb-too-thirsty";

    /// <summary>
    /// Sound played when a limb is regrown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Voice/Slime/slime_squish.ogg");
}
