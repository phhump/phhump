using System.Linq;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class OverviewPanel : Panel
    {
        public OverviewPanel(GameUI ui) : base(ui) { }
        public override string Title => "Overview";

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            var def = Sim.Db.Location(Loc);
            var loc = Sim.State.Location(Loc);
            Heading(def.Name + " Operations", def.Description);
            Scroll = GUILayout.BeginScrollView(Scroll);

            var obj = Sim.Achievements.CurrentObjective;
            if (obj != null)
            {
                GUILayout.Label("Current objective", Theme.Header);
                GUILayout.Label(Gx.Colored(obj.Name, Theme.Orange) + " - " + obj.Description, Theme.Wrap);
                Gx.LayoutBar((float)(Sim.Achievements.Progress(obj) / obj.Threshold), Theme.Orange, null, 300);
            }

            GUILayout.Label("Energy", Theme.Header);
            GUILayout.BeginHorizontal();
            Gx.LayoutBar(loc.EnergyDemand <= 0 ? 1 : (float)(loc.EnergySupply / loc.EnergyDemand / 2), loc.EnergySatisfaction >= 0.999 ? Theme.Good : Theme.Warn,
                NumberFormat.Short(loc.EnergySupply) + " MW supply / " + NumberFormat.Short(loc.EnergyDemand) + " MW demand", 360, 16);
            GUILayout.Label("  Satisfaction " + NumberFormat.Percent(loc.EnergySatisfaction), Theme.Label);
            GUILayout.EndHorizontal();
            if (loc.EnergySatisfaction < 0.999) GUILayout.Label(Gx.Colored("Power shortage! Production is slowed. Build power plants in Industries.", Theme.Warn), Theme.Wrap);

            GUILayout.Label("Production (net per second)", Theme.Header);
            var rates = loc.Rates.Where(k => Sim.Db.Resource(k.Key) != null && !Sim.Db.Resource(k.Key).IsGlobal).OrderByDescending(k => System.Math.Abs(k.Value)).Take(10).ToList();
            if (rates.Count == 0) GUILayout.Label("No production yet. Build mines and smelters in Industries.", Theme.DimLabel);
            foreach (var kv in rates)
            {
                GUILayout.BeginHorizontal();
                Gx.Swatch(Fmt.ResColor(Sim.Db, kv.Key));
                GUILayout.Label(Sim.Db.ResourceName(kv.Key), Theme.Label, GUILayout.Width(200));
                GUILayout.Label(NumberFormat.Short(Sim.Inventory.Get(Loc, kv.Key)), Theme.Label, GUILayout.Width(80));
                GUILayout.Label(Gx.Colored(NumberFormat.Rate(kv.Value), kv.Value >= 0 ? Theme.Good : Theme.Bad), Theme.Label);
                GUILayout.EndHorizontal();
            }

            int working = 0, blocked = 0;
            foreach (var b in Sim.Production.BuildingsAt(Loc))
                if (b.Status == Core.State.BuildingStatus.Working) working++;
                else if (b.Status != Core.State.BuildingStatus.Paused) blocked++;
            GUILayout.Label("Facilities", Theme.Header);
            GUILayout.Label($"{working} working, {Gx.Colored(blocked + " need attention", blocked > 0 ? Theme.Warn : Theme.Dim)}", Theme.Label);

            GUILayout.Label("Research", Theme.Header);
            var tech = Sim.Db.Tech(Sim.State.Research.ActiveTech);
            if (tech != null) Gx.LayoutBar((float)Sim.Research.Fraction, Theme.Cyan, tech.Name, 360, 16);
            else GUILayout.Label(Gx.Colored("No active research. Open the Research tree.", Theme.Warn), Theme.Label);

            GUILayout.Label("Missions in flight", Theme.Header);
            if (Sim.State.ActiveMissions.Count == 0) GUILayout.Label("None", Theme.DimLabel);
            foreach (var m in Sim.State.ActiveMissions)
            {
                var md = Sim.Db.Mission(m.MissionId);
                Gx.LayoutBar((float)(m.Elapsed / m.Duration), Theme.Cyan, md?.Name + "  ·  " + NumberFormat.Duration(m.Duration - m.Elapsed), 360, 16);
            }

            GUILayout.Label("Log", Theme.Header);
            foreach (var t in UI.Log.Take(14)) GUILayout.Label(Gx.Colored("• ", Gx.LevelColor(t.Level)) + t.Text, Theme.Small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
