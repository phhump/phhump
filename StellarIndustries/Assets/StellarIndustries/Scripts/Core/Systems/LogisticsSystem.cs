using System;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    /// <summary>
    /// Automated transport routes: a route repeatedly flies a mission with a chosen rocket
    /// and cargo whenever the previous flight has finished and resources are available.
    /// </summary>
    public class LogisticsSystem
    {
        readonly GameDatabase db;
        readonly GameState state;
        readonly InventorySystem inv;
        readonly MissionSystem missions;
        double timer;

        public LogisticsSystem(GameDatabase db, GameState state, InventorySystem inv, MissionSystem missions)
        { this.db = db; this.state = state; this.inv = inv; this.missions = missions; }

        public bool Unlocked => state.HasFeature(Features.LaunchAutomation);

        public TransportRoute CreateRoute(string missionId, string rocketId, string cargoRes, double cargoAmount)
        {
            if (!Unlocked) return null;
            var m = db.Mission(missionId); var r = db.Rocket(rocketId);
            if (m == null || r == null || !missions.RocketSuitable(m, r)) return null;
            var route = new TransportRoute
            {
                Uid = state.TakeUid(), MissionId = missionId, RocketId = rocketId,
                CargoResource = m.AllowsCargo ? (cargoRes ?? "") : "", CargoAmount = m.AllowsCargo ? Math.Min(cargoAmount, r.CargoCapacity) : 0,
            };
            state.Routes.Add(route);
            return route;
        }

        public void RemoveRoute(int uid) => state.Routes.RemoveAll(r => r.Uid == uid);

        public void Tick(double dt)
        {
            if (state.Routes.Count == 0 || !Unlocked) return;
            timer += dt;
            if (timer < 1) return;
            timer = 0;
            foreach (var route in state.Routes)
            {
                if (!route.Enabled) { route.LastStatus = "Paused"; continue; }
                if (state.ActiveMissions.Exists(mi => mi.RouteUid == route.Uid)) { route.LastStatus = "In flight"; continue; }
                var m = db.Mission(route.MissionId); var r = db.Rocket(route.RocketId);
                if (m == null || r == null) { route.LastStatus = "Invalid route"; continue; }
                double amount = 0;
                if (!string.IsNullOrEmpty(route.CargoResource) && route.CargoAmount > 0)
                {
                    double reservedForFuel = 0;
                    foreach (var f in missions.FuelCost(m, r)) if (f.ResourceId == route.CargoResource) reservedForFuel += f.Amount;
                    double available = inv.Get(m.Origin, route.CargoResource) - reservedForFuel;
                    double wanted = Math.Min(route.CargoAmount, r.CargoCapacity);
                    if (available < wanted * 0.25) { route.LastStatus = "Waiting for cargo"; continue; }
                    amount = Math.Floor(Math.Min(wanted, available));
                }
                var problem = missions.LaunchProblem(m.Id, r.Id, route.CargoResource, amount);
                if (problem != null) { route.LastStatus = problem; continue; }
                if (missions.Launch(m.Id, r.Id, route.CargoResource, amount, route.Uid) != null)
                {
                    route.Launches++;
                    route.LastStatus = "Launched";
                }
            }
        }
    }
}
