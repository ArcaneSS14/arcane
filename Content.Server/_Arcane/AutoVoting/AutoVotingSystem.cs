using Content.Server.Voting.Managers;
using Content.Shared._Arcane.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Voting;
using Robust.Shared.Configuration;

namespace Content.Server._Arcane.AutoVoting;

public sealed partial class AutoVotingSystem : EntitySystem
{
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, ACCVars.AutoVotingEnabled, SetAutoVotingEnabled, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
    }

    private void SetAutoVotingEnabled(bool value)
    {
        _enabled = value;
    }

    private void OnRoundEnd(RoundRestartCleanupEvent args)
    {
        if (!_enabled)
            return;

        _voteManager.CreateStandardVote(null, StandardVoteType.Preset);
        _voteManager.CreateStandardVote(null, StandardVoteType.Map);
    }
}
