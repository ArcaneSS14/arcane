// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Clothing;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class ArcaneClothingLayerData
{
    [DataField("layer")]
    public PrototypeLayerData Layer = new();

    [DataField("whitelist")]
    public EntityWhitelist? Whitelist;
}
