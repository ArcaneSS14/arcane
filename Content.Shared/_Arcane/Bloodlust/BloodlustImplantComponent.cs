// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Dataset;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Bloodlust;

/// <summary>
///     Component of the bloodlust implant itself. Lives on the implant entity inside the host.
///     Holds only data and runtime links, all logic is in the server system.
/// </summary>
[RegisterComponent]
public sealed partial class BloodlustImplantComponent : Component
{
    /// <summary>Companion entity that only the host can see.</summary>
    [DataField]
    public EntProtoId CompanionPrototype = "BloodlustCompanion";

    /// <summary>Companion offset relative to the host.</summary>
    [DataField]
    public Vector2 CompanionOffset = new(0.75f, 0.2f);

    /// <summary>Chance that a bullet flying at the host is blocked by a spark.</summary>
    [DataField]
    public float BlockChance = 0.5f;

    /// <summary>Spark effect prototypes (one is chosen randomly).</summary>
    [DataField]
    public List<EntProtoId> SparkPrototypes = new()
    {
        "BloodlustSpark1",
        "BloodlustSpark2",
        "BloodlustSpark3",
    };

    [DataField]
    public SoundSpecifier BlockSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");

    /// <summary>Phrase on implanting.</summary>
    [DataField]
    public LocId GreetingPhrase = "bloodlust-greeting";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> CombatPhrases = "BloodlustCombatPhrases";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> KillPhrases = "BloodlustKillPhrases";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> IdlePhrases = "BloodlustIdlePhrases";

    /// <summary>Chance to speak when the host shoots or hits something.</summary>
    [DataField]
    public float CombatPhraseChance = 0.33f;

    /// <summary>Chance to speak when the host kills someone.</summary>
    [DataField]
    public float KillPhraseChance = 0.33f;

    /// <summary>Chance to speak on each idle check.</summary>
    [DataField]
    public float IdlePhraseChance = 0.33f;

    /// <summary>Minimum delay between any two phrases.</summary>
    [DataField]
    public TimeSpan PhraseCooldown = TimeSpan.FromSeconds(4);

    /// <summary>Idle check interval range in seconds.</summary>
    [DataField]
    public float IdleCheckMin = 25f;

    [DataField]
    public float IdleCheckMax = 55f;

    // Runtime state, intentionally not serialized.
    public EntityUid? Host;
    public EntityUid? Companion;
    public TimeSpan NextIdleCheck;
    public TimeSpan NextPhrase;
}

/// <summary>
///     Marker on the host of the implant. Used to catch events targeted at the host.
/// </summary>
[RegisterComponent]
public sealed partial class BloodlustHostComponent : Component
{
    public EntityUid Implant;
}

/// <summary>
///     Marker on the companion entity.
/// </summary>
[RegisterComponent]
public sealed partial class BloodlustCompanionComponent : Component;
