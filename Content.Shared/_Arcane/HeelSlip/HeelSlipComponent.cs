using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.HeelSlip;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HeelSlipComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SlipChance = 0.0005f;

    [DataField, AutoNetworkedField]
    public float RunSpeedThreshold = 3.0f;

    [DataField, AutoNetworkedField]
    public float SlowdownModifier = 0.25f;

    [DataField, AutoNetworkedField]
    public float SlowdownDurationMin = 3f;

    [DataField, AutoNetworkedField]
    public float SlowdownDurationMax = 5f;

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);
}
