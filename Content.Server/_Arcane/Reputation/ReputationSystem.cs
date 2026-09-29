// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._Arcane;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._Arcane.Reputation;

/// <summary>
/// Awards reputation at round end. Surviving the shift is worth a flat amount, antags additionally
/// earn based on how many of their objectives they completed.
/// </summary>
public sealed class ReputationSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ReputationManager _repManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    /// <summary>
    /// Anti-farm guards: reputation is only handed out on rounds that were actually played out.
    /// </summary>
    private const int MinPlayers = 15;

    private const int MinRoundLength = 25;
    private const int MinTimePlayerConnected = 20;

    /// <summary>
    /// Tries to modify reputation on round end and then returns its new value and delta if successful.
    /// </summary>
    /// <param name="uid">Player to get new values for.</param>
    /// <param name="newValue">Modified player's reputation value.</param>
    /// <param name="deltaValue">How much was added this round.</param>
    /// <returns>Whether the player's reputation could be resolved at all.</returns>
    public bool TryModifyReputationOnRoundEnd(NetUserId uid, out float? newValue, out float? deltaValue)
    {
        newValue = null;
        deltaValue = null;

        // if (!_cfg.GetCVar(WhiteCVars.ReputationEnabled))
        //     return false;

        if (!_playerManager.TryGetSessionById(uid, out var session) || session.AttachedEntity == null)
            return false;

        if (!TryCalculatePlayerReputation(session.AttachedEntity.Value, out var delta))
            return false;

        if (!_repManager.GetCachedPlayerReputation(uid, out var value) || value == null)
            return false;

        var longConnected = _repManager.GetCachedPlayerConnection(uid, out var date)
                            && DateTime.UtcNow - date >= TimeSpan.FromMinutes(MinTimePlayerConnected);
        var longRound = _gameTicker.RoundDuration() >= TimeSpan.FromMinutes(MinRoundLength);
        var enoughPlayers = _playerManager.PlayerCount >= MinPlayers;
        var earned = longRound && longConnected && enoughPlayers;

        if (delta != 0 && earned)
            _repManager.ModifyPlayerReputation(uid, delta);

        deltaValue = earned ? delta : 0f;
        newValue = value + deltaValue;

        return true;
    }

    private bool TryCalculatePlayerReputation(EntityUid entity, out float deltaValue)
    {
        deltaValue = 0f;

        // Dead players get nothing.
        if (!TryComp<MobStateComponent>(entity, out var state) || state.CurrentState is MobState.Dead or MobState.Invalid)
            return true;

        // Flat reward for surviving the shift.
        deltaValue += 1f;

        if (!TryComp<MindContainerComponent>(entity, out var mind)
            || mind.Mind == null
            || !_roles.MindIsAntagonist(mind.Mind)
            || !TryComp(mind.Mind, out MindComponent? mindComp))
        {
            return true;
        }

        var objCompleted = 0;
        var totalObj = 0;

        foreach (var obj in mindComp.Objectives)
        {
            totalObj++;

            var info = _objectives.GetInfo(obj, mind.Mind.Value, mindComp);

            if (info is { Progress: > 0.99f })
                objCompleted++;
        }

        if (totalObj > 0 && objCompleted == totalObj)
            deltaValue += 2f + objCompleted * 0.5f;
        else
            deltaValue += objCompleted * 0.5f;

        return true;
    }
}
