using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class SpacecraftPanel : Panel
    {
        public SpacecraftPanel(GameUI ui) : base(ui) { }
        public override string Title => "Spacecraft";
        public override string LockedHint => Sim.State.HasFeature(Features.Spacecraft) ? null : "Research Basic Rocketry";

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            Heading("Spacecraft", "Rockets are assembled from components in a Rocket Assembly Building, then launched from the Missions screen.");
            GUILayout.Label(new GUIContent(Gx.Colored("Rocket equation: ", Theme.Orange) + "delta-v = Isp · g0 · ln(m_full / m_empty). Each stage is computed separately; spent stages are dropped. " +
                "Payload counts as dead mass for every stage, so heavier cargo means less delta-v.",
                "Konstantin Tsiolkovsky, 1903. g0 = 9.81 m/s². Isp (specific impulse) is the engine's efficiency: kerosene/LOX ~300 s, hydrogen/LOX ~450 s."), Theme.DimLabel);
            Scroll = GUILayout.BeginScrollView(Scroll);
            foreach (var rocket in Sim.Db.Rockets)
            {
                var recipe = Sim.Db.Recipes.FirstOrDefault(x => x.Outputs.Any(o => o.ResourceId == rocket.VehicleResourceId));
                bool unlocked = recipe == null || Sim.Construction.IsUnlocked(recipe);
                GUILayout.BeginVertical(Theme.Card);
                GUILayout.BeginHorizontal();
                GUILayout.Label(Gx.Colored(rocket.Name, unlocked ? Theme.Cyan : Theme.Dim) + "  " + Gx.Colored(rocket.Category.ToString(), Theme.Dim), Theme.Label);
                GUILayout.FlexibleSpace();
                var fleet = Sim.State.UnlockedLocations.Select(l => (l, n: Sim.Inventory.Get(l, rocket.VehicleResourceId))).Where(x => x.n > 0).ToList();
                GUILayout.Label(fleet.Count == 0 ? Gx.Colored("none built", Theme.Dim) : string.Join("  ", fleet.Select(f => Sim.Db.Location(f.l).Name + ": " + Gx.Colored(f.n.ToString("0"), Theme.Good))), Theme.Label);
                GUILayout.EndHorizontal();
                GUILayout.Label(rocket.Description, Theme.DimLabel);
                GUILayout.Label($"Cargo bay {NumberFormat.Short(rocket.CargoCapacity)} units  ·  Reliability {NumberFormat.Percent(Sim.Missions.Reliability(rocket))} ({Sim.State.Flights(rocket.Id)} flights)" +
                                $"  ·  {(rocket.CanLand ? "Lands" : "No landing")}  ·  {(rocket.Reusable ? Gx.Colored("Reusable", Theme.Good) : "Expendable")}", Theme.Small);
                GUILayout.Label("Propellant per launch: " + Fmt.Amounts(Sim.Db, Core.Systems.SpacePhysics.Propellant(rocket)) + Gx.Colored("  (1 unit = 10 t)", Theme.Dim), Theme.Small);
                if (Core.Systems.SpacePhysics.HasPhysics(rocket))
                {
                    GUILayout.Label($"Lift-off mass {NumberFormat.Short(Core.Systems.SpacePhysics.TotalMassT(rocket))} t  ·  delta-v empty {Core.Systems.SpacePhysics.DeltaV(rocket, 0):0.0} km/s", Theme.Small);
                    foreach (var st in rocket.Stages)
                        GUILayout.Label($"   {st.Name}: {st.DryMassT:0.#} t dry + {st.PropellantMassT:0.#} t {st.Propellant}, Isp {st.IspSeconds:0} s", Theme.DimLabel);
                    var targets = Sim.Db.Missions.Where(m => m.DeltaVKms > 0 && Sim.Missions.RocketSuitable(m, rocket)).GroupBy(m => m.DeltaVKms).Select(g => g.First());
                    var payloads = targets.Select(m => m.Name + ": " + Core.Systems.SpacePhysics.MaxPayloadT(rocket, m.DeltaVKms).ToString("0.#") + " t").ToList();
                    if (payloads.Count > 0) GUILayout.Label("Max payload: " + string.Join("  ·  ", payloads), Theme.Small);
                }
                if (recipe != null)
                {
                    GUILayout.Label("Assembly: " + Fmt.Cost(Sim, Loc, recipe.Inputs) + "  (" + recipe.Duration + "s)", Theme.Small);
                    if (!unlocked) GUILayout.Label(Gx.Colored("Requires research: " + Sim.Db.Tech(recipe.RequiredTech)?.Name, Theme.Bad), Theme.Small);
                    else
                    {
                        var pads = Sim.Production.BuildingsAt(Loc).Where(b => sim_IsAssembly(b.DefId, recipe.Id)).ToList();
                        GUILayout.BeginHorizontal();
                        if (pads.Count == 0) GUILayout.Label(Gx.Colored("Build a Rocket Assembly Building at " + Sim.Db.Location(Loc).Name + " to produce this rocket.", Theme.Warn), Theme.Small);
                        else
                        {
                            int using_ = pads.Count(p => p.RecipeId == recipe.Id);
                            GUILayout.Label(using_ + " of " + pads.Count + " assembly buildings here produce this rocket", Theme.Small);
                            var free = pads.FirstOrDefault(p => p.RecipeId != recipe.Id);
                            if (free != null && Gx.Button("Assign one", true, "Switch an assembly building to this rocket", Theme.Button, GUILayout.Width(100)))
                                Sim.Construction.SetRecipe(free.Uid, recipe.Id);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        bool sim_IsAssembly(string defId, string recipeId) => Sim.Db.Building(defId)?.Recipes.Contains(recipeId) ?? false;
    }
}
