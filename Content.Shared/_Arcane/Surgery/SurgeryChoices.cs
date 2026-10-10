// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

public static class SurgeryChoices
{
    public static bool Equal<TPart>(Dictionary<TPart, List<EntProtoId>> a, Dictionary<TPart, List<EntProtoId>> b)
        where TPart : notnull
    {
        if (a.Count != b.Count)
            return false;

        foreach (var (part, surgeries) in a)
        {
            if (!b.TryGetValue(part, out var other) || !surgeries.SequenceEqual(other))
                return false;
        }

        return true;
    }
}
