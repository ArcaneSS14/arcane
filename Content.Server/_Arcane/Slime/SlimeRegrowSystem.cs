using Content.Shared._Arcane.Slime;

namespace Content.Server._Arcane.Slime;

/// <summary>
/// Server part of the slime limb regrow. All gameplay logic lives in the shared system;
/// this concrete type exists so it registers on the server.
/// </summary>
public sealed partial class SlimeRegrowSystem : SharedSlimeRegrowSystem
{
}
