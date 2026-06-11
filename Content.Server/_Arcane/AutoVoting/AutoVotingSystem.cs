using Content.Server.Voting.Managers;
using Content.Shared.GameTicking;
using Content.Shared.Voting;
using Robust.Server.Player;
using System.Linq;

namespace Content.Server._Arcane.AutoVoting;

public sealed partial class AutoVotingSystem : EntitySystem
{
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(RoundRestartCleanupEvent args)
    {
        _voteManager.CreateStandardVote(null, StandardVoteType.Preset);
        _voteManager.CreateStandardVote(null, StandardVoteType.Map);
    }
}
