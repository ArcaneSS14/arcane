using System.Linq;
using Content.Server._Arcane.GameTicking;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Voting.Managers;
using Content.Shared._Arcane.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Random.Helpers;
using Content.Shared.Voting;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.AutoVoting;

public sealed partial class AutoVotingSystem : EntitySystem
{
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    private bool _enabled;
    private readonly GamePresetHistory _history = new();
    private int _restartGeneration;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, ACCVars.AutoVotingEnabled, SetAutoVotingEnabled, true);
        Subs.CVar(_cfg, ACCVars.GamePresetHistorySize, size => _history.Capacity = size, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void SetAutoVotingEnabled(bool value)
    {
        _enabled = value;
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        if (_ticker.CurrentPreset is { } preset)
            _history.Record(preset.ID);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        var generation = ++_restartGeneration;
        if (!_enabled || !_ticker.LobbyEnabled || _ticker.DummyTicker)
            return;

        Timer.Spawn(TimeSpan.FromSeconds(25), () =>
        {
            if (!_enabled || generation != _restartGeneration || !_ticker.CanUpdateMap())
                return;

            _voteManager.CreateStandardVote(null, StandardVoteType.Map);
            TrySelectGamePreset();
        });
    }

    private bool TrySelectGamePreset()
    {
        var weights = new Dictionary<GamePresetPrototype, float>();
        foreach (var preset in _prototypes.EnumeratePrototypes<GamePresetPrototype>())
        {
            if (!preset.ShowInVote ||
                _players.PlayerCount < (preset.MinPlayers ?? int.MinValue) ||
                _players.PlayerCount > (preset.MaxPlayers ?? int.MaxValue))
                continue;

            weights.Add(preset, _history.GetWeight(preset.ID,
                preset.RepeatWeightMultiplier,
                preset.PreviousRoundWeightMultiplier));
        }

        if (weights.Count == 0)
            return false;

        var selected = _random.Pick(weights);
        _ticker.SetGamePreset(selected);

        _chat.DispatchServerAnnouncement(Loc.GetString("game-preset-random-selected",
            ("preset", Loc.GetString(selected.ModeTitle))));

        var totalWeight = weights.Values.Sum();
        var chances = weights.Select(entry => Loc.GetString("game-preset-random-chance",
            ("preset", Loc.GetString(entry.Key.ModeTitle)),
            ("chance", entry.Value / totalWeight * 100f)));

        _chat.DispatchServerAnnouncement(Loc.GetString("game-preset-random-chances",
            ("chances", string.Join("\n", chances))));

        return true;
    }
}
