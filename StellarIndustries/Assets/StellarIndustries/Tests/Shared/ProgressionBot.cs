using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.Systems;

namespace StellarIndustries.Tests
{
    /// <summary>Greedy scripted player used for balance/progression tests.</summary>
    public class ProgressionBot
    {
        readonly GameSimulation sim;
        public ProgressionBot(GameSimulation sim) { this.sim = sim; }

        struct Goal { public string Loc, Building, Recipe, Tech; public int Count; }
        static Goal G(string loc, string b, string r, int count, string tech = "") => new Goal { Loc = loc, Building = b, Recipe = r, Count = count, Tech = tech };

        static readonly Goal[] Plan =
        {
            G("earth","surface_mine","mine_copper",1), G("earth","smelter","draw_wire",1), G("earth","research_lab","research_basic",1),
            G("earth","surface_mine","mine_iron",3), G("earth","smelter","smelt_iron",2), G("earth","component_factory","make_pipe",1),
            G("earth","surface_mine","mine_copper",2), G("earth","smelter","draw_wire",2), G("earth","component_factory","make_circuit",1),
            G("earth","oil_derrick","pump_oil",1,"petroleum"), G("earth","refinery","refine_fuel",1,"petroleum"), G("earth","refinery","make_plastic",1,"petroleum"),
            G("earth","surface_mine","mine_iron",5), G("earth","smelter","smelt_iron",3),
            G("earth","rocket_assembly","",1,"basic_rocketry"), G("earth","component_factory","make_frame",1,"basic_rocketry"),
            G("earth","component_factory","make_tank",1,"basic_rocketry"), G("earth","component_factory","make_engine",1,"basic_rocketry"),
            G("earth","component_factory","make_guidance",1,"electronics_1"), G("earth","research_lab","research_circuits",1,"electronics_1"),
            G("earth","surface_mine","mine_coal",1), G("earth","smelter","make_steel",1,"steelmaking"),
            G("earth","surface_mine","mine_bauxite",2,"aluminum_processing"), G("earth","smelter","smelt_aluminum",1,"aluminum_processing"),
            G("earth","water_pump","pump_water",1), G("earth","chemical_plant","make_lox",2,"cryogenics"),
            G("earth","surface_mine","mine_iron",7), G("earth","smelter","smelt_iron",4), G("earth","surface_mine","mine_copper",3), G("earth","smelter","draw_wire",3),
            G("earth","component_factory","make_circuit",2), G("earth","research_lab","research_circuits",2,"electronics_1"), G("earth","warehouse","",1),
            G("earth","surface_mine","mine_sand",1,"silicon_processing"), G("earth","smelter","make_wafer",1,"silicon_processing"), G("earth","surface_mine","mine_coal",2),
            G("earth","research_lab","research_wafers",2,"electronics_2"), G("earth","oil_derrick","pump_oil",2,"petroleum"),
            G("earth","component_factory","make_landing",1,"lunar_navigation"), G("earth","component_factory","make_engine",2,"basic_rocketry"),
            G("earth","component_factory","make_pipe",2), G("earth","smelter","make_steel",2,"steelmaking"), G("earth","surface_mine","mine_iron",9), G("earth","smelter","smelt_iron",5),
            G("earth","surface_mine","mine_copper",4), G("earth","smelter","draw_wire",4), G("earth","component_factory","make_circuit",3),
            G("earth","warehouse","",2), G("earth","research_lab","research_wafers",4,"electronics_2"), G("earth","smelter","make_wafer",2,"silicon_processing"),
            G("earth","surface_mine","mine_sand",2,"silicon_processing"), G("earth","surface_mine","mine_bauxite",3,"aluminum_processing"), G("earth","smelter","smelt_aluminum",2,"aluminum_processing"),
            // Moon
            G("moon","solar_array","",3,"photovoltaics"), G("moon","surface_mine","mine_regolith",1), G("moon","he3_harvester","harvest_he3",1,"lunar_industry"),
            G("moon","ice_extractor","extract_ice",1,"lunar_industry"), G("moon","smelter","smelt_iron",1), G("moon","surface_mine","mine_iron",1),
            G("moon","he3_harvester","harvest_he3",2,"lunar_industry"), G("moon","solar_array","",5,"photovoltaics"),
        };

        static readonly string[] ResearchOrder =
        {
            "petroleum","electronics_1","basic_rocketry","steelmaking","advanced_mining","automation_1","cryogenics","aluminum_processing","storage_logistics",
            "silicon_processing","orbital_rocketry","photovoltaics","electronics_2","lunar_navigation","launch_automation","lunar_industry","automated_research","deep_mining","offline_operations",
        };

        int Count(string loc, string b, string r) => sim.State.Buildings.Count(x => x.LocationId == loc && x.DefId == b && (string.IsNullOrEmpty(r) || x.RecipeId == r));

        string blocked = "";

        public void Decide()
        {
            blocked = "";
            // Active play: a few manual clicks while the economy is tiny.
            if (sim.State.Buildings.Count < 14)
                for (int i = 0; i < 3; i++) { sim.Construction.Gather("earth", "iron_ore"); sim.Construction.Gather("earth", "copper_ore"); }
            if (sim.State.Buildings.Count < 10 && sim.Inventory.Get("earth", "iron_ore") >= 2) sim.Construction.ManualCraft("earth", "smelt_iron");

            // Research
            if (string.IsNullOrEmpty(sim.State.Research.ActiveTech) && sim.State.Research.Queue.Count == 0)
            {
                var next = ResearchOrder.FirstOrDefault(id => sim.Research.Status(id) == TechStatus.Available);
                if (next != null) sim.Research.Start(next);
            }

            // Power first
            foreach (var loc in sim.State.UnlockedLocations)
            {
                var ls = sim.State.Location(loc);
                if (ls.EnergyDemand > ls.EnergySupply * 0.9)
                {
                    var power = loc == "earth" ? "wind_turbine" : "solar_array";
                    sim.Construction.Build(power, loc);
                }
            }

            // Rocket assembly target
            string rocketRecipe = null;
            if (sim.State.Completions("suborbital_test") == 0) rocketRecipe = "assemble_sounding";
            else if (sim.Construction.TechDone("orbital_rocketry") && sim.State.Completions("orbital_launch") == 0) rocketRecipe = "assemble_orbital";
            else if (sim.Construction.TechDone("lunar_navigation")) rocketRecipe = "assemble_lunar";
            foreach (var b in sim.State.Buildings.Where(b => b.DefId == "rocket_assembly"))
                if (rocketRecipe != null) sim.Construction.SetRecipe(b.Uid, rocketRecipe);

            // Build plan: first unmet goal that is buildable
            foreach (var g in Plan)
            {
                if (!sim.State.IsUnlocked(g.Loc)) continue;
                if (!string.IsNullOrEmpty(g.Tech) && !sim.Construction.TechDone(g.Tech)) continue;
                if (Count(g.Loc, g.Building, g.Recipe) >= g.Count) continue;
                var def = sim.Db.Building(g.Building);
                if (sim.Construction.PlacementProblem(def, g.Loc) != null) continue;
                if (!string.IsNullOrEmpty(g.Recipe) && sim.Db.Recipe(g.Recipe) != null && !sim.Construction.IsUnlocked(sim.Db.Recipe(g.Recipe))) continue;
                if (sim.Construction.CanBuild(def, g.Loc, out var why)) sim.Construction.Build(g.Building, g.Loc, string.IsNullOrEmpty(g.Recipe) ? null : g.Recipe);
                else
                {
                    blocked = g.Loc + ":" + g.Building + "/" + g.Recipe + " " + why;
                    var missing = sim.Construction.BuildCost(def, g.Loc).FirstOrDefault(c => sim.Inventory.Get(g.Loc, c.ResourceId) < c.Amount);
                    if (missing != null) TryBuildProducer(g.Loc, missing.ResourceId, 0);
                }
                break; // wait for resources for this goal
            }

            // Missions
            TryLaunch("suborbital_test", "sounding", sim.State.Completions("suborbital_test") < 2);
            TryLaunch("orbital_launch", "orbital", sim.State.Completions("orbital_launch") < 1);
            if (!sim.State.IsUnlocked("moon")) TryLaunch("lunar_landing", "lunar", true);
            else if (sim.State.Buildings.Count(b => b.LocationId == "moon") < 6 || sim.Inventory.Get("moon", "copper_wire") < 20)
            {
                // Supply the moon with what local industry cannot produce yet.
                foreach (var res in new[] { "silicon_wafer", "circuit", "copper_wire", "iron_plate" })
                {
                    double have = sim.Inventory.Get("earth", res);
                    if (have >= 30 && sim.Inventory.Get("moon", res) < 40) { TryLaunch("lunar_supply", "lunar", true, res, Math.Min(60, have)); break; }
                }
            }
        }

        /// <summary>Bottleneck solver: build a producer for a missing resource (recursing into its own missing inputs).</summary>
        bool TryBuildProducer(string loc, string res, int depth)
        {
            if (depth > 2) return false;
            foreach (var bdef in sim.Db.Buildings)
            {
                if (sim.Construction.PlacementProblem(bdef, loc) != null) continue;
                foreach (var r in sim.Construction.ValidRecipes(bdef, loc))
                {
                    if (!r.Outputs.Any(o => o.ResourceId == res)) continue;
                    // Not useful if the existing producers are starved of inputs: fix the input instead.
                    var starved = sim.State.Buildings.FirstOrDefault(b => b.LocationId == loc && b.RecipeId == r.Id && b.Status == StellarIndustries.Core.State.BuildingStatus.MissingInput);
                    if (starved != null)
                    {
                        var inp = r.Inputs.FirstOrDefault(i => sim.Inventory.Get(loc, i.ResourceId) < i.Amount);
                        if (inp != null) return TryBuildProducer(loc, inp.ResourceId, depth + 1);
                    }
                    if (Count(loc, bdef.Id, r.Id) >= 8) continue;
                    if (sim.Construction.CanBuild(bdef, loc, out _)) return sim.Construction.Build(bdef.Id, loc, r.Id) != null;
                    var miss = sim.Construction.BuildCost(bdef, loc).FirstOrDefault(c => sim.Inventory.Get(loc, c.ResourceId) < c.Amount);
                    if (miss != null && miss.ResourceId != res) return TryBuildProducer(loc, miss.ResourceId, depth + 1);
                    return false;
                }
            }
            return false;
        }

        void TryLaunch(string mission, string rocket, bool wanted, string cargo = "", double amount = 0)
        {
            if (!wanted || sim.State.ActiveMissions.Any(m => m.MissionId == mission)) return;
            if (sim.Missions.LaunchProblem(mission, rocket, cargo, amount) == null) sim.Missions.Launch(mission, rocket, cargo, amount);
        }

        public string Diagnostics()
        {
            var earth = sim.State.Location("earth");
            var inv = string.Join(", ", earth.Inventory.Where(k => k.Value > 0).Select(k => k.Key + "=" + k.Value.ToString("0")));
            var st = string.Join(", ", sim.State.Buildings.GroupBy(b => b.LocationId + ":" + b.DefId + "/" + b.RecipeId + "[" + b.Status + "]").Select(g => g.Key + "x" + g.Count()));
            return "Blocked goal: " + blocked + "\nResearch: active=" + sim.State.Research.ActiveTech + " done=" + string.Join(",", sim.State.Research.Completed) +
                   "\nScience=" + sim.Inventory.Get("earth", "science").ToString("0") + "\nEarth: " + inv + "\nPower " + earth.EnergySupply + "/" + earth.EnergyDemand + "\nBuildings: " + st;
        }
    }
}
