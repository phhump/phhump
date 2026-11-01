using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Util;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class MapPanel : Panel
    {
        public MapPanel(GameUI ui) : base(ui) { }
        public override string Title => "Galaxy Map";
        public override string LockedHint => Sim.State.HasFeature(Features.Map) ? null : "Complete an Orbital Launch";
        public override float PreferredWidth => 360;
        public override SpaceView.ViewMode ViewMode => SpaceView.ViewMode.Map;

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            Heading("Sol System", "Click a body in the map or the list. Drag to rotate, scroll to zoom.");
            Scroll = GUILayout.BeginScrollView(Scroll);
            foreach (var def in Sim.Db.Locations)
            {
                bool unlocked = Sim.State.IsUnlocked(def.Id);
                GUILayout.BeginVertical(Theme.Box);
                GUILayout.Label(Gx.Colored(def.Name, unlocked ? Theme.Cyan : Theme.Dim) + Gx.Colored("  " + def.BodyType + (string.IsNullOrEmpty(def.ParentId) ? "" : " of " + Sim.Db.Location(def.ParentId)?.Name), Theme.Dim), Theme.Label);
                if (unlocked)
                {
                    int count = Sim.State.Buildings.Count(b => b.LocationId == def.Id);
                    var loc = Sim.State.Location(def.Id);
                    GUILayout.Label($"{count} facilities · {NumberFormat.Short(loc.EnergySupply)} MW", Theme.Small);
                }
                else
                {
                    var mission = Sim.Db.Missions.FirstOrDefault(m => m.UnlockLocation == def.Id);
                    GUILayout.Label("Unexplored" + (mission != null ? " - reach it with: " + mission.Name : ""), Theme.DimLabel);
                }
                GUILayout.Label("Resources: " + string.Join(", ", def.Deposits.Select(d => Sim.Db.ResourceName(d.ResourceId))), Theme.DimLabel);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Focus", Theme.Button, GUILayout.Width(70))) UI.View.FocusBody = def.Id;
                if (unlocked && Gx.Button("Manage", true, "Open the industries of " + def.Name, Theme.Accent, GUILayout.Width(80)))
                {
                    Sim.State.CurrentLocation = def.Id;
                    UI.Open<IndustriesPanel>();
                    Gx.Changed();
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.Label("Beyond Sol", Theme.Header);
            GUILayout.Label("Asteroid belt, outer planets and procedurally generated star systems are planned for Milestones 4-5. " +
                            "They are added as LocationDefs - no code changes required.", Theme.DimLabel);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
