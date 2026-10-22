using Content.Server.Administration;
using Content.Server.Heretic.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Heretic;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Administration.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class HereticAddTargetCommand : LocalizedEntityCommands
{
    [Dependency] private readonly HereticSystem _heretic = default!;

    private static readonly ProtoId<JobPrototype> FallbackJob = "Passenger";

    public override string Command => "hereticaddtarget";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netHeretic)
            || !EntityManager.TryGetEntity(netHeretic, out var heretic)
            || !_heretic.TryGetHereticComponent(heretic.Value, out var hereticComp, out var mind))
        {
            shell.WriteError(Loc.GetString("cmd-hereticaddtarget-invalid-heretic", ("entity", args[0])));
            return;
        }

        if (!NetEntity.TryParse(args[1], out var netTarget)
            || !EntityManager.TryGetEntity(netTarget, out var target)
            || !EntityManager.TryGetComponent<HumanoidAppearanceComponent>(target, out var humanoid))
        {
            shell.WriteError(Loc.GetString("cmd-hereticaddtarget-invalid-target", ("entity", args[1])));
            return;
        }

        if (hereticComp.SacrificeTargets.Exists(x => x.Entity == netTarget))
            return;

        hereticComp.SacrificeTargets.Add(new SacrificeTargetData
        {
            Entity = netTarget,
            Profile = HumanoidCharacterProfile.DefaultWithSpecies(humanoid.Species),
            Job = FallbackJob,
        });
        EntityManager.Dirty(mind, hereticComp);

        shell.WriteLine(Loc.GetString("cmd-hereticaddtarget-success", ("entity", args[1])));
    }
}
