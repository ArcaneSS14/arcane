using Content.Shared._Arcane.CCVars;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._Arcane.RoundEndPacification;

public sealed partial class RoundEndPacificationSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _endRoundPacification;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);

        Subs.CVar(_cfg, ACCVars.EndRoundPacification, OnEndRoundPacificationChanged, true);
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        if (!_endRoundPacification)
            return;

        var query = EntityQueryEnumerator<ActorComponent>();

        while (query.MoveNext(out var uid, out var _))
        {
            EnsureComp<PacifiedComponent>(uid);
        }
    }

    private void OnEndRoundPacificationChanged(bool value)
    {
        _endRoundPacification = value;
    }
}
