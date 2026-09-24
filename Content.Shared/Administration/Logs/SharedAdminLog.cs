// SPDX-License-Identifier: MIT

using Content.Shared.Database;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration.Logs;

[Serializable, NetSerializable]
public readonly record struct SharedAdminLog(
    int Id,
    LogType Type,
    LogImpact Impact,
    DateTime Date,
    string Message,
    Guid[] Players,
    int RoundId); // Arcane

// Arcane-Edit-Start
[Serializable, NetSerializable]
public readonly record struct AdminLogCursor(DateTime Date, int RoundId, int Id) : IComparable<AdminLogCursor>
{
    public int CompareTo(AdminLogCursor other)
    {
        var dateOrder = Date.CompareTo(other.Date);
        if (dateOrder != 0)
            return dateOrder;

        var roundOrder = RoundId.CompareTo(other.RoundId);
        return roundOrder != 0 ? roundOrder : Id.CompareTo(other.Id);
    }
}
// Arcane-Edit-End
