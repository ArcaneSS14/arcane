// SPDX-License-Identifier: MIT

using Content.Shared.Research.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared._Orion.Research;

namespace Content.Client.Research;

public sealed class ResearchSystem : SharedResearchSystem
{
    // Orion-Start
    public List<ResearchPointAmount> GetTechnologyFinalPointCostsForUi(
        TechnologyDatabaseComponent database,
        TechnologyPrototype technology)
    {
        return GetTechnologyFinalPointCosts(database, technology);
    }
    // Orion-End
}
