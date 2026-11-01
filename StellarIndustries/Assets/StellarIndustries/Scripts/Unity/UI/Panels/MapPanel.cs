using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Systems;
using StellarIndustries.Core.Util;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    /// <summary>Solar system map and the nearby-stars view (real star systems with confirmed exoplanets).</summary>
    public class MapPanel : Panel
    {
        int tab;
        public MapPanel(GameUI ui) : base(ui) { }
        public override string Title => "Galaxy Map";
        public override string LockedHint => Sim.State.HasFeature(Features.Map) ? null : "Complete an Orbital Launch";
        public override float PreferredWidth => 380;
        public override SpaceView.ViewMode ViewMode => tab == 1 ? SpaceView.ViewMode.Galaxy : SpaceView.ViewMode.Map;

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sol System", tab == 0 ? Theme.NavActive : Theme.Nav)) { tab = 0; Gx.Changed(); }
            if (GUILayout.Button("Nearby Stars", tab == 1 ? Theme.NavActive : Theme.Nav)) { tab = 1; Gx.Changed(); }
            GUILayout.EndHorizontal();
            Scroll = GUILayout.BeginScrollView(Scroll);
            if (tab == 0) DrawSol(); else DrawStars();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawSol()
        {
            GUILayout.Label("Click a body in the map or the list. Drag to rotate, scroll to zoom.", Theme.DimLabel);
            foreach (var def in Sim.Db.Locations.Where(l => l.StarSystem == "sol")) DrawBody(def);
        }

        void DrawStars()
        {
            GUILayout.Label("Real star systems with confirmed exoplanets. Directions are real (right ascension / declination); distances are compressed (square root).", Theme.DimLabel);
            foreach (var s in Sim.Db.StarSystems.Where(x => x.Id != "sol").OrderBy(x => x.DistanceLy))
            {
                bool sel = UI.SelectedStarSystem == s.Id;
                if (GUILayout.Button(Gx.Colored(s.Name, sel ? Theme.Orange : Theme.Cyan) + Gx.Colored("  " + s.DistanceLy.ToString("0.##") + " ly · " + s.SpectralType, Theme.Dim), sel ? Theme.NavActive : Theme.Nav))
                { UI.SelectBody("sys:" + s.Id); Gx.Changed(); }
                if (!sel) continue;
                GUILayout.Label(s.Description, Theme.Wrap);
                GUILayout.Label($"Luminosity {s.LuminositySun:0.#####} x Sun · light needs {s.DistanceLy:0.#} years to reach us", Theme.DimLabel);
                if (!string.IsNullOrEmpty(s.Notes)) GUILayout.Label(Gx.Colored("Status: ", Theme.Orange) + s.Notes, Theme.DimLabel);
                var survey = Sim.Db.Missions.FirstOrDefault(m => m.Id == "survey_" + s.Id);
                if (survey != null)
                    GUILayout.Label(Sim.State.Completions(survey.Id) > 0 ? Gx.Colored("Surveyed by laser-sail probes", Theme.Good)
                        : Gx.Colored("Not surveyed yet - research Laser Sails and fly '" + survey.Name + "'", Theme.Warn), Theme.Small);
                foreach (var def in Sim.Db.Locations.Where(l => l.StarSystem == s.Id)) DrawBody(def);
            }
            GUILayout.Label("Planet compositions are unknown to science; deposits in the game are speculative.", Theme.DimLabel);
        }

        void DrawBody(LocationDef def)
        {
            bool unlocked = Sim.State.IsUnlocked(def.Id);
            GUILayout.BeginVertical(Theme.Card);
            GUILayout.Label(Gx.Colored(def.Name, unlocked ? Theme.Cyan : Theme.Dim) + Gx.Colored("  " + def.BodyType + (string.IsNullOrEmpty(def.ParentId) ? "" : " of " + Sim.Db.Location(def.ParentId)?.Name), Theme.Dim), Theme.Label);
            if (!string.IsNullOrEmpty(def.Status)) GUILayout.Label(def.Status, Theme.Small);
            if (unlocked)
            {
                int count = Sim.State.Buildings.Count(b => b.LocationId == def.Id);
                GUILayout.Label($"{count} facilities · {NumberFormat.Short(Sim.State.Location(def.Id).EnergySupply)} MW", Theme.Small);
            }
            else
            {
                var mission = Sim.Db.Missions.FirstOrDefault(m => m.UnlockLocation == def.Id);
                GUILayout.Label("Unexplored" + (mission != null ? " - reach it with: " + mission.Name : ""), Theme.DimLabel);
            }
            GUILayout.Label($"{def.Gravity:0.00} g · {def.TemperatureC:0} °C · sunlight {SpacePhysics.SolarFactor(def) * 100:0}% of Earth surface" +
                            (def.EscapeVelocityKms > 0 ? $" · escape {def.EscapeVelocityKms:0.0} km/s" : ""), Theme.DimLabel);
            GUILayout.Label("Resources: " + string.Join(", ", def.Deposits.Select(d => Sim.Db.ResourceName(d.ResourceId))), Theme.DimLabel);
            if (def.StarSystem != "sol" && def.Hazards.Count > 0) GUILayout.Label("Hazards: " + string.Join(", ", def.Hazards), Theme.DimLabel);
            GUILayout.BeginHorizontal();
            if (def.StarSystem == "sol" && GUILayout.Button("Focus", Theme.Button, GUILayout.Width(70))) UI.View.FocusBody = def.Id;
            if (unlocked && Gx.Button("Manage", true, "Open the industries of " + def.Name, Theme.Accent, GUILayout.Width(80)))
            {
                Sim.State.CurrentLocation = def.Id;
                UI.Open<IndustriesPanel>();
                Gx.Changed();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
