// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Arcane.CCVars;
using Content.Shared._ST.Interaction;
using Robust.Client.Player;

namespace Content.Client._ST.Interaction;

public sealed partial class StellarInteractionParticleSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private bool _showOverheadInteractionParticle = true;

    private void InitializeArcane()
    {
        Subs.CVar(_cfg, ACCVars.ShowOverheadInteractionParticle, v => _showOverheadInteractionParticle = v, true);
    }

    private bool CanShowInteractionParticle(EntityUid performer, StellarInteractionParticleType type)
    {
        if (_player.LocalEntity != performer)
            return false;

        return _showOverheadInteractionParticle;
    }
}
