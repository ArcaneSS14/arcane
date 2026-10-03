using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;

namespace Content.Shared._Arcane.PlantAnalyzer;

[NetSerializable, Serializable]
public readonly struct PlantAnalyzerReagentInfo
{
    public readonly string ReagentName;
    public readonly float Quantity;

    public PlantAnalyzerReagentInfo(string reagentName, float quantity)
    {
        ReagentName = reagentName;
        Quantity = quantity;
    }
}

public enum PlantSpecialGene : byte
{
    None,
    Kudzu,
    Lethal
}


[NetSerializable, Serializable]
public sealed class PlantAnalyzerUserInterfaceState : BoundUserInterfaceState
{
    public readonly NetEntity? Target;
    public readonly string TargetName;
    public readonly bool HasPlant;
    public readonly string PlantName;
    public readonly int Potency;
    public readonly int Yield;
    public readonly int Age;
    public readonly int MaxAge;
    public readonly bool Harvestable;
    public readonly bool Dead;
    public readonly float Health;
    public readonly float MaxHealth;
    public readonly int WeedLevel;
    public readonly int PestLevel;
    public readonly int Toxins;
    public readonly int MutationLevel;
    public readonly bool IsScanFinished;
    public int GrowthRate { get; }
    public float MinTemp { get; }
    public float MaxTemp { get; }
    public float MinPressure { get; }
    public float MaxPressure { get; }
    public List<string> ExudeGases { get; }

    public readonly List<PlantAnalyzerReagentInfo> SoilReagents;
    public readonly List<PlantAnalyzerReagentInfo> ProduceReagents;
    public PlantSpecialGene SpecialGene { get; }


    public PlantAnalyzerUserInterfaceState(
        NetEntity? target,
        string targetName,
        bool hasPlant,
        string plantName,
        int potency,
        int yield,
        int age,
        int maxAge,
        bool harvestable,
        bool dead,
        float health,
        float maxHealth,
        int weedLevel,
        int pestLevel,
        int toxins,
        int mutationLevel,
        bool isScanFinished,
        int growthRate,
        float minTemp,
        float maxTemp,
        float minPressure,
        float maxPressure,
        List<string> exudeGases,
        List<PlantAnalyzerReagentInfo> soilReagents,
        List<PlantAnalyzerReagentInfo> produceReagents,
        PlantSpecialGene specialGene)

    {
        Target = target;
        TargetName = targetName;
        HasPlant = hasPlant;
        PlantName = plantName;
        Potency = potency;
        Yield = yield;
        Age = age;
        MaxAge = maxAge;
        Harvestable = harvestable;
        Dead = dead;
        Health = health;
        MaxHealth = maxHealth;
        WeedLevel = weedLevel;
        PestLevel = pestLevel;
        Toxins = toxins;
        MutationLevel = mutationLevel;
        IsScanFinished = isScanFinished;
        GrowthRate = growthRate;
        MinTemp = minTemp;
        MaxTemp = maxTemp;
        MinPressure = minPressure;
        MaxPressure = maxPressure;
        ExudeGases = exudeGases;
        SoilReagents = soilReagents;
        ProduceReagents = produceReagents;
        SpecialGene = specialGene;
    }
}
