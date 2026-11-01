using System;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Save;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Systems;

namespace StellarIndustries.Core.Simulation
{
    /// <summary>
    /// Facade that wires all gameplay systems around one GameState. Has no engine
    /// dependency: Unity drives it via Tick(dt), tests drive it directly.
    /// </summary>
    public class GameSimulation
    {
        public readonly GameDatabase Db;
        public readonly GameState State;
        public readonly GameEvents Events = new GameEvents();
        public readonly ModifierSystem Modifiers;
        public readonly InventorySystem Inventory;
        public readonly ProductionSystem Production;
        public readonly ConstructionSystem Construction;
        public readonly ResearchSystem Research;
        public readonly MissionSystem Missions;
        public readonly LogisticsSystem Logistics;
        public readonly AchievementSystem Achievements;
        public readonly PlacementSystem Placement;

        double rateTimer, slowTimer;

        public GameSimulation(GameDatabase db, GameState state)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            State = state ?? throw new ArgumentNullException(nameof(state));
            SaveSanitizer.Sanitize(state, db, null);
            Modifiers = new ModifierSystem(db, state);
            Inventory = new InventorySystem(db, state, Modifiers);
            Placement = new PlacementSystem(db, state);
            Production = new ProductionSystem(db, state, Modifiers, Inventory) { Placement = Placement };
            Construction = new ConstructionSystem(db, state, Inventory, Modifiers, Events, () => { Production.MarkDirty(); Inventory.InvalidateStorage(); }, Placement);
            Research = new ResearchSystem(db, state, Inventory, Modifiers, Events, () => { Modifiers.Rebuild(); Inventory.InvalidateStorage(); });
            Missions = new MissionSystem(db, state, Inventory, Modifiers, Events);
            Logistics = new LogisticsSystem(db, state, Inventory, Missions);
            Achievements = new AchievementSystem(db, state, Events);
            Placement.PlaceAllUnplaced(); // saves from before the surface update
        }

        public static GameState CreateNewState(GameDatabase db, string name, uint seed)
        {
            var s = new GameState
            {
                Version = SaveSerializer.CurrentVersion,
                SaveName = string.IsNullOrEmpty(name) ? "Stellar Industries" : name,
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
                LastSavedUtcTicks = DateTime.UtcNow.Ticks,
                RngState = seed == 0 ? 12345u : seed,
                CurrentLocation = db.Start.StartLocation,
            };
            foreach (var l in db.Locations)
                if (l.StartUnlocked || l.Id == db.Start.StartLocation)
                {
                    s.UnlockedLocations.Add(l.Id);
                    s.Locations[l.Id] = new LocationState { Id = l.Id };
                }
            return s;
        }

        public static GameSimulation NewGame(GameDatabase db, string name = null, uint seed = 12345)
        {
            var sim = new GameSimulation(db, CreateNewState(db, name, seed));
            foreach (var r in db.Start.Resources) sim.Inventory.Add(db.Start.StartLocation, r.ResourceId, r.Amount, false);
            foreach (var b in db.Start.Buildings)
            {
                var def = db.Building(b.BuildingId);
                if (def != null) sim.Construction.Place(def, string.IsNullOrEmpty(b.LocationId) ? db.Start.StartLocation : b.LocationId, b.RecipeId);
            }
            return sim;
        }

        /// <summary>Advance the simulation in real time.</summary>
        public void Tick(double dt)
        {
            if (dt <= 0) return;
            Step(dt, 1);
            State.PlayTime += dt;
        }

        /// <summary>
        /// Advance by dt seconds. productionScale &lt; 1 is used for offline progression:
        /// industry and research run at reduced efficiency, missions in flight progress in real time.
        /// </summary>
        public void Step(double dt, double productionScale)
        {
            if (dt <= 0 || double.IsNaN(dt)) return;
            double pdt = dt * Math.Max(0, productionScale);
            State.GameTime += dt;
            Production.Tick(pdt);
            Research.Tick(pdt);
            Missions.Tick(dt);
            Logistics.Tick(dt);
            rateTimer += dt;
            if (rateTimer >= 1) { Inventory.UpdateRates(rateTimer); rateTimer = 0; }
            slowTimer += dt;
            if (slowTimer >= 1) { Achievements.Check(); slowTimer = 0; }
        }
    }
}
