using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class StationAddJobCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;

    private static readonly string[] Modes = { "set", "add", "remove" };

    public override string Command => "stationaddjob";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 4)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netStation)
            || !EntityManager.TryGetEntity(netStation, out var station)
            || !EntityManager.TryGetComponent<StationJobsComponent>(station, out var jobs))
        {
            shell.WriteError(Loc.GetString("cmd-stationaddjob-invalid-station", ("station", args[0])));
            return;
        }

        if (!_proto.TryIndex<JobPrototype>(args[1], out var job))
        {
            shell.WriteError(Loc.GetString("cmd-stationaddjob-invalid-job", ("job", args[1])));
            return;
        }

        var mode = args[2].ToLowerInvariant();
        if (!Modes.Contains(mode))
        {
            shell.WriteError(Loc.GetString("cmd-stationaddjob-invalid-mode", ("mode", args[2])));
            return;
        }

        if (!int.TryParse(args[3], out var amount) || amount < -1 || amount == -1 && mode != "set")
        {
            shell.WriteError(Loc.GetString("cmd-stationaddjob-invalid-amount"));
            return;
        }

        var uid = station.Value;
        var stationName = EntityManager.GetComponent<MetaDataComponent>(uid).EntityName;

        if (mode == "set")
        {
            if (amount == -1)
                _stationJobs.MakeJobUnlimited(uid, job, jobs);
            else
                _stationJobs.TrySetJobSlot(uid, job, amount, createSlot: true, stationJobs: jobs);
        }
        else
        {
            if (_stationJobs.IsJobUnlimited(uid, job, jobs))
            {
                shell.WriteError(Loc.GetString("cmd-stationaddjob-unlimited-adjust",
                    ("job", job.LocalizedName), ("station", stationName)));
                return;
            }

            if (mode == "remove" && !_stationJobs.TryGetJobSlot(uid, job, out _, jobs))
            {
                shell.WriteError(Loc.GetString("cmd-stationaddjob-no-slot",
                    ("job", job.LocalizedName), ("station", stationName)));
                return;
            }

            var delta = mode == "add" ? amount : -amount;
            _stationJobs.TryAdjustJobSlot(uid, job, delta, createSlot: true, clamp: true, stationJobs: jobs);
        }

        _stationJobs.TryGetJobSlot(uid, job, out var slots, jobs);

        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{shell.Player?.Name ?? "Server"} changed {job.ID} slots on {EntityManager.ToPrettyString(uid)} ({mode} {amount}), now {slots?.ToString() ?? "unlimited"}");

        if (slots == null)
        {
            shell.WriteLine(Loc.GetString("cmd-stationaddjob-result-unlimited",
                ("job", job.LocalizedName), ("station", stationName)));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-stationaddjob-result",
            ("job", job.LocalizedName), ("station", stationName), ("total", slots.Value)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(GetStations(), Loc.GetString("cmd-stationaddjob-hint-station")),
            2 => CompletionResult.FromHintOptions(GetJobs(), Loc.GetString("cmd-stationaddjob-hint-job")),
            3 => CompletionResult.FromHintOptions(Modes.Select(m => new CompletionOption(m, Loc.GetString($"cmd-stationaddjob-mode-{m}"))),
                Loc.GetString("cmd-stationaddjob-hint-mode")),
            4 when args[2].Equals("set", StringComparison.OrdinalIgnoreCase) =>
                CompletionResult.FromHintOptions(new[] { new CompletionOption("-1", Loc.GetString("cmd-stationaddjob-hint-unlimited")) },
                    Loc.GetString("cmd-stationaddjob-hint-amount")),
            4 => CompletionResult.FromHint(Loc.GetString("cmd-stationaddjob-hint-amount")),
            _ => CompletionResult.Empty,
        };
    }

    private IEnumerable<CompletionOption> GetStations()
    {
        var query = EntityManager.EntityQueryEnumerator<StationJobsComponent, StationDataComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var meta))
        {
            yield return new CompletionOption(EntityManager.GetNetEntity(uid).ToString(), meta.EntityName);
        }
    }

    private IEnumerable<CompletionOption> GetJobs()
    {
        return _proto.EnumeratePrototypes<JobPrototype>()
            .OrderBy(j => j.ID)
            .Select(j => new CompletionOption(j.ID, j.LocalizedName));
    }
}
