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
            Scroll = GUILayout.BeginScrollView(Scroll);
            foreach (var rocket in Sim.Db.Rockets)
            {
                var recipe = Sim.Db.Recipes.FirstOrDefault(x => x.Outputs.Any(o => o.ResourceId == rocket.VehicleResourceId));
                bool unlocked = recipe == null || Sim.Construction.IsUnlocked(recipe);
                GUILayout.BeginVertical(Theme.Box);
                GUILayout.BeginHorizontal();
                GUILayout.Label(Gx.Colored(rocket.Name, unlocked ? Theme.Cyan : Theme.Dim) + "  " + Gx.Colored(rocket.Category.ToString(), Theme.Dim), Theme.Label);
                GUILayout.FlexibleSpace();
                var fleet = Sim.State.UnlockedLocations.Select(l => (l, n: Sim.Inventory.Get(l, rocket.VehicleResourceId))).Where(x => x.n > 0).ToList();
                GUILayout.Label(fleet.Count == 0 ? Gx.Colored("none built", Theme.Dim) : string.Join("  ", fleet.Select(f => Sim.Db.Location(f.l).Name + ": " + Gx.Colored(f.n.ToString("0"), Theme.Good))), Theme.Label);
                GUILayout.EndHorizontal();
                GUILayout.Label(rocket.Description, Theme.DimLabel);
                GUILayout.Label($"Cargo {NumberFormat.Short(rocket.CargoCapacity)}  ·  Range tier {rocket.RangeTier}  ·  Reliability {NumberFormat.Percent(System.Math.Min(1, rocket.Reliability + Sim.Modifiers.Sum(ModifierType.RocketReliability)))}" +
                                $"  ·  {(rocket.CanLand ? "Lands" : "No landing")}  ·  {(rocket.Reusable ? Gx.Colored("Reusable", Theme.Good) : "Expendable")}  ·  Fuel: {Fmt.Amounts(Sim.Db, rocket.FuelCost)}", Theme.Small);
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
