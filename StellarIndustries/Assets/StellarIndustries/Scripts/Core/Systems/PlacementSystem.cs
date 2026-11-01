using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    /// <summary>A deposit field on a location's surface: extraction buildings must stand inside one.</summary>
    public struct DepositPatch
    {
        public string ResourceId;
        public double X, Z, Radius, Richness;
        public bool Contains(double x, double z) => (x - X) * (x - X) + (z - Z) * (z - Z) <= Radius * Radius;
    }

    /// <summary>
    /// Spatial rules for the surface: deterministic deposit fields, footprints, overlap checks,
    /// player placement (with position) and automatic placement (start kit, old saves, quick-build UI, tests).
    /// </summary>
    public class PlacementSystem
    {
        public const double SpawnClearRadius = 22;   // landing pad / spawn area stays free
        readonly GameDatabase db;
        readonly GameState state;
        readonly Dictionary<string, List<DepositPatch>> patchCache = new Dictionary<string, List<DepositPatch>>();

        // Spatial hash (32 m cells) of placed buildings: overlap checks are O(neighbours) instead of O(all buildings).
        const double Cell = 32;
        readonly Dictionary<(string, int, int), List<BuildingInstance>> grid = new Dictionary<(string, int, int), List<BuildingInstance>>();
        int gridCount = -1;
        bool gridDirty = true;
        double maxRadius;

        public void MarkDirty() => gridDirty = true;
        /// <summary>After a demolition, space frees up again: restart ring searches from the centre.</summary>
        public void ForgetMemo() { ringMemo.Clear(); gridDirty = true; }

        static (int, int) CellOf(double x, double z) => ((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell));

        void EnsureGrid()
        {
            if (!gridDirty && gridCount == state.Buildings.Count) return;
            grid.Clear();
            maxRadius = 0;
            foreach (var b in state.Buildings) if (b.Placed) AddToGrid(b);
            gridCount = state.Buildings.Count;
            gridDirty = false;
        }

        void AddToGrid(BuildingInstance b)
        {
            var (cx, cz) = CellOf(b.PosX, b.PosZ);
            var key = (b.LocationId, cx, cz);
            if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<BuildingInstance>();
            list.Add(b);
            var def = db.Building(b.DefId);
            if (def != null) maxRadius = Math.Max(maxRadius, Radius(def));
        }

        public PlacementSystem(GameDatabase db, GameState state) { this.db = db; this.state = state; }

        static uint Hash(string s) { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } return h; }

        /// <summary>Deposit fields, generated deterministically from the location id (same layout for every player).</summary>
        public List<DepositPatch> Patches(string locationId)
        {
            if (patchCache.TryGetValue(locationId, out var list)) return list;
            list = new List<DepositPatch>();
            var loc = db.Location(locationId);
            if (loc != null)
            {
                uint rng = Hash(locationId) | 1;
                double half = loc.SurfaceHalfSizeM;
                int i = 0;
                foreach (var d in loc.Deposits)
                {
                    int count = 2 + (int)Math.Round(d.Richness);
                    for (int k = 0; k < count; k++, i++)
                    {
                        // first field of every deposit lies close to the landing site so the start is quick
                        double minR = k == 0 ? 50 : 110, maxR = k == 0 ? 120 + 12 * i : half - 50;
                        double radius = 26 + 18 * d.Richness * (0.7 + 0.6 * Util.Rng.NextDouble(ref rng));
                        for (int attempt = 0; attempt < 40; attempt++)
                        {
                            double ang = Util.Rng.NextDouble(ref rng) * Math.PI * 2;
                            double dist = minR + (maxR - minR) * Util.Rng.NextDouble(ref rng);
                            double x = Math.Cos(ang) * dist, z = Math.Sin(ang) * dist;
                            if (Math.Abs(x) > half - radius || Math.Abs(z) > half - radius) continue;
                            if (list.Any(p => Math.Sqrt((p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z)) < p.Radius + radius + 8)) continue;
                            list.Add(new DepositPatch { ResourceId = d.ResourceId, X = x, Z = z, Radius = radius, Richness = d.Richness });
                            break;
                        }
                    }
                }
            }
            patchCache[locationId] = list;
            return list;
        }

        public IEnumerable<DepositPatch> PatchesAt(string locationId, double x, double z) => Patches(locationId).Where(p => p.Contains(x, z));

        /// <summary>Is the building standing on a field of this deposit? Unplaced/exempt buildings always count as yes.
        /// Called every tick for every extractor: written as a plain loop so it allocates nothing.</summary>
        public bool OnDeposit(BuildingInstance b, string resourceId)
        {
            if (b == null || !b.Placed || b.DepositExempt) return true;
            var list = Patches(b.LocationId);
            for (int i = 0; i < list.Count; i++)
                if (list[i].ResourceId == resourceId && list[i].Contains(b.PosX, b.PosZ)) return true;
            return false;
        }

        /// <summary>Footprint radius plus 2.5 m clearance, so neighbouring buildings always leave a ~5 m walkway.</summary>
        public static double Radius(BuildingDef def) => def.FootprintM / 2 + 2.5;

        /// <summary>Null when the spot is free and legal (ignores cost and tech), otherwise the reason.</summary>
        public string SpotProblem(BuildingDef def, string locationId, double x, double z, int ignoreUid = 0)
        {
            var loc = db.Location(locationId);
            if (loc == null) return "Unknown location";
            double r = Radius(def), half = loc.SurfaceHalfSizeM;
            if (Math.Abs(x) > half - r || Math.Abs(z) > half - r) return "Outside the surveyed area";
            if (x * x + z * z < (SpawnClearRadius + r) * (SpawnClearRadius + r)) return "Keep the landing pad clear";
            EnsureGrid();
            int reach = (int)Math.Ceiling((r + maxRadius) / Cell);
            var (gx, gz) = CellOf(x, z);
            for (int cx = gx - reach; cx <= gx + reach; cx++)
                for (int cz = gz - reach; cz <= gz + reach; cz++)
                {
                    if (!grid.TryGetValue((locationId, cx, cz), out var cell)) continue;
                    foreach (var b in cell)
                    {
                        if (b.Uid == ignoreUid) continue;
                        var od = db.Building(b.DefId);
                        if (od == null) continue;
                        double min = r + Radius(od);
                        if ((b.PosX - x) * (b.PosX - x) + (b.PosZ - z) * (b.PosZ - z) < min * min) return "Too close to " + od.Name;
                    }
                }
            // Extraction buildings need a matching deposit field under them.
            if (def.PowerOutput <= 0 && def.Recipes.Count > 0 && def.Category == BuildingCategory.Extraction)
            {
                var here = PatchesAt(locationId, x, z).Select(p => p.ResourceId).ToList();
                bool any = def.Recipes.Select(db.Recipe).Any(rc => rc != null && !string.IsNullOrEmpty(rc.RequiredDeposit) && here.Contains(rc.RequiredDeposit));
                if (!any) return "Must be built on a deposit field (" + string.Join(", ", def.Recipes.Select(db.Recipe).Where(rc => rc != null && !string.IsNullOrEmpty(rc.RequiredDeposit))
                                     .Select(rc => db.ResourceName(rc.RequiredDeposit)).Distinct()) + ")";
            }
            return null;
        }

        /// <summary>Finds the closest free spot, preferably on a field of the given deposit. Deterministic.</summary>
        public bool FindSpot(BuildingDef def, string locationId, string depositResource, out double x, out double z)
        {
            x = z = 0;
            var centers = new List<(double x, double z, double r)>();
            if (!string.IsNullOrEmpty(depositResource))
                foreach (var p in Patches(locationId).Where(p => p.ResourceId == depositResource).OrderBy(p => p.X * p.X + p.Z * p.Z)) centers.Add((p.X, p.Z, p.Radius));
            if (centers.Count == 0) centers.Add((0, 0, db.Location(locationId)?.SurfaceHalfSizeM ?? 300));
            double step = Math.Max(4, def.FootprintM * 0.6);
            foreach (var c in centers)
            {
                bool onPatch = c.r < 200;
                // Rings below the last successful one are full already (buildings are never moved): resume there.
                var memoKey = (locationId, Math.Round(c.x), Math.Round(c.z), def.FootprintM);
                ringMemo.TryGetValue(memoKey, out double startRing);
                for (double ring = startRing; ring <= c.r; ring += step)
                {
                    int n = ring < 1 ? 1 : (int)Math.Ceiling(2 * Math.PI * ring / step);
                    for (int k = 0; k < n; k++)
                    {
                        double a = k * 2 * Math.PI / n + ring * 0.37;
                        double px = c.x + Math.Cos(a) * ring, pz = c.z + Math.Sin(a) * ring;
                        if (onPatch && (px - c.x) * (px - c.x) + (pz - c.z) * (pz - c.z) > c.r * c.r) continue;
                        if (SpotProblem(def, locationId, px, pz) == null || (!onPatch && SpotProblemIgnoringDeposit(def, locationId, px, pz) == null))
                        { x = px; z = pz; ringMemo[memoKey] = Math.Max(0, ring - step); return true; }
                    }
                }
                ringMemo[memoKey] = c.r + step; // this field is full for this footprint
            }
            return false;
        }

        readonly Dictionary<(string, double, double, double), double> ringMemo = new Dictionary<(string, double, double, double), double>();

        string SpotProblemIgnoringDeposit(BuildingDef def, string locationId, double x, double z)
        {
            var p = SpotProblem(def, locationId, x, z);
            return p != null && p.StartsWith("Must be built on a deposit") ? null : p;
        }

        /// <summary>Gives an unplaced building a position (on a matching deposit field if it extracts).</summary>
        public void AutoPlace(BuildingInstance b)
        {
            if (b.Placed) return;
            var def = db.Building(b.DefId);
            if (def == null) return;
            var deposit = db.Recipe(b.RecipeId)?.RequiredDeposit;
            if (FindSpot(def, b.LocationId, deposit, out var x, out var z))
            {
                b.PosX = x; b.PosZ = z;
                b.DepositExempt = !string.IsNullOrEmpty(deposit) && !PatchesAt(b.LocationId, x, z).Any(p => p.ResourceId == deposit);
            }
            else
            {
                // Area full: stack outside the plot edge; keep producing (grandfathered).
                b.PosX = (db.Location(b.LocationId)?.SurfaceHalfSizeM ?? 300) + 20 + (b.Uid % 50) * 3; b.PosZ = 0; b.DepositExempt = true;
            }
            b.RotationDeg = (b.Uid * 47) % 360;
            b.Placed = true;
            if (!gridDirty) { AddToGrid(b); gridCount = state.Buildings.Count; }
        }

        public void PlaceAllUnplaced() { foreach (var b in state.Buildings.ToList()) if (!b.Placed) AutoPlace(b); }
    }
}
