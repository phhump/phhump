using System;
using System.Collections.Generic;

// All content definitions are plain [Serializable] classes with public fields.
// - The core simulation uses them directly (engine independent, unit testable).
// - Unity ScriptableObjects wrap them 1:1 (see Scripts/Unity/Content), so designers
//   can author new content as assets without touching code.
// Cross references are string ids. Unlocks are declared on the unlocked object
// (RequiredTech), so there is exactly one source of truth for every unlock.
namespace StellarIndustries.Core.Data
{
    [Serializable]
    public class ResourceAmount
    {
        public string ResourceId;
        public double Amount;
        public ResourceAmount() { }
        public ResourceAmount(string id, double amount) { ResourceId = id; Amount = amount; }
        public override string ToString() => Amount + "x " + ResourceId;
    }

    public enum ResourceCategory { Raw, Material, Component, Fuel, Vehicle, Science }

    [Serializable]
    public class ResourceDef
    {
        public string Id;
        public string Name;
        public string Description;
        public ResourceCategory Category;
        public string ColorHex = "#FFFFFF";
        /// <summary>Global resources (e.g. science) are shared across all locations and uncapped.</summary>
        public bool IsGlobal;
        /// <summary>Base storage per location. Negative = use the category default.</summary>
        public double BaseStorage = -1;
    }

    public enum BuildingCategory { Extraction, Processing, Manufacturing, Assembly, Power, Research, Storage }
    public enum PowerSource { None, Fuel, Solar, Wind, Constant }

    [Serializable]
    public class BuildingDef
    {
        public string Id;
        public string Name;
        public string Description;
        public BuildingCategory Category;
        public List<ResourceAmount> BuildCost = new List<ResourceAmount>();
        /// <summary>Cost multiplier per already existing building of this type at the same location.</summary>
        public double CostScaling = 1.15;
        /// <summary>Upgrade cost to reach level L+1 = BuildCost * UpgradeCostBase * UpgradeCostGrowth^(L-1).</summary>
        public double UpgradeCostBase = 1.5;
        public double UpgradeCostGrowth = 1.7;
        public int MaxLevel = 10;
        /// <summary>Speed bonus per level above 1 (0.5 = +50% per level).</summary>
        public double SpeedPerLevel = 0.5;
        /// <summary>Power consumption at level 1 (MW). Scales with level speed.</summary>
        public double PowerUse;
        /// <summary>Power generation at level 1 (MW).</summary>
        public double PowerOutput;
        public PowerSource PowerSource = PowerSource.None;
        /// <summary>Recipes this building can run. Power plants use their recipe as fuel consumption.</summary>
        public List<string> Recipes = new List<string>();
        /// <summary>Empty = may be built at any unlocked location.</summary>
        public List<string> AllowedLocations = new List<string>();
        /// <summary>Storage capacity bonus per level (0.5 = +50% of base storage).</summary>
        public double StorageBonus;
        public string RequiredTech = "";
        public string ColorHex = "#8899AA";
        /// <summary>Visual hint for the placeholder renderer: cube, cylinder, dome, tower, panel, pad.</summary>
        public string Shape = "cube";
    }

    [Serializable]
    public class RecipeDef
    {
        public string Id;
        public string Name;
        public List<ResourceAmount> Inputs = new List<ResourceAmount>();
        public List<ResourceAmount> Outputs = new List<ResourceAmount>();
        /// <summary>Seconds per cycle at level 1, 100% speed.</summary>
        public double Duration = 1;
        public string RequiredTech = "";
        /// <summary>If set, the location must have a deposit of this resource; speed scales with richness.</summary>
        public string RequiredDeposit = "";
        /// <summary>Can be executed instantly by the player (active play bootstrap).</summary>
        public bool ManualCraftable;
    }

    public enum ModifierType
    {
        ProductionSpeed, ExtractionSpeed, ResearchSpeed, StorageCapacity, EnergyOutput,
        OfflineEfficiency, OfflineCapHours, MissionSpeed, RocketReliability, ManualYield
    }

    [Serializable]
    public class ModifierDef
    {
        public ModifierType Type;
        public double Value;
        public ModifierDef() { }
        public ModifierDef(ModifierType type, double value) { Type = type; Value = value; }
    }

    public enum TechCategory
    {
        Rocketry, Propulsion, Mining, Metallurgy, Chemistry, Electronics, Robotics, ArtificialIntelligence,
        Automation, Energy, Nuclear, Fusion, PlanetaryEngineering, SpaceConstruction, Terraforming,
        InterstellarTravel, Megastructures, Logistics
    }

    [Serializable]
    public class TechDef
    {
        public string Id;
        public string Name;
        public string Description;
        public TechCategory Category;
        /// <summary>Column in the technology tree.</summary>
        public int Tier;
        public double ScienceCost;
        public List<ResourceAmount> MaterialCost = new List<ResourceAmount>();
        public List<string> Prerequisites = new List<string>();
        public List<ModifierDef> Modifiers = new List<ModifierDef>();
        /// <summary>Feature flags (see <see cref="Features"/>).</summary>
        public List<string> UnlockFeatures = new List<string>();
    }

    public enum RocketCategory
    {
        BasicRocket, CargoRocket, CrewSpacecraft, MiningSpacecraft, Probe, InterplanetaryTransport,
        HeavyLift, InterstellarShip, ConstructionVessel
    }

    [Serializable]
    public class RocketDef
    {
        public string Id;
        public string Name;
        public string Description;
        public RocketCategory Category;
        /// <summary>The assembled vehicle is an inventory resource (produced by an assembly recipe).</summary>
        public string VehicleResourceId;
        public double CargoCapacity;
        /// <summary>0 suborbital, 1 orbit, 2 lunar, 3 interplanetary, 4 outer system, 5 interstellar.</summary>
        public int RangeTier;
        public bool CanLand;
        public bool Reusable;
        public double Reliability = 0.9;
        public double SpeedMultiplier = 1;
        public List<ResourceAmount> FuelCost = new List<ResourceAmount>();
    }

    [Serializable]
    public class DepositDef
    {
        public string ResourceId;
        public double Richness = 1;
        public DepositDef() { }
        public DepositDef(string id, double richness) { ResourceId = id; Richness = richness; }
    }

    public enum BodyType { Star, Planet, Moon, AsteroidBelt, GasGiant, Station }

    [Serializable]
    public class LocationDef
    {
        public string Id;
        public string Name;
        public string Description;
        public BodyType BodyType;
        public string ParentId = "";
        public double Gravity = 1;
        public string Atmosphere = "None";
        public double TemperatureC;
        public double SolarMultiplier = 1;
        public bool HasWind;
        public List<DepositDef> Deposits = new List<DepositDef>();
        public List<string> Hazards = new List<string>();
        public bool StartUnlocked;
        public string ColorHex = "#FFFFFF";
        /// <summary>Visual orbit radius around the parent (map units).</summary>
        public double OrbitRadius;
        public double VisualRadius = 1;
        /// <summary>Visual orbit period in seconds.</summary>
        public double OrbitPeriod = 120;
    }

    public enum MissionType { Launch, Landing, Cargo, Survey }

    [Serializable]
    public class MissionDef
    {
        public string Id;
        public string Name;
        public string Description;
        public MissionType Type;
        public string Origin;
        public string Destination;
        public int RequiredRangeTier;
        public bool RequiresLanding;
        public double Duration = 60;
        public double BaseSuccess = 1;
        public double FuelMultiplier = 1;
        public List<ResourceAmount> ExtraCost = new List<ResourceAmount>();
        public string RequiredTech = "";
        /// <summary>All of these locations must be unlocked to see the mission.</summary>
        public List<string> RequiredLocations = new List<string>();
        public bool Repeatable = true;
        public bool AllowsCargo;
        public double RewardScience;
        public List<ResourceAmount> RewardsAtDestination = new List<ResourceAmount>();
        public List<ResourceAmount> FirstTimeRewards = new List<ResourceAmount>();
        public string UnlockLocation = "";
        public List<string> UnlockFeatures = new List<string>();
    }

    public enum AchievementCondition { ResourceProduced, MissionCompleted, LocationUnlocked, TechCount, BuildingCount, RocketsLaunched, TechCompleted }

    [Serializable]
    public class AchievementDef
    {
        public string Id;
        public string Name;
        public string Description;
        public AchievementCondition Condition;
        public string TargetId = "";
        public double Threshold = 1;
        /// <summary>Shown in the guided objective list on the overview screen.</summary>
        public bool IsObjective;
    }

    [Serializable]
    public class StartingBuilding
    {
        public string BuildingId;
        public string RecipeId;
        public string LocationId;
    }

    [Serializable]
    public class StartDef
    {
        public string StartLocation = "earth";
        public List<ResourceAmount> Resources = new List<ResourceAmount>();
        public List<StartingBuilding> Buildings = new List<StartingBuilding>();
        public double BaseOfflineCapHours = 8;
        public double BaseOfflineEfficiency = 0.5;
        public double BaseResearchRate = 4;
        public double ManualGatherAmount = 2;
    }

    /// <summary>Well-known feature flags that change gameplay or UI.</summary>
    public static class Features
    {
        public const string Spacecraft = "spacecraft";
        public const string Missions = "missions";
        public const string LaunchAutomation = "launch_automation";
        public const string AutoResearch = "auto_research";
        public const string Map = "map";
        public const string Logistics = "logistics";
    }
}
