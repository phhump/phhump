using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Util;

namespace StellarIndustries.Core.Systems
{
    /// <summary>
    /// Launches and resolves missions. Rockets are inventory items (assembled by recipes);
    /// a launch consumes the vehicle, fuel, extra costs and cargo at the origin. Missions run
    /// in the background in real time (also during offline progression).
    /// </summary>
    public class MissionSystem
    {
        readonly GameDatabase db;
        readonly GameState state;
        readonly InventorySystem inv;
        readonly ModifierSystem mods;
        readonly GameEvents events;

        public MissionSystem(GameDatabase db, GameState state, InventorySystem inv, ModifierSystem mods, GameEvents events)
        { this.db = db; this.state = state; this.inv = inv; this.mods = mods; this.events = events; }

        public bool IsAvailable(MissionDef m)
        {
            if (!string.IsNullOrEmpty(m.RequiredTech) && !state.Research.Completed.Contains(m.RequiredTech)) return false;
            if (!state.IsUnlocked(m.Origin)) return false;
            if (m.RequiredLocations.Any(l => !state.IsUnlocked(l))) return false;
            if (!m.Repeatable && state.Completions(m.Id) > 0) return false;
            return true;
        }

        public IEnumerable<MissionDef> AvailableMissions() => db.Missions.Where(IsAvailable);

        public bool RocketSuitable(MissionDef m, RocketDef r)
            => r.RangeTier >= m.RequiredRangeTier && (!m.RequiresLanding || r.CanLand) && (m.Type != MissionType.Cargo || r.CargoCapacity > 0);

        public IEnumerable<RocketDef> SuitableRockets(MissionDef m) => db.Rockets.Where(r => RocketSuitable(m, r));

        public List<ResourceAmount> FuelCost(MissionDef m, RocketDef r)
            => r.FuelCost.Select(f => new ResourceAmount(f.ResourceId, Math.Ceiling(f.Amount * m.FuelMultiplier))).ToList();

        public double SuccessChance(MissionDef m, RocketDef r)
            => Math.Min(0.995, m.BaseSuccess * Math.Min(1.0, r.Reliability + mods.Sum(ModifierType.RocketReliability)));

        public double Duration(MissionDef m, RocketDef r) => m.Duration / (mods.Multiplier(ModifierType.MissionSpeed) * Math.Max(0.01, r.SpeedMultiplier));

        public List<ResourceAmount> TotalLaunchCost(MissionDef m, RocketDef r, string cargoRes, double cargoAmt)
        {
            var map = new Dictionary<string, double> { [r.VehicleResourceId] = 1 };
            void AddC(string id, double a) { if (string.IsNullOrEmpty(id) || a <= 0) return; map.TryGetValue(id, out var c); map[id] = c + a; }
            foreach (var f in FuelCost(m, r)) AddC(f.ResourceId, f.Amount);
            foreach (var e in m.ExtraCost) AddC(e.ResourceId, e.Amount);
            AddC(cargoRes, cargoAmt);
            return map.Select(kv => new ResourceAmount(kv.Key, kv.Value)).ToList();
        }

        /// <summary>Null when launchable, otherwise a human readable reason.</summary>
        public string LaunchProblem(string missionId, string rocketId, string cargoRes, double cargoAmt)
        {
            var m = db.Mission(missionId); var r = db.Rocket(rocketId);
            if (m == null || r == null) return "Unknown mission or rocket";
            if (!IsAvailable(m)) return "Mission not available";
            if (!RocketSuitable(m, r)) return r.Name + " cannot fly this mission";
            if (cargoAmt > 0)
            {
                if (!m.AllowsCargo) return "Mission carries no cargo";
                var cr = db.Resource(cargoRes);
                if (cr == null || cr.IsGlobal) return "Invalid cargo";
                if (cargoAmt > r.CargoCapacity + 1e-9) return "Cargo exceeds capacity (" + r.CargoCapacity + ")";
            }
            var cost = TotalLaunchCost(m, r, cargoRes, cargoAmt);
            if (!inv.Has(m.Origin, cost)) return inv.MissingText(m.Origin, cost);
            return null;
        }

        public MissionInstance Launch(string missionId, string rocketId, string cargoRes = "", double cargoAmt = 0, int routeUid = 0)
        {
            if (LaunchProblem(missionId, rocketId, cargoRes, cargoAmt) != null) return null;
            var m = db.Mission(missionId); var r = db.Rocket(rocketId);
            inv.TryPay(m.Origin, TotalLaunchCost(m, r, cargoRes, cargoAmt));
            var mi = new MissionInstance
            {
                Uid = state.TakeUid(), MissionId = m.Id, RocketId = r.Id, Duration = Duration(m, r),
                CargoResource = cargoAmt > 0 ? cargoRes : "", CargoAmount = Math.Max(0, cargoAmt), RouteUid = routeUid,
                SuccessChance = SuccessChance(m, r),
            };
            state.ActiveMissions.Add(mi);
            state.Stats.RocketsLaunched++;
            events.Notify("Liftoff! " + r.Name + " launched: " + m.Name, NotificationLevel.Info);
            events.RaiseRocketLaunched(m.Id, r.Id, m.Origin);
            return mi;
        }

        public void Tick(double dt)
        {
            if (state.ActiveMissions.Count == 0) return;
            for (int i = state.ActiveMissions.Count - 1; i >= 0; i--)
            {
                var mi = state.ActiveMissions[i];
                mi.Elapsed += dt;
                if (mi.Elapsed >= mi.Duration)
                {
                    state.ActiveMissions.RemoveAt(i);
                    Resolve(mi);
                }
            }
        }

        void Resolve(MissionInstance mi)
        {
            var m = db.Mission(mi.MissionId); var r = db.Rocket(mi.RocketId);
            if (m == null || r == null) return;
            bool success = Rng.NextDouble(ref state.RngState) < mi.SuccessChance;
            if (success)
            {
                bool first = state.Completions(m.Id) == 0;
                state.MissionCompletions[m.Id] = state.Completions(m.Id) + 1;
                state.Stats.MissionsSucceeded++;
                if (first && !string.IsNullOrEmpty(m.UnlockLocation)) UnlockLocation(m.UnlockLocation);
                if (mi.CargoAmount > 0)
                {
                    double added = inv.Add(m.Destination, mi.CargoResource, mi.CargoAmount, false);
                    if (added + 1e-6 < mi.CargoAmount)
                        events.Notify("Storage full at " + db.Location(m.Destination)?.Name + ": " + NumberFormat.Short(mi.CargoAmount - added) + " " + db.ResourceName(mi.CargoResource) + " lost", NotificationLevel.Warning);
                }
                if (m.RewardScience > 0) inv.Add(m.Destination, ResearchSystem.ScienceId, m.RewardScience);
                foreach (var rw in m.RewardsAtDestination) inv.Add(m.Destination, rw.ResourceId, rw.Amount);
                if (first) foreach (var rw in m.FirstTimeRewards) inv.Add(m.Destination, rw.ResourceId, rw.Amount);
                foreach (var f in m.UnlockFeatures) if (!state.Features.Contains(f)) state.Features.Add(f);
                if (r.Reusable) inv.Add(m.Origin, r.VehicleResourceId, 1, false);
                events.Notify("Mission success: " + m.Name + (first ? " (first completion!)" : ""), NotificationLevel.Success);
            }
            else
            {
                state.Stats.MissionsFailed++;
                if (m.RewardScience > 0) inv.Add(m.Origin, ResearchSystem.ScienceId, Math.Floor(m.RewardScience * 0.25));
                events.Notify("Mission failed: " + m.Name + ". " + r.Name + " and cargo were lost.", NotificationLevel.Error);
            }
            events.RaiseMissionCompleted(m.Id, success);
        }

        public void UnlockLocation(string id)
        {
            var def = db.Location(id);
            if (def == null || state.IsUnlocked(id)) return;
            state.UnlockedLocations.Add(id);
            if (state.Location(id) == null) state.Locations[id] = new LocationState { Id = id };
            events.Notify("New location reached: " + def.Name, NotificationLevel.Success);
            events.RaiseLocationUnlocked(id);
        }
    }
}
