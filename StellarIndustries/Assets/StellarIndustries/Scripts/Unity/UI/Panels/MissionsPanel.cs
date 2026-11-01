using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class MissionsPanel : Panel
    {
        readonly Dictionary<string, int> rocketSel = new Dictionary<string, int>();
        readonly Dictionary<string, int> cargoSel = new Dictionary<string, int>();
        readonly Dictionary<string, float> cargoAmt = new Dictionary<string, float>();

        public MissionsPanel(GameUI ui) : base(ui) { }
        public override string Title => "Missions";
        public override string LockedHint => Sim.State.HasFeature(Features.Missions) ? null : "Research Basic Rocketry";
        public override float PreferredWidth => 760;

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            Heading("Mission Control", "Missions continue in the background - also while you are offline.");
            Scroll = GUILayout.BeginScrollView(Scroll);
            GUILayout.Label("In flight", Theme.Header);
            if (Sim.State.ActiveMissions.Count == 0) GUILayout.Label("No missions in flight.", Theme.DimLabel);
            foreach (var m in Sim.State.ActiveMissions)
            {
                var md = Sim.Db.Mission(m.MissionId); var rd = Sim.Db.Rocket(m.RocketId);
                GUILayout.BeginHorizontal();
                GUILayout.Label(md.Name + Gx.Colored("  " + rd.Name, Theme.Dim) + (m.CargoAmount > 0 ? Gx.Colored("  cargo " + NumberFormat.Short(m.CargoAmount) + " " + Sim.Db.ResourceName(m.CargoResource), Theme.Dim) : "") +
                                (m.RouteUid != 0 ? Gx.Colored("  [route]", Theme.Cyan) : ""), Theme.Label, GUILayout.Width(380));
                Gx.LayoutBar((float)(m.Elapsed / m.Duration), Theme.Cyan, NumberFormat.Duration(m.Duration - m.Elapsed) + " · " + NumberFormat.Percent(m.SuccessChance) + " success", 250, 16);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Available missions", Theme.Header);
            foreach (var m in Sim.Missions.AvailableMissions()) DrawMission(m);

            var upcoming = Sim.Db.Missions.Where(m => !Sim.Missions.IsAvailable(m) && (m.Repeatable || Sim.State.Completions(m.Id) == 0)).ToList();
            if (upcoming.Count > 0)
            {
                GUILayout.Label("Upcoming", Theme.Header);
                foreach (var m in upcoming)
                {
                    string req = !string.IsNullOrEmpty(m.RequiredTech) && !Sim.State.Research.Completed.Contains(m.RequiredTech) ? "research " + Sim.Db.Tech(m.RequiredTech)?.Name
                               : "reach " + string.Join(", ", m.RequiredLocations.Select(l => Sim.Db.Location(l)?.Name));
                    GUILayout.Label(Gx.Colored(m.Name, Theme.Dim) + Gx.Colored("  - requires " + req, Theme.Dim), Theme.Small);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawMission(MissionDef m)
        {
            GUILayout.BeginVertical(Theme.Box);
            bool first = Sim.State.Completions(m.Id) == 0;
            GUILayout.Label(Gx.Colored(m.Name, Theme.Cyan) + Gx.Colored("  " + Sim.Db.Location(m.Origin).Name + " -> " + Sim.Db.Location(m.Destination).Name, Theme.Dim) +
                            (first ? Gx.Colored("  NEW", Theme.Orange) : Gx.Colored("  completed " + Sim.State.Completions(m.Id) + "x", Theme.Dim)), Theme.Label);
            GUILayout.Label(m.Description, Theme.DimLabel);
            var rewards = new List<string>();
            if (m.RewardScience > 0) rewards.Add(NumberFormat.Short(m.RewardScience) + " science");
            if (first && m.FirstTimeRewards.Count > 0) rewards.Add("first time: " + Fmt.Amounts(Sim.Db, m.FirstTimeRewards));
            if (first && !string.IsNullOrEmpty(m.UnlockLocation)) rewards.Add(Gx.Colored("unlocks " + Sim.Db.Location(m.UnlockLocation).Name, Theme.Orange));
            if (rewards.Count > 0) GUILayout.Label("Rewards: " + string.Join(" · ", rewards), Theme.Small);

            var rockets = Sim.Missions.SuitableRockets(m).ToList();
            rocketSel.TryGetValue(m.Id, out int ri);
            if (ri >= rockets.Count) ri = 0;
            // Prefer a rocket that is actually available at the origin.
            if (!rocketSel.ContainsKey(m.Id))
            {
                int avail = rockets.FindIndex(x => Sim.Inventory.Get(m.Origin, x.VehicleResourceId) >= 1);
                ri = avail >= 0 ? avail : 0;
            }
            var rocket = rockets.Count > 0 ? rockets[ri] : null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", Theme.Button, GUILayout.Width(22))) { rocketSel[m.Id] = (ri - 1 + rockets.Count) % rockets.Count; Gx.Changed(); }
            GUILayout.Label(rocket == null ? "-" : rocket.Name + Gx.Colored("  (" + NumberFormat.Short(Sim.Inventory.Get(m.Origin, rocket.VehicleResourceId)) + " ready)", Theme.Dim), Theme.Label, GUILayout.Width(250));
            if (GUILayout.Button(">", Theme.Button, GUILayout.Width(22))) { rocketSel[m.Id] = (ri + 1) % rockets.Count; Gx.Changed(); }
            if (rocket != null)
                GUILayout.Label($"{NumberFormat.Duration(Sim.Missions.Duration(m, rocket))} · {NumberFormat.Percent(Sim.Missions.SuccessChance(m, rocket))} success", Theme.Small);
            GUILayout.EndHorizontal();
            if (rocket == null) { GUILayout.EndVertical(); return; }

            string cargoRes = ""; double cargo = 0;
            if (m.AllowsCargo && rocket.CargoCapacity > 0)
            {
                var options = Sim.State.Location(m.Origin).Inventory.Where(kv => kv.Value >= 1 && Sim.Db.Resource(kv.Key)?.Category != ResourceCategory.Vehicle).Select(kv => kv.Key).OrderBy(k => k).ToList();
                if (options.Count > 0)
                {
                    cargoSel.TryGetValue(m.Id, out int ci);
                    if (ci >= options.Count) ci = 0;
                    cargoRes = options[ci];
                    cargoAmt.TryGetValue(m.Id, out float amt);
                    double max = System.Math.Min(rocket.CargoCapacity, Sim.Inventory.Get(m.Origin, cargoRes));
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Cargo:", Theme.Small, GUILayout.Width(44));
                    if (GUILayout.Button("<", Theme.Button, GUILayout.Width(22))) cargoSel[m.Id] = (ci - 1 + options.Count) % options.Count;
                    GUILayout.Label(Sim.Db.ResourceName(cargoRes), Theme.Label, GUILayout.Width(150));
                    if (GUILayout.Button(">", Theme.Button, GUILayout.Width(22))) cargoSel[m.Id] = (ci + 1) % options.Count;
                    amt = Mathf.Round(GUILayout.HorizontalSlider(Mathf.Min(amt, (float)max), 0, (float)max, GUILayout.Width(180)));
                    cargoAmt[m.Id] = amt;
                    GUILayout.Label(NumberFormat.Short(amt) + " / " + NumberFormat.Short(rocket.CargoCapacity), Theme.Small);
                    GUILayout.EndHorizontal();
                    cargo = amt;
                }
            }
            GUILayout.Label("Launch cost: " + Fmt.Cost(Sim, m.Origin, Sim.Missions.TotalLaunchCost(m, rocket, cargoRes, cargo)), Theme.Small);
            GUILayout.BeginHorizontal();
            var problem = Sim.Missions.LaunchProblem(m.Id, rocket.Id, cargoRes, cargo);
            if (Gx.Button("LAUNCH", problem == null, problem, Theme.Accent, GUILayout.Width(110), GUILayout.Height(30)))
            { Sim.Missions.Launch(m.Id, rocket.Id, cargoRes, cargo); Gx.Changed(); }
            if (Sim.Logistics.Unlocked && m.Repeatable)
            {
                if (Gx.Button("Automate", true, "Create a transport route that flies this mission repeatedly", Theme.Button, GUILayout.Width(100), GUILayout.Height(30)))
                {
                    Sim.Logistics.CreateRoute(m.Id, rocket.Id, cargoRes, cargo > 0 ? cargo : rocket.CargoCapacity);
                    UI.Notify("Route created. Manage it in Logistics.", Core.Systems.NotificationLevel.Success);
                    Gx.Changed();
                }
            }
            if (problem != null) GUILayout.Label(Gx.Colored(problem, Theme.Bad), Theme.Small);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
