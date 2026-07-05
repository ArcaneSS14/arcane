using Robust.Shared.Player;

namespace Content.Shared._Arcane.Sponsor;

public interface ISharedSponsorManager
{
    bool HasSponsor(ICommonSession session, string? tier = null);
}
