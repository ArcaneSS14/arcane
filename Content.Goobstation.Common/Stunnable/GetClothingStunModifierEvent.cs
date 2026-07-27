// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Stunnable;

public sealed class GetClothingStunModifierEvent : EntityEventArgs
{
    public GetClothingStunModifierEvent(EntityUid target)
    {
        Target = target;
    }

    public EntityUid Target;
    public float Modifier = 1f;
}
