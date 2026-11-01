using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Save
{
    /// <summary>
    /// Repairs loaded state against the current content database: removes references to
    /// content that no longer exists, fixes invalid numbers and restores invariants.
    /// Makes saves robust against content changes and partial corruption.
    /// </summary>
    public static class SaveSanitizer
    {
        public static void Sanitize(GameState s, GameDatabase db, List<string> warnings)
        {
            void Warn(string w) => warnings?.Add(w);
            if (s.Locations == null) s.Locations = new Dictionary<string, LocationState>();
            if (s.GlobalInventory == null) s.GlobalInventory = new Dictionary<string, double>();
            if (s.Buildings == null) s.Buildings = new List<BuildingInstance>();
            if (s.Research == null) s.Research = new ResearchState();
            if (s.Research.Completed == null) s.Research.Completed = new List<string>();
            if (s.Research.Queue == null) s.Research.Queue = new List<string>();
            if (s.ActiveMissions == null) s.ActiveMissions = new List<MissionInstance>();
            if (s.Routes == null) s.Routes = new List<TransportRoute>();
            if (s.UnlockedLocations == null) s.UnlockedLocations = new List<string>();
            if (s.MissionCompletions == null) s.MissionCompletions = new Dictionary<string, int>();
            if (s.Features == null) s.Features = new List<string>();
            if (s.Achievements == null) s.Achievements = new List<string>();
            if (s.Stats == null) s.Stats = new Statistics();
            if (s.Stats.Produced == null) s.Stats.Produced = new Dictionary<string, double>();
            if (s.Settings == null) s.Settings = new GameSettings();
            if (s.RngState == 0) s.RngState = 12345;

            int removedLoc = s.UnlockedLocations.RemoveAll(id => db.Location(id) == null);
            if (removedLoc > 0) Warn("Removed " + removedLoc + " unknown locations");
            foreach (var l in db.Locations)
                if ((l.StartUnlocked || l.Id == db.Start.StartLocation) && !s.UnlockedLocations.Contains(l.Id)) s.UnlockedLocations.Add(l.Id);
            s.UnlockedLocations = s.UnlockedLocations.Distinct().ToList();
            foreach (var key in s.Locations.Keys.ToList())
                if (db.Location(key) == null) s.Locations.Remove(key);
            foreach (var id in s.UnlockedLocations)
                if (!s.Locations.ContainsKey(id) || s.Locations[id] == null) s.Locations[id] = new LocationState { Id = id };
            foreach (var kv in s.Locations)
            {
                var loc = kv.Value;
                loc.Id = kv.Key;
                if (loc.Inventory == null) loc.Inventory = new Dictionary<string, double>();
                CleanInventory(loc.Inventory, db, false, Warn);
                if (loc.Rates == null) loc.Rates = new Dictionary<string, double>();
                if (loc.RateAccumulator == null) loc.RateAccumulator = new Dictionary<string, double>();
                loc.EnergySatisfaction = 1;
            }
            CleanInventory(s.GlobalInventory, db, true, Warn);

            int removedB = s.Buildings.RemoveAll(b => b == null || db.Building(b.DefId) == null || !s.UnlockedLocations.Contains(b.LocationId));
            if (removedB > 0) Warn("Removed " + removedB + " invalid buildings");
            var seen = new HashSet<int>();
            foreach (var b in s.Buildings)
            {
                var def = db.Building(b.DefId);
                b.Level = Math.Max(1, Math.Min(def.MaxLevel, b.Level));
                b.Priority = Math.Max(0, Math.Min(2, b.Priority));
                if (double.IsNaN(b.Progress) || b.Progress < 0 || b.Progress > 1e6) b.Progress = 0;
                if (b.RecipeId == null || (def.Recipes.Count > 0 && !def.Recipes.Contains(b.RecipeId)))
                {
                    b.RecipeId = def.Recipes.Count > 0 ? def.Recipes[0] : "";
                    Warn("Reset recipe of building " + b.Uid);
                }
                if (b.Uid <= 0 || !seen.Add(b.Uid)) { b.Uid = 0; }
            }
            int maxUid = 0;
            foreach (var b in s.Buildings) maxUid = Math.Max(maxUid, b.Uid);
            foreach (var m in s.ActiveMissions) maxUid = Math.Max(maxUid, m.Uid);
            foreach (var r in s.Routes) maxUid = Math.Max(maxUid, r.Uid);
            if (s.NextUid <= maxUid) s.NextUid = maxUid + 1;
            foreach (var b in s.Buildings) if (b.Uid == 0) b.Uid = s.NextUid++;

            s.Research.Completed = s.Research.Completed.Where(t => db.Tech(t) != null).Distinct().ToList();
            s.Research.Queue = s.Research.Queue.Where(t => db.Tech(t) != null && !s.Research.Completed.Contains(t)).Distinct().ToList();
            if (!string.IsNullOrEmpty(s.Research.ActiveTech) && (db.Tech(s.Research.ActiveTech) == null || s.Research.Completed.Contains(s.Research.ActiveTech)))
            { s.Research.ActiveTech = ""; s.Research.ActiveProgress = 0; }
            if (s.Research.ActiveTech == null) s.Research.ActiveTech = "";
            if (double.IsNaN(s.Research.ActiveProgress) || s.Research.ActiveProgress < 0) s.Research.ActiveProgress = 0;

            // Features granted by completed techs must always be present.
            foreach (var t in s.Research.Completed)
                foreach (var f in db.Tech(t).UnlockFeatures) if (!s.Features.Contains(f)) s.Features.Add(f);

            int removedM = s.ActiveMissions.RemoveAll(m => m == null || db.Mission(m.MissionId) == null || db.Rocket(m.RocketId) == null);
            if (removedM > 0) Warn("Removed " + removedM + " invalid missions");
            foreach (var m in s.ActiveMissions)
            {
                if (double.IsNaN(m.Elapsed) || m.Elapsed < 0) m.Elapsed = 0;
                if (m.Duration <= 0 || double.IsNaN(m.Duration)) m.Duration = db.Mission(m.MissionId).Duration;
                if (m.SuccessChance <= 0 || m.SuccessChance > 1 || double.IsNaN(m.SuccessChance)) m.SuccessChance = 0.9;
                if (m.CargoResource == null || db.Resource(m.CargoResource) == null) { m.CargoResource = ""; m.CargoAmount = 0; }
            }
            s.Routes.RemoveAll(r => r == null || db.Mission(r.MissionId) == null || db.Rocket(r.RocketId) == null);
            foreach (var r in s.Routes) { if (r.CargoResource == null) r.CargoResource = ""; r.LastStatus = ""; }
            foreach (var key in s.MissionCompletions.Keys.ToList()) if (db.Mission(key) == null) s.MissionCompletions.Remove(key);

            if (string.IsNullOrEmpty(s.CurrentLocation) || !s.UnlockedLocations.Contains(s.CurrentLocation)) s.CurrentLocation = db.Start.StartLocation;
            if (double.IsNaN(s.PlayTime) || s.PlayTime < 0) s.PlayTime = 0;
            s.Settings.UiScale = Math.Max(0.6f, Math.Min(2f, s.Settings.UiScale <= 0 ? 1 : s.Settings.UiScale));
            s.Settings.AutosaveSeconds = Math.Max(10f, Math.Min(600f, s.Settings.AutosaveSeconds <= 0 ? 30 : s.Settings.AutosaveSeconds));
        }

        static void CleanInventory(Dictionary<string, double> inv, GameDatabase db, bool global, Action<string> warn)
        {
            foreach (var key in inv.Keys.ToList())
            {
                var r = db.Resource(key);
                double v = inv[key];
                if (r == null || r.IsGlobal != global) { inv.Remove(key); continue; }
                if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) { inv[key] = 0; warn("Fixed invalid amount of " + key); }
            }
        }
    }
}
