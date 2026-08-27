using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.DiscordRoles;

public sealed class DiscordRolesStatusMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public HashSet<DiscordRole> Roles = [];

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        Roles = new HashSet<DiscordRole>();

        for (var i = 0; i < count; i++)
        {
            var role = (DiscordRole) buffer.ReadByte();
            if (Enum.IsDefined(role))
                Roles.Add(role);
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Roles.Count);
        foreach (var role in Roles)
            buffer.Write((byte) role);
    }
}
