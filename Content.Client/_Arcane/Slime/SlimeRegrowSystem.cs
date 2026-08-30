using Content.Shared._Arcane.Slime;

namespace Content.Client._Arcane.Slime;

/// <summary>
/// Client part of the slime limb regrow. All decisions and feedback are server-authoritative;
/// the client never predicts which limb regrows or the success popup, so it cannot show a
/// result the server would reject.
/// </summary>
public sealed partial class SlimeRegrowSystem : SharedSlimeRegrowSystem
{
}
