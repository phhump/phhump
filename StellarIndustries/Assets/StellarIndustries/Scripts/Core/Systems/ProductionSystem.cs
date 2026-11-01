using System;
using System.Collections.Generic;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    /// <summary>
    /// Runs energy balance and all recipe-based production. Data-driven: every building
    /// is handled by the same code path, configured by its BuildingDef/RecipeDef.
    /// Production is batched (whole cycles per tick), so large time steps (offline
    /// progression) stay cheap: O(buildings) per step, independent of dt.
    /// </summary>
    public class ProductionSystem
    {
        readonly GameDatabase db;
        readonly GameState state;
        readonly ModifierSystem mods;
        readonly InventorySystem inv;
        readonly Dictionary<string, List<BuildingInstance>> byLocation = new Dictionary<string, List<BuildingInstance>>();
        bool dirty = true;

        public ProductionSystem(GameDatabase db, GameState state, ModifierSystem mods, InventorySystem inv)
        { this.db = db; this.state = state; this.mods = mods; this.inv = inv; }

        public void MarkDirty() => dirty = true;

        public static double LevelSpeed(BuildingDef def, int level) => 1 + def.SpeedPerLevel * Math.Max(0, level - 1);

        public List<BuildingInstance> BuildingsAt(string locationId)
        {
            Rebuild();
            return byLocation.TryGetValue(locationId, out var l) ? l : new List<BuildingInstance>();
        }

        void Rebuild()
        {
            if (!dirty) return;
            byLocation.Clear();
            foreach (var b in state.Buildings)
            {
                if (!byLocation.TryGetValue(b.LocationId, out var list)) byLocation[b.LocationId] = list = new List<BuildingInstance>();
                list.Add(b);
            }
            foreach (var list in byLocation.Values)
                list.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : a.Uid.CompareTo(b.Uid));
            dirty = false;
        }

        public void Tick(double dt)
        {
            if (dt <= 0) return;
            Rebuild();
            foreach (var kv in byLocation)
            {
                var loc = state.Location(kv.Key);
                var locDef = db.Location(kv.Key);
                if (loc == null || locDef == null) continue;
                TickLocation(loc, locDef, kv.Value, dt);
            }
        }

        public static double SourceFactor(BuildingDef def, LocationDef loc)
        {
            switch (def.PowerSource)
            {
                case PowerSource.Solar: return SpacePhysics.SolarFactor(loc);
                case PowerSource.Wind: return loc.HasWind ? 1 : 0;
                default: return 1;
            }
        }

        void TickLocation(LocationState loc, LocationDef locDef, List<BuildingInstance> list, double dt)
        {
            // ---- 1. Energy balance --------------------------------------------------
            double supply = 0, demand = 0;
            double energyMult = mods.Multiplier(ModifierType.EnergyOutput);
            foreach (var b in list)
            {
                var def = db.Building(b.DefId);
                if (def == null) continue;
                if (!b.Enabled) { b.Status = BuildingStatus.Paused; b.EffectiveSpeed = 0; continue; }
                double lvl = LevelSpeed(def, b.Level);
                if (def.PowerOutput > 0)
                {
                    double factor = SourceFactor(def, locDef);
                    if (def.PowerSource == PowerSource.Fuel) factor = BurnFuel(b, def, lvl, dt);
                    supply += def.PowerOutput * lvl * energyMult * factor;
                    b.EffectiveSpeed = factor;
                    b.Status = factor > 0 ? BuildingStatus.Working : BuildingStatus.NoFuel;
                }
                else if (def.PowerUse > 0 && b.Status != BuildingStatus.MissingInput && b.Status != BuildingStatus.OutputFull
                         && b.Status != BuildingStatus.NoDeposit)
                {
                    demand += def.PowerUse * lvl;
                }
            }
            loc.EnergySupply = supply;
            loc.EnergyDemand = demand;
            double satisfaction = demand <= 0 ? 1 : Math.Min(1, supply / demand);
            loc.EnergySatisfaction = satisfaction;

            // ---- 2. Production in priority order ------------------------------------
            double prodMult = mods.Multiplier(ModifierType.ProductionSpeed);
            double extrMult = mods.Multiplier(ModifierType.ExtractionSpeed);
            double resMult = mods.Multiplier(ModifierType.ResearchSpeed);
            foreach (var b in list)
            {
                if (!b.Enabled) continue;
                var def = db.Building(b.DefId);
                if (def == null || def.PowerOutput > 0) continue;
                var recipe = db.Recipe(b.RecipeId);
                if (recipe == null) { b.Status = BuildingStatus.Idle; b.EffectiveSpeed = 0; continue; }

                double richness = 1, mult = prodMult;
                if (!string.IsNullOrEmpty(recipe.RequiredDeposit))
                {
                    var dep = locDef.Deposits.Find(d => d.ResourceId == recipe.RequiredDeposit);
                    if (dep == null) { b.Status = BuildingStatus.NoDeposit; b.EffectiveSpeed = 0; continue; }
                    richness = dep.Richness;
                    mult = extrMult;
                }
                if (!string.IsNullOrEmpty(recipe.RequiredAtmosphere) && !(locDef.Atmosphere ?? "").Contains(recipe.RequiredAtmosphere))
                { b.Status = BuildingStatus.NoDeposit; b.EffectiveSpeed = 0; continue; }
                if (def.Category == BuildingCategory.Research) mult = prodMult * resMult;

                double power = def.PowerUse > 0 ? satisfaction : 1;
                if (power <= 0) { b.Status = BuildingStatus.NoPower; b.EffectiveSpeed = 0; continue; }
                double speed = LevelSpeed(def, b.Level) * mult * richness * power;
                b.EffectiveSpeed = speed;
                b.Progress += dt * speed / recipe.Duration;

                if (b.Progress >= 1)
                {
                    double cycles = Math.Floor(b.Progress);
                    double maxIn = double.MaxValue, maxOut = double.MaxValue;
                    foreach (var i in recipe.Inputs) maxIn = Math.Min(maxIn, Math.Floor(inv.Get(b.LocationId, i.ResourceId) / i.Amount + 1e-9));
                    foreach (var o in recipe.Outputs) maxOut = Math.Min(maxOut, Math.Floor(inv.FreeSpace(b.LocationId, o.ResourceId) / o.Amount + 1e-9));
                    double n = Math.Min(cycles, Math.Min(maxIn, maxOut));
                    if (n > 0)
                    {
                        foreach (var i in recipe.Inputs) inv.Remove(b.LocationId, i.ResourceId, i.Amount * n);
                        foreach (var o in recipe.Outputs) inv.Add(b.LocationId, o.ResourceId, o.Amount * n);
                        b.Progress -= n;
                    }
                    if (n < cycles)
                    {
                        b.Status = maxIn < cycles ? BuildingStatus.MissingInput : BuildingStatus.OutputFull;
                        b.Progress = Math.Min(b.Progress, 1);
                        continue;
                    }
                }
                b.Status = power < 0.999 ? BuildingStatus.LowPower : BuildingStatus.Working;
            }
        }

        /// <summary>Consumes fuel proportionally to dt. Returns the fraction of the time step the plant was fuelled.</summary>
        double BurnFuel(BuildingInstance b, BuildingDef def, double lvl, double dt)
        {
            var recipe = db.Recipe(b.RecipeId);
            if (recipe == null && def.Recipes.Count > 0) { recipe = db.Recipe(def.Recipes[0]); b.RecipeId = recipe?.Id ?? ""; }
            if (recipe == null || recipe.Inputs.Count == 0) return 1;
            double fraction = 1;
            foreach (var i in recipe.Inputs)
            {
                double need = i.Amount * lvl * dt / recipe.Duration;
                if (need > 0) fraction = Math.Min(fraction, inv.Get(b.LocationId, i.ResourceId) / need);
            }
            fraction = Math.Max(0, Math.Min(1, fraction));
            if (fraction > 0)
                foreach (var i in recipe.Inputs) inv.Remove(b.LocationId, i.ResourceId, i.Amount * lvl * dt / recipe.Duration * fraction);
            return fraction;
        }

        /// <summary>Theoretical output per second of a building at its current speed (for UI/statistics).</summary>
        public double OutputPerSecond(BuildingInstance b, string resourceId)
        {
            var r = db.Recipe(b.RecipeId);
            if (r == null || b.Status != BuildingStatus.Working && b.Status != BuildingStatus.LowPower) return 0;
            double sum = 0;
            foreach (var o in r.Outputs) if (o.ResourceId == resourceId) sum += o.Amount * b.EffectiveSpeed / r.Duration;
            return sum;
        }
    }
}
