using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

//
// License-Identifier: MIT
//

public sealed partial class CCVars
{
    /// <summary>
    ///     Enable or disable NSFW content, like NSFW flavor.
    /// </summary>
    public static readonly CVarDef<bool> NsfwContentEnabled =
        CVarDef.Create("accessibility.nsfw_content_enabled", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Compiles the serialization delegates of entity components in the background after the client has loaded,
    /// so that the first entity of each kind doesn't cause a stutter in game.
    /// </summary>
    public static readonly CVarDef<bool> ComponentCopyWarmup =
        CVarDef.Create("content.component_copy_warmup", true, CVar.CLIENTONLY);
}
