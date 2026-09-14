using Content.Shared.StatusEffectNew;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server.Toolshed.TypeParsers.StatusEffects;

public sealed class StatusEffectCompletionParser : CustomCompletionParser<EntProtoId>
{
    [Dependency] private readonly IEntitySystemManager _systems = default!; // Arcane
    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        var effects = _systems.GetEntitySystem<StatusEffectsSystem>().StatusEffectPrototypes; // Arcane: Fix Tests
        return CompletionResult.FromHintOptions(effects, GetArgHint(arg)); // Arcane-Edit
    }
}
