using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Goobstation.Shared.Xenomorph;

[RegisterComponent, NetworkedComponent]
public sealed partial class XenoInstantGrabComponent : Component
{
    [DataField("cooldown")]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [DataField("nextInstantGrab", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextInstantGrab = TimeSpan.Zero;
}
