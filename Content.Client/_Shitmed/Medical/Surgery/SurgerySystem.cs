// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.UserInterface;

namespace Content.Client._Shitmed.Medical.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!; // Arcane

    public override void Initialize()
    {
        base.Initialize();
    }

    // Arcane-Start
    protected override void RefreshUI(EntityUid body)
    {
        if (_ui.TryGetOpenUi<SurgeryBui>(body, SurgeryUIKey.Key, out var bui))
            bui.RefreshUI();
    }
    // Arcane-End
}
