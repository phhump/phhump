using System;
using System.Collections.Generic;

// Mutable runtime state. Contains only ids and numbers, never definition objects, so it
// serializes cleanly and survives content changes (unknown ids are sanitized on load).
// Fields marked [NonSerialized] are runtime caches recomputed every tick.
namespace StellarIndustries.Core.State
{
    public enum BuildingStatus { Working, Idle, MissingInput, OutputFull, NoPower, LowPower, Paused, NoDeposit, NoFuel }

    [Serializable]
    public class BuildingInstance
    {
        public int Uid;
        public string DefId;
        public string LocationId;
        public int Level = 1;
        public string RecipeId = "";
        public bool Enabled = true;
        /// <summary>0 = low, 1 = normal, 2 = high. High priority buildings get inputs first.</summary>
        public int Priority = 1;
        public double Progress;
        [NonSerialized] public BuildingStatus Status = BuildingStatus.Idle;
        /// <summary>Current effective speed multiplier (for UI).</summary>
        [NonSerialized] public double EffectiveSpeed;
    }

    [Serializable]
    public class LocationState
    {
        public string Id;
        public Dictionary<string, double> Inventory = new Dictionary<string, double>();
        [NonSerialized] public double EnergySupply;
        [NonSerialized] public double EnergyDemand;
        [NonSerialized] public double EnergySatisfaction = 1;
        [NonSerialized] public Dictionary<string, double> RateAccumulator = new Dictionary<string, double>();
        [NonSerialized] public Dictionary<string, double> Rates = new Dictionary<string, double>();
    }

    [Serializable]
    public class ResearchState
    {
        public List<string> Completed = new List<string>();
        public string ActiveTech = "";
        public double ActiveProgress;
        public List<string> Queue = new List<string>();
        public bool AutoResearch = true;
    }

    [Serializable]
    public class MissionInstance
    {
        public int Uid;
        public string MissionId;
        public string RocketId;
        public double Duration;
        public double Elapsed;
        public string CargoResource = "";
        public double CargoAmount;
        public int RouteUid;
        public double SuccessChance;
    }

    [Serializable]
    public class TransportRoute
    {
        public int Uid;
        public string MissionId;
        public string RocketId;
        public string CargoResource = "";
        public double CargoAmount;
        public bool Enabled = true;
        public int Launches;
        [NonSerialized] public string LastStatus = "";
    }

    [Serializable]
    public class Statistics
    {
        public Dictionary<string, double> Produced = new Dictionary<string, double>();
        public int RocketsLaunched;
        public int MissionsSucceeded;
        public int MissionsFailed;
        public int BuildingsBuilt;
        public int Upgrades;
        public double OfflineSeconds;
        public int ManualActions;
    }

    [Serializable]
    public class GameSettings
    {
        public float AutosaveSeconds = 30;
        public float UiScale = 1;
        public bool ShowHints = true;
        public bool DeveloperTools = false;
    }

    [Serializable]
    public class GameState
    {
        public int Version;
        public string SaveName = "Stellar Industries";
        public long CreatedUtcTicks;
        public long LastSavedUtcTicks;
        public double PlayTime;
        public uint RngState = 12345;
        public int NextUid = 1;
        public string CurrentLocation = "earth";
        public Dictionary<string, LocationState> Locations = new Dictionary<string, LocationState>();
        public Dictionary<string, double> GlobalInventory = new Dictionary<string, double>();
        public List<BuildingInstance> Buildings = new List<BuildingInstance>();
        public ResearchState Research = new ResearchState();
        public List<MissionInstance> ActiveMissions = new List<MissionInstance>();
        public List<TransportRoute> Routes = new List<TransportRoute>();
        public List<string> UnlockedLocations = new List<string>();
        public Dictionary<string, int> MissionCompletions = new Dictionary<string, int>();
        public List<string> Features = new List<string>();
        public List<string> Achievements = new List<string>();
        public Statistics Stats = new Statistics();
        public GameSettings Settings = new GameSettings();

        public int TakeUid() => NextUid++;

        public LocationState Location(string id)
        {
            if (id == null) return null;
            Locations.TryGetValue(id, out var l);
            return l;
        }

        public bool HasFeature(string f) => Features.Contains(f);
        public bool IsUnlocked(string locationId) => UnlockedLocations.Contains(locationId);
        public int Completions(string missionId) => MissionCompletions.TryGetValue(missionId, out var c) ? c : 0;
    }
}
