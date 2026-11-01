using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class LogisticsPanel : Panel
    {
        public LogisticsPanel(GameUI ui) : base(ui) { }
        public override string Title => "Logistics";
        public override string LockedHint => Sim.State.HasFeature(Features.Logistics) ? null : "Research Launch Automation";
        public override float PreferredWidth => 760;

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            Heading("Logistics Network", "Routes relaunch automatically whenever the rocket has returned and fuel and cargo are available. Create routes from the Missions screen.");
            Scroll = GUILayout.BeginScrollView(Scroll);
            GUILayout.Label("Transport routes", Theme.Header);
            if (Sim.State.Routes.Count == 0) GUILayout.Label("No routes yet. Press 'Automate' on a mission. Reusable rockets make routes cheap.", Theme.DimLabel);
            int remove = 0;
            foreach (var route in Sim.State.Routes)
            {
                var m = Sim.Db.Mission(route.MissionId); var rk = Sim.Db.Rocket(route.RocketId);
                GUILayout.BeginVertical(Theme.Box);
                GUILayout.BeginHorizontal();
                GUILayout.Label(Gx.Colored(m.Name, Theme.Cyan) + "  " + Sim.Db.Location(m.Origin).Name + " -> " + Sim.Db.Location(m.Destination).Name + Gx.Colored("  " + rk.Name, Theme.Dim), Theme.Label);
                GUILayout.FlexibleSpace();
                route.Enabled = GUILayout.Toggle(route.Enabled, " active", Theme.Label, GUILayout.Width(70));
                if (GUILayout.Button("Remove", Theme.Button, GUILayout.Width(70))) remove = route.Uid;
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(route.CargoResource))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Cargo per flight: " + Sim.Db.ResourceName(route.CargoResource), Theme.Small, GUILayout.Width(240));
                    route.CargoAmount = Mathf.Round(GUILayout.HorizontalSlider((float)route.CargoAmount, 1, (float)rk.CargoCapacity, GUILayout.Width(200)));
                    GUILayout.Label(NumberFormat.Short(route.CargoAmount), Theme.Small);
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label("Status: " + (route.LastStatus ?? "") + "   ·   flights: " + route.Launches + "   ·   fuel per flight: " + Fmt.Amounts(Sim.Db, Sim.Missions.FuelCost(m, rk)), Theme.Small);
                GUILayout.EndVertical();
            }
            if (remove != 0) { Sim.Logistics.RemoveRoute(remove); Gx.Changed(); }

            GUILayout.Label("Fleet & fuel by location", Theme.Header);
            foreach (var id in Sim.State.UnlockedLocations)
            {
                var vehicles = Sim.Db.Rockets.Select(rk => (rk, n: Sim.Inventory.Get(id, rk.VehicleResourceId))).Where(x => x.n > 0).Select(x => x.rk.Name + " x" + x.n.ToString("0"));
                var fuels = Sim.Db.Resources.Where(x => x.Category == ResourceCategory.Fuel).Select(x => x.Name + " " + NumberFormat.Short(Sim.Inventory.Get(id, x.Id)));
                GUILayout.Label(Gx.Colored(Sim.Db.Location(id).Name, Theme.Orange) + ": " + (vehicles.Any() ? string.Join(", ", vehicles) : "no rockets") + Gx.Colored("  |  " + string.Join(", ", fuels), Theme.Dim), Theme.Small);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
