using System.Collections.Generic;
using System.Linq;

namespace StellarIndustries.Core.Data
{
    /// <summary>Indexed, read-only view over all content definitions.</summary>
    public class GameDatabase
    {
        public readonly List<ResourceDef> Resources = new List<ResourceDef>();
        public readonly List<BuildingDef> Buildings = new List<BuildingDef>();
        public readonly List<RecipeDef> Recipes = new List<RecipeDef>();
        public readonly List<TechDef> Techs = new List<TechDef>();
        public readonly List<RocketDef> Rockets = new List<RocketDef>();
        public readonly List<LocationDef> Locations = new List<LocationDef>();
        public readonly List<MissionDef> Missions = new List<MissionDef>();
        public readonly List<AchievementDef> Achievements = new List<AchievementDef>();
        public StartDef Start = new StartDef();

        readonly Dictionary<string, ResourceDef> resources = new Dictionary<string, ResourceDef>();
        readonly Dictionary<string, BuildingDef> buildings = new Dictionary<string, BuildingDef>();
        readonly Dictionary<string, RecipeDef> recipes = new Dictionary<string, RecipeDef>();
        readonly Dictionary<string, TechDef> techs = new Dictionary<string, TechDef>();
        readonly Dictionary<string, RocketDef> rockets = new Dictionary<string, RocketDef>();
        readonly Dictionary<string, LocationDef> locations = new Dictionary<string, LocationDef>();
        readonly Dictionary<string, MissionDef> missions = new Dictionary<string, MissionDef>();
        readonly Dictionary<string, RocketDef> rocketByVehicle = new Dictionary<string, RocketDef>();

        public GameDatabase Add(ResourceDef d) { Resources.Add(d); resources[d.Id] = d; return this; }
        public GameDatabase Add(BuildingDef d) { Buildings.Add(d); buildings[d.Id] = d; return this; }
        public GameDatabase Add(RecipeDef d) { Recipes.Add(d); recipes[d.Id] = d; return this; }
        public GameDatabase Add(TechDef d) { Techs.Add(d); techs[d.Id] = d; return this; }
        public GameDatabase Add(RocketDef d) { Rockets.Add(d); rockets[d.Id] = d; if (!string.IsNullOrEmpty(d.VehicleResourceId)) rocketByVehicle[d.VehicleResourceId] = d; return this; }
        public GameDatabase Add(LocationDef d) { Locations.Add(d); locations[d.Id] = d; return this; }
        public GameDatabase Add(MissionDef d) { Missions.Add(d); missions[d.Id] = d; return this; }
        public GameDatabase Add(AchievementDef d) { Achievements.Add(d); return this; }

        public ResourceDef Resource(string id) => id != null && resources.TryGetValue(id, out var v) ? v : null;
        public BuildingDef Building(string id) => id != null && buildings.TryGetValue(id, out var v) ? v : null;
        public RecipeDef Recipe(string id) => id != null && recipes.TryGetValue(id, out var v) ? v : null;
        public TechDef Tech(string id) => id != null && techs.TryGetValue(id, out var v) ? v : null;
        public RocketDef Rocket(string id) => id != null && rockets.TryGetValue(id, out var v) ? v : null;
        public RocketDef RocketForVehicle(string resourceId) => resourceId != null && rocketByVehicle.TryGetValue(resourceId, out var v) ? v : null;
        public LocationDef Location(string id) => id != null && locations.TryGetValue(id, out var v) ? v : null;
        public MissionDef Mission(string id) => id != null && missions.TryGetValue(id, out var v) ? v : null;

        public string ResourceName(string id) => Resource(id)?.Name ?? id;

        /// <summary>Everything a technology unlocks, derived from RequiredTech fields.</summary>
        public IEnumerable<string> DescribeUnlocks(TechDef t)
        {
            foreach (var b in Buildings.Where(x => x.RequiredTech == t.Id)) yield return "Building: " + b.Name;
            foreach (var r in Recipes.Where(x => x.RequiredTech == t.Id)) yield return "Recipe: " + r.Name;
            foreach (var m in Missions.Where(x => x.RequiredTech == t.Id)) yield return "Mission: " + m.Name;
            foreach (var m in t.Modifiers) yield return "Bonus: " + m.Type + " +" + (m.Type == ModifierType.OfflineCapHours ? m.Value + "h" : (m.Value * 100).ToString("0") + "%");
            foreach (var f in t.UnlockFeatures) yield return "Feature: " + f.Replace('_', ' ');
        }

        /// <summary>Validates referential integrity and gameplay consistency. Returns a list of problems.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            void CheckRes(string ctx, string id) { if (Resource(id) == null) errors.Add(ctx + ": unknown resource '" + id + "'"); }
            void CheckTech(string ctx, string id) { if (!string.IsNullOrEmpty(id) && Tech(id) == null) errors.Add(ctx + ": unknown tech '" + id + "'"); }
            void CheckLoc(string ctx, string id) { if (!string.IsNullOrEmpty(id) && Location(id) == null) errors.Add(ctx + ": unknown location '" + id + "'"); }

            foreach (var g in Resources.GroupBy(r => r.Id).Where(g => g.Count() > 1)) errors.Add("Duplicate resource id " + g.Key);
            foreach (var b in Buildings)
            {
                foreach (var c in b.BuildCost) CheckRes("Building " + b.Id, c.ResourceId);
                foreach (var r in b.Recipes) if (Recipe(r) == null) errors.Add("Building " + b.Id + ": unknown recipe " + r);
                foreach (var l in b.AllowedLocations) CheckLoc("Building " + b.Id, l);
                CheckTech("Building " + b.Id, b.RequiredTech);
                if (b.Recipes.Count == 0 && b.PowerOutput <= 0 && b.StorageBonus <= 0) errors.Add("Building " + b.Id + " has no function");
            }
            foreach (var r in Recipes)
            {
                foreach (var a in r.Inputs) CheckRes("Recipe " + r.Id, a.ResourceId);
                foreach (var a in r.Outputs) CheckRes("Recipe " + r.Id, a.ResourceId);
                if (!string.IsNullOrEmpty(r.RequiredDeposit)) CheckRes("Recipe " + r.Id + " deposit", r.RequiredDeposit);
                CheckTech("Recipe " + r.Id, r.RequiredTech);
                if (r.Duration <= 0) errors.Add("Recipe " + r.Id + " has non-positive duration");
                if (!Buildings.Any(b => b.Recipes.Contains(r.Id))) errors.Add("Recipe " + r.Id + " is not used by any building");
            }
            foreach (var t in Techs)
            {
                foreach (var p in t.Prerequisites) CheckTech("Tech " + t.Id, p);
                foreach (var c in t.MaterialCost) CheckRes("Tech " + t.Id, c.ResourceId);
                if (!DescribeUnlocks(t).Any()) errors.Add("Tech " + t.Id + " has no gameplay consequence");
            }
            foreach (var r in Rockets)
            {
                CheckRes("Rocket " + r.Id, r.VehicleResourceId);
                foreach (var f in r.FuelCost) CheckRes("Rocket " + r.Id, f.ResourceId);
            }
            foreach (var l in Locations)
            {
                foreach (var d in l.Deposits) CheckRes("Location " + l.Id, d.ResourceId);
                CheckLoc("Location " + l.Id + " parent", l.ParentId);
            }
            foreach (var m in Missions)
            {
                CheckLoc("Mission " + m.Id, m.Origin);
                CheckLoc("Mission " + m.Id, m.Destination);
                CheckLoc("Mission " + m.Id + " unlock", m.UnlockLocation);
                CheckTech("Mission " + m.Id, m.RequiredTech);
                foreach (var a in m.ExtraCost) CheckRes("Mission " + m.Id, a.ResourceId);
                foreach (var a in m.RewardsAtDestination) CheckRes("Mission " + m.Id, a.ResourceId);
                foreach (var a in m.FirstTimeRewards) CheckRes("Mission " + m.Id, a.ResourceId);
                if (!Rockets.Any(r => r.RangeTier >= m.RequiredRangeTier && (!m.RequiresLanding || r.CanLand)))
                    errors.Add("Mission " + m.Id + " cannot be flown by any rocket");
            }
            // Every resource must be obtainable: produced by a recipe, given as reward or start resource.
            foreach (var res in Resources)
            {
                bool produced = Recipes.Any(r => r.Outputs.Any(o => o.ResourceId == res.Id))
                    || Missions.Any(m => m.RewardsAtDestination.Concat(m.FirstTimeRewards).Any(o => o.ResourceId == res.Id) || (res.Id == "science" && m.RewardScience > 0))
                    || Start.Resources.Any(s => s.ResourceId == res.Id);
                if (!produced) errors.Add("Resource " + res.Id + " can never be obtained");
            }
            CheckLoc("Start", Start.StartLocation);
            foreach (var b in Start.Buildings) if (Building(b.BuildingId) == null) errors.Add("Start building unknown: " + b.BuildingId);
            return errors;
        }
    }
}
