using System;
using System.Collections.Generic;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    /// <summary>
    /// All resource changes go through here: storage limits, global resources, production
    /// statistics and rate tracking are handled in one place.
    /// </summary>
    public class InventorySystem
    {
        readonly GameDatabase db;
        readonly GameState state;
        readonly ModifierSystem mods;
        readonly Dictionary<string, double> storageMultCache = new Dictionary<string, double>();

        public InventorySystem(GameDatabase db, GameState state, ModifierSystem mods) { this.db = db; this.state = state; this.mods = mods; }

        public static double DefaultStorage(ResourceCategory c)
        {
            switch (c)
            {
                case ResourceCategory.Raw: return 500;
                case ResourceCategory.Material: return 300;
                case ResourceCategory.Fuel: return 600;
                case ResourceCategory.Component: return 100;
                case ResourceCategory.Vehicle: return 5;
                default: return double.MaxValue;
            }
        }

        public void InvalidateStorage() => storageMultCache.Clear();

        double StorageMultiplier(string locationId)
        {
            if (storageMultCache.TryGetValue(locationId, out var m)) return m;
            double bonus = 0;
            foreach (var b in state.Buildings)
            {
                if (b.LocationId != locationId) continue;
                var def = db.Building(b.DefId);
                if (def != null && def.StorageBonus > 0) bonus += def.StorageBonus * b.Level;
            }
            m = (1 + bonus) * mods.Multiplier(ModifierType.StorageCapacity);
            storageMultCache[locationId] = m;
            return m;
        }

        public double Capacity(string locationId, string resourceId)
        {
            var def = db.Resource(resourceId);
            if (def == null) return 0;
            if (def.IsGlobal) return double.MaxValue;
            double baseCap = def.BaseStorage >= 0 ? def.BaseStorage : DefaultStorage(def.Category);
            if (baseCap == double.MaxValue) return baseCap;
            return Math.Floor(baseCap * StorageMultiplier(locationId));
        }

        Dictionary<string, double> Store(string locationId, string resourceId)
        {
            var def = db.Resource(resourceId);
            if (def != null && def.IsGlobal) return state.GlobalInventory;
            return state.Location(locationId)?.Inventory;
        }

        public double Get(string locationId, string resourceId)
        {
            var s = Store(locationId, resourceId);
            return s != null && s.TryGetValue(resourceId, out var v) ? v : 0;
        }

        public double FreeSpace(string locationId, string resourceId) => Math.Max(0, Capacity(locationId, resourceId) - Get(locationId, resourceId));

        /// <summary>Adds up to the storage limit. Returns the amount actually added.</summary>
        public double Add(string locationId, string resourceId, double amount, bool countAsProduced = true, bool ignoreCapacity = false)
        {
            if (amount <= 0) return 0;
            var s = Store(locationId, resourceId);
            if (s == null || db.Resource(resourceId) == null) return 0;
            double added = ignoreCapacity ? amount : Math.Min(amount, FreeSpace(locationId, resourceId));
            if (added <= 0) return 0;
            s.TryGetValue(resourceId, out var cur);
            s[resourceId] = cur + added;
            if (countAsProduced)
            {
                state.Stats.Produced.TryGetValue(resourceId, out var p);
                state.Stats.Produced[resourceId] = p + added;
            }
            Track(locationId, resourceId, added);
            return added;
        }

        public bool Remove(string locationId, string resourceId, double amount)
        {
            if (amount <= 0) return true;
            var s = Store(locationId, resourceId);
            if (s == null) return false;
            s.TryGetValue(resourceId, out var cur);
            if (cur + 1e-9 < amount) return false;
            s[resourceId] = Math.Max(0, cur - amount);
            Track(locationId, resourceId, -amount);
            return true;
        }

        public bool Has(string locationId, IList<ResourceAmount> cost, double multiplier = 1)
        {
            foreach (var c in cost) if (Get(locationId, c.ResourceId) + 1e-9 < c.Amount * multiplier) return false;
            return true;
        }

        public bool TryPay(string locationId, IList<ResourceAmount> cost, double multiplier = 1)
        {
            if (!Has(locationId, cost, multiplier)) return false;
            foreach (var c in cost) Remove(locationId, c.ResourceId, c.Amount * multiplier);
            return true;
        }

        public string MissingText(string locationId, IList<ResourceAmount> cost, double multiplier = 1)
        {
            var parts = new List<string>();
            foreach (var c in cost)
            {
                double need = c.Amount * multiplier, have = Get(locationId, c.ResourceId);
                if (have + 1e-9 < need) parts.Add(db.ResourceName(c.ResourceId) + " " + Util.NumberFormat.Short(have) + "/" + Util.NumberFormat.Short(need));
            }
            return parts.Count == 0 ? "" : "Missing: " + string.Join(", ", parts);
        }

        void Track(string locationId, string resourceId, double delta)
        {
            var def = db.Resource(resourceId);
            var loc = state.Location(def != null && def.IsGlobal ? state.CurrentLocation : locationId);
            if (loc == null) return;
            if (loc.RateAccumulator == null) loc.RateAccumulator = new Dictionary<string, double>();
            loc.RateAccumulator.TryGetValue(resourceId, out var a);
            loc.RateAccumulator[resourceId] = a + delta;
        }

        /// <summary>Converts accumulated deltas into smoothed per-second rates.</summary>
        public void UpdateRates(double window)
        {
            if (window <= 0) return;
            foreach (var loc in state.Locations.Values)
            {
                if (loc.Rates == null) loc.Rates = new Dictionary<string, double>();
                if (loc.RateAccumulator == null) loc.RateAccumulator = new Dictionary<string, double>();
                var keys = new List<string>(loc.Rates.Keys);
                foreach (var k in loc.RateAccumulator.Keys) if (!loc.Rates.ContainsKey(k)) keys.Add(k);
                foreach (var k in keys)
                {
                    loc.RateAccumulator.TryGetValue(k, out var acc);
                    loc.Rates.TryGetValue(k, out var prev);
                    double r = acc / window;
                    loc.Rates[k] = prev * 0.5 + r * 0.5;
                    if (Math.Abs(loc.Rates[k]) < 1e-4) loc.Rates.Remove(k);
                }
                loc.RateAccumulator.Clear();
            }
        }
    }
}
