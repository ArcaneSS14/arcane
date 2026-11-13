// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Arcane.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Timing;

namespace Content.Shared._Arcane.Surgery;

public sealed partial class SurgeryRevivalSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SurgeryToolExamineSystem _toolExamine = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryDefibrillatorComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
    }

    [SubscribeLocalEvent]
    private void OnCompleteCheck(Entity<SurgeryRevivalStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_mobState.IsDead(args.Body))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnDefibrillatorUsed(Entity<SurgeryDefibrillatorComponent> ent, ref SurgeryToolUsedEvent args)
    {
        if (args.Cancelled || !TryComp<DefibrillatorComponent>(ent, out var defib))
            return;

        if (_useDelay.IsDelayed(ent.Owner, defib.DelayId))
        {
            _popup.PopupClient(Loc.GetString("surgery-tool-defibrillator-charging"), ent, args.User);
            args.Cancelled = true;
            return;
        }

        if (!_powerCell.HasActivatableCharge(ent.Owner, args.User, predicted: true))
            args.Cancelled = true;
    }
}
