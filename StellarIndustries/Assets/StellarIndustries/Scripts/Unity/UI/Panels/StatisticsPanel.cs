using System.Linq;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class StatisticsPanel : Panel
    {
        public StatisticsPanel(GameUI ui) : base(ui) { }
        public override string Title => "Statistics";

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            Heading("Statistics & Achievements");
            Scroll = GUILayout.BeginScrollView(Scroll);
            var s = Sim.State.Stats;
            void Row(string k, string v) { GUILayout.BeginHorizontal(); GUILayout.Label(k, Theme.Label, GUILayout.Width(220)); GUILayout.Label(Gx.Colored(v, Theme.Cyan), Theme.Label); GUILayout.EndHorizontal(); }
            Row("Play time", NumberFormat.Duration(Sim.State.PlayTime));
            Row("Offline time simulated", NumberFormat.Duration(s.OfflineSeconds));
            Row("Facilities", Sim.State.Buildings.Count + " (" + s.BuildingsBuilt + " built, " + s.Upgrades + " upgrades)");
            Row("Technologies", Sim.State.Research.Completed.Count + " / " + Sim.Db.Techs.Count);
            Row("Rockets launched", s.RocketsLaunched.ToString());
            Row("Missions", s.MissionsSucceeded + " succeeded, " + s.MissionsFailed + " failed");
            Row("Worlds", Sim.State.UnlockedLocations.Count + " / " + Sim.Db.Locations.Count);
            Row("Manual actions", s.ManualActions.ToString());

            GUILayout.Label("Achievements", Theme.Header);
            foreach (var a in Sim.Db.Achievements)
            {
                bool done = Sim.Achievements.IsDone(a);
                GUILayout.BeginHorizontal();
                GUILayout.Label((done ? Gx.Colored("[x] ", Theme.Good) : "[ ] ") + Gx.Colored(a.Name, done ? Theme.Good : Theme.Text), Theme.Label, GUILayout.Width(220));
                GUILayout.Label(a.Description, Theme.DimLabel, GUILayout.Width(250));
                if (!done) Gx.LayoutBar((float)(Sim.Achievements.Progress(a) / a.Threshold), Theme.Orange, null, 120, 10);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Total production", Theme.Header);
            foreach (var kv in s.Produced.OrderByDescending(k => k.Value).Take(30))
            {
                GUILayout.BeginHorizontal();
                Gx.Swatch(Fmt.ResColor(Sim.Db, kv.Key));
                GUILayout.Label(Sim.Db.ResourceName(kv.Key), Theme.Label, GUILayout.Width(200));
                GUILayout.Label(NumberFormat.Short(kv.Value), Theme.Label);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
