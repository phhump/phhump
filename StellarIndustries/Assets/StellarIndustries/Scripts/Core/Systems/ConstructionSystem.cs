using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    /// <summary>Building construction, upgrades, configuration and manual (active-play) actions.</summary>
    public class ConstructionSystem
    {
        readonly GameDatabase db;
        readonly GameState state;
        readonly InventorySystem inv;
        readonly ModifierSystem mods;
        readonly GameEvents events;
        readonly Action onChanged;

        public ConstructionSystem(GameDatabase db, GameState state, InventorySystem inv, ModifierSystem mods, GameEvents events, Action onChanged)
        { this.db = db; this.state = state; this.inv = inv; this.mods = mods; this.events = events; this.onChanged = onChanged; }

        public bool TechDone(string techId) => string.IsNullOrEmpty(techId) || state.Research.Completed.Contains(techId);
        public bool IsUnlocked(BuildingDef def) => TechDone(def.RequiredTech);
        public bool IsUnlocked(RecipeDef r) => TechDone(r.RequiredTech);

        public bool HasDeposit(string locationId, string resourceId)
            => db.Location(locationId)?.Deposits.Any(d => d.ResourceId == resourceId) ?? false;

        public List<RecipeDef> ValidRecipes(BuildingDef def, string locationId)
        {
            var list = new List<RecipeDef>();
            foreach (var id in def.Recipes)
            {
                var r = db.Recipe(id);
                if (r == null || !IsUnlocked(r)) continue;
                if (!string.IsNullOrEmpty(r.RequiredDeposit) && !HasDeposit(locationId, r.RequiredDeposit)) continue;
                if (!AtmosphereOk(r, locationId)) continue;
                list.Add(r);
            }
            return list;
        }

        public bool AtmosphereOk(RecipeDef r, string locationId)
            => string.IsNullOrEmpty(r.RequiredAtmosphere) || (db.Location(locationId)?.Atmosphere ?? "").Contains(r.RequiredAtmosphere);

        public int CountAt(string defId, string locationId) => state.Buildings.Count(b => b.DefId == defId && b.LocationId == locationId);

        public List<ResourceAmount> BuildCost(BuildingDef def, string locationId)
        {
            double m = Math.Pow(def.CostScaling, CountAt(def.Id, locationId));
            return def.BuildCost.Select(c => new ResourceAmount(c.ResourceId, Math.Ceiling(c.Amount * m))).ToList();
        }

        /// <summary>Null when the building can be placed here (ignoring cost); otherwise the reason.</summary>
        public string PlacementProblem(BuildingDef def, string locationId)
        {
            var loc = db.Location(locationId);
            if (loc == null || !state.IsUnlocked(locationId)) return "Location not reached";
            if (!IsUnlocked(def)) return "Requires research: " + (db.Tech(def.RequiredTech)?.Name ?? def.RequiredTech);
            if (def.AllowedLocations.Count > 0 && !def.AllowedLocations.Contains(locationId)) return "Cannot operate on " + loc.Name;
            if (def.PowerSource == PowerSource.Wind && !loc.HasWind) return "No wind on " + loc.Name;
            if (def.PowerOutput <= 0 && def.Recipes.Count > 0 && ValidRecipes(def, locationId).Count == 0) return "No suitable deposit or recipe here";
            return null;
        }

        public bool CanBuild(BuildingDef def, string locationId, out string reason)
        {
            reason = PlacementProblem(def, locationId);
            if (reason != null) return false;
            var cost = BuildCost(def, locationId);
            if (!inv.Has(locationId, cost)) { reason = inv.MissingText(locationId, cost); return false; }
            return true;
        }

        public BuildingInstance Build(string defId, string locationId, string recipeId = null)
        {
            var def = db.Building(defId);
            if (def == null || !CanBuild(def, locationId, out _)) return null;
            inv.TryPay(locationId, BuildCost(def, locationId));
            var b = Place(def, locationId, recipeId);
            state.Stats.BuildingsBuilt++;
            events.Notify("Built " + def.Name + " on " + db.Location(locationId).Name, NotificationLevel.Success);
            return b;
        }

        /// <summary>Places without cost (start conditions, tests).</summary>
        public BuildingInstance Place(BuildingDef def, string locationId, string recipeId = null)
        {
            var valid = ValidRecipes(def, locationId);
            string rec = recipeId != null && def.Recipes.Contains(recipeId) ? recipeId : (valid.Count > 0 ? valid[0].Id : (def.Recipes.Count > 0 ? def.Recipes[0] : ""));
            var b = new BuildingInstance { Uid = state.TakeUid(), DefId = def.Id, LocationId = locationId, RecipeId = rec };
            state.Buildings.Add(b);
            Changed(b.Uid);
            return b;
        }

        public List<ResourceAmount> UpgradeCost(BuildingInstance b)
        {
            var def = db.Building(b.DefId);
            double m = def.UpgradeCostBase * Math.Pow(def.UpgradeCostGrowth, b.Level - 1);
            return def.BuildCost.Select(c => new ResourceAmount(c.ResourceId, Math.Ceiling(c.Amount * m))).ToList();
        }

        public bool CanUpgrade(BuildingInstance b, out string reason)
        {
            var def = db.Building(b.DefId);
            reason = null;
            if (def == null) { reason = "Unknown building"; return false; }
            if (b.Level >= def.MaxLevel) { reason = "Max level"; return false; }
            var cost = UpgradeCost(b);
            if (!inv.Has(b.LocationId, cost)) { reason = inv.MissingText(b.LocationId, cost); return false; }
            return true;
        }

        public bool Upgrade(int uid)
        {
            var b = Find(uid);
            if (b == null || !CanUpgrade(b, out _)) return false;
            inv.TryPay(b.LocationId, UpgradeCost(b));
            b.Level++;
            state.Stats.Upgrades++;
            Changed(uid);
            return true;
        }

        public bool Demolish(int uid)
        {
            var b = Find(uid);
            if (b == null) return false;
            var def = db.Building(b.DefId);
            state.Buildings.Remove(b);
            if (def != null) foreach (var c in def.BuildCost) inv.Add(b.LocationId, c.ResourceId, Math.Floor(c.Amount * 0.5), false);
            Changed(uid);
            return true;
        }

        public bool SetRecipe(int uid, string recipeId)
        {
            var b = Find(uid);
            var def = b != null ? db.Building(b.DefId) : null;
            if (def == null || !ValidRecipes(def, b.LocationId).Any(r => r.Id == recipeId)) return false;
            if (b.RecipeId != recipeId) { b.RecipeId = recipeId; b.Progress = 0; b.Status = BuildingStatus.Idle; }
            return true;
        }

        public void CycleRecipe(int uid, int dir)
        {
            var b = Find(uid);
            var def = b != null ? db.Building(b.DefId) : null;
            if (def == null) return;
            var valid = ValidRecipes(def, b.LocationId);
            if (valid.Count == 0) return;
            int idx = valid.FindIndex(r => r.Id == b.RecipeId);
            idx = ((idx + dir) % valid.Count + valid.Count) % valid.Count;
            SetRecipe(uid, valid[idx].Id);
        }

        public void SetEnabled(int uid, bool enabled)
        {
            var b = Find(uid);
            if (b == null) return;
            b.Enabled = enabled;
            if (enabled) b.Status = BuildingStatus.Idle;
        }

        public void SetPriority(int uid, int priority)
        {
            var b = Find(uid);
            if (b == null) return;
            b.Priority = Math.Max(0, Math.Min(2, priority));
            Changed(uid);
        }

        public BuildingInstance Find(int uid) => state.Buildings.Find(x => x.Uid == uid);

        void Changed(int uid)
        {
            onChanged?.Invoke();
            events.RaiseBuildingsChanged(uid);
        }

        // ---- Manual actions ----------------------------------------------------------
        public double ManualGatherAmount => db.Start.ManualGatherAmount * mods.Multiplier(ModifierType.ManualYield);

        public List<ResourceDef> GatherableResources(string locationId)
        {
            var loc = db.Location(locationId);
            var list = new List<ResourceDef>();
            if (loc == null) return list;
            foreach (var d in loc.Deposits)
            {
                var r = db.Resource(d.ResourceId);
                // Only resources the player can actually extract with currently unlocked recipes.
                if (r != null && db.Recipes.Any(x => x.RequiredDeposit == r.Id && IsUnlocked(x))) list.Add(r);
            }
            return list;
        }

        public double Gather(string locationId, string resourceId)
        {
            if (!state.IsUnlocked(locationId) || !GatherableResources(locationId).Any(r => r.Id == resourceId)) return 0;
            state.Stats.ManualActions++;
            return inv.Add(locationId, resourceId, ManualGatherAmount);
        }

        public bool ManualCraft(string locationId, string recipeId)
        {
            var r = db.Recipe(recipeId);
            if (r == null || !r.ManualCraftable || !IsUnlocked(r) || !state.IsUnlocked(locationId)) return false;
            foreach (var o in r.Outputs) if (inv.FreeSpace(locationId, o.ResourceId) < o.Amount) return false;
            if (!inv.TryPay(locationId, r.Inputs)) return false;
            foreach (var o in r.Outputs) inv.Add(locationId, o.ResourceId, o.Amount);
            state.Stats.ManualActions++;
            return true;
        }
    }
}
