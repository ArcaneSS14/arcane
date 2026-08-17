// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server.Fax;
using Content.Server.StationEvents.Events;
using Content.Server._Arcane.StationEvents.Components;
using Content.Shared.Dataset;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Arcane.StationEvents.Events;

/// <summary>
/// An event related to a strange law from the Central Command. I tried to set it up so that new laws could be added without having to edit the code. The dataset file contains instructions.
/// </summary>
public sealed class WackyLawRule : StationEventSystem<WackyLawComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private string GetDepartmentText(string department)
    {
        if (department == "All")
            return Loc.GetString("wacky-law-department-all");

        return _proto.TryIndex<DepartmentPrototype>(department, out var dept)
            ? Loc.GetString(dept.Name)
            : department;
    }
    private string PickRandomEntityName(ProtoId<DatasetPrototype> datasetId)
    {
        var dataset = _proto.Index(datasetId);
        var protoId = _random.Pick(dataset.Values);

        if (_proto.TryIndex<EntityPrototype>(protoId, out var proto) && !string.IsNullOrEmpty(proto.Name))
            return Loc.GetString(proto.Name);
        return protoId;
    }

    protected override void Started(EntityUid uid, WackyLawComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        var lawEntry = _random.Pick(_proto.Index(component.LawsDataset).Values);
        var lawParts = lawEntry.Split('|', 2);
        var contentLoc = lawParts[0];
        var itemName = string.Empty;

        if (lawParts.Length == 2 && _proto.TryIndex<DatasetPrototype>(lawParts[1], out _))
            itemName = PickRandomEntityName(lawParts[1]);

        var departmentEntry = _random.Pick(_proto.Index(component.DepartmentDataset).Values);
        var departmentText = GetDepartmentText(departmentEntry);
        var content = Loc.GetString(
            contentLoc,
            ("station", MetaData(station.Value).EntityName),
            ("item", itemName),
            ("department", departmentText)
        );

        var documentRelease = new FaxPrintout(
            content,
            Loc.GetString("wacky-law-document-title"),
            label: null,
            prototypeId: "PaperOffice",
            stampState: "paper_stamp-centcom",
            stampedBy: new List<StampDisplayInfo>
            {
                new()
                {
                    StampedName = Loc.GetString("stamp-component-stamped-name-centcom"),
                    StampedColor = Color.Green,
                }
            },
            locked: true
        );

        var faxQuery = EntityQueryEnumerator<FaxMachineComponent>();
        while (faxQuery.MoveNext(out var faxUid, out var faxComp))
        {
            if (!faxComp.ReceiveAllStationGoals && !(faxComp.ReceiveStationGoal && StationSystem.GetOwningStation(faxUid) == station.Value))
                continue;

            _fax.Receive(faxUid, documentRelease, null, faxComp);
        }
    }
}
