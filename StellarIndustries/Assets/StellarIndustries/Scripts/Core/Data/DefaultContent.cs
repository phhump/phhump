using System.Collections.Generic;

namespace StellarIndustries.Core.Data
{
    /// <summary>
    /// Built-in content (Milestones 1-2 plus a first Mars step). Used when no ContentDatabase
    /// asset exists, and as the source for the editor exporter that converts everything to
    /// ScriptableObjects. Adding content = adding entries here or adding assets in Unity.
    /// </summary>
    public static class DefaultContent
    {
        static List<ResourceAmount> R(params object[] pairs)
        {
            var l = new List<ResourceAmount>();
            for (int i = 0; i < pairs.Length; i += 2) l.Add(new ResourceAmount((string)pairs[i], System.Convert.ToDouble(pairs[i + 1])));
            return l;
        }
        static List<string> L(params string[] s) => new List<string>(s);

        public static GameDatabase Create()
        {
            var db = new GameDatabase();
            AddResources(db);
            AddRecipes(db);
            AddBuildings(db);
            AddTechs(db);
            AddRockets(db);
            AddLocations(db);
            AddMissions(db);
            AddAchievements(db);
            db.Start = new StartDef
            {
                StartLocation = "earth",
                Resources = R("iron_plate", 50, "copper_wire", 20, "iron_ore", 10),
                Buildings = new List<StartingBuilding>
                {
                    new StartingBuilding { BuildingId = "surface_mine", RecipeId = "mine_iron", LocationId = "earth" },
                    new StartingBuilding { BuildingId = "smelter", RecipeId = "smelt_iron", LocationId = "earth" },
                    new StartingBuilding { BuildingId = "wind_turbine", LocationId = "earth" },
                },
            };
            return db;
        }

        static void Res(GameDatabase db, string id, string name, ResourceCategory c, string color, string desc, bool global = false)
            => db.Add(new ResourceDef { Id = id, Name = name, Category = c, ColorHex = color, Description = desc, IsGlobal = global });

        static void AddResources(GameDatabase db)
        {
            var raw = ResourceCategory.Raw; var mat = ResourceCategory.Material; var comp = ResourceCategory.Component;
            var fuel = ResourceCategory.Fuel; var veh = ResourceCategory.Vehicle;
            Res(db, "iron_ore", "Iron Ore", raw, "#A0522D", "Common ore found on most rocky bodies.");
            Res(db, "copper_ore", "Copper Ore", raw, "#D2691E", "Conductive metal ore.");
            Res(db, "bauxite", "Bauxite", raw, "#C08A60", "Aluminum-bearing ore.");
            Res(db, "quartz_sand", "Quartz Sand", raw, "#E8DCA8", "Source of silicon.");
            Res(db, "coal", "Coal", raw, "#3A3A3A", "Fuel for early power plants and steelmaking.");
            Res(db, "crude_oil", "Crude Oil", raw, "#2B2B40", "Refined into rocket fuel and plastics.");
            Res(db, "water", "Water", raw, "#3FA9F5", "Electrolysed into oxygen and hydrogen.");
            Res(db, "regolith", "Regolith", raw, "#9A9A9A", "Lunar soil, processed into useful minerals.");
            Res(db, "titanium_ore", "Titanium Ore", raw, "#B0C4DE", "Ilmenite, rich in lunar maria.");
            Res(db, "helium3", "Helium-3", raw, "#E0F7FF", "Rare isotope implanted by the solar wind (~10 ppb in lunar regolith). Fusion fuel. 1 unit = 1 kg.");
            db.Resource("helium3").MassPerUnitT = 0.001;
            Res(db, "water_ice", "Water Ice", raw, "#BFEFFF", "Frozen water from shadowed craters and polar caps.");
            Res(db, "rare_earths", "Rare Earths", raw, "#9B59B6", "Elements essential for advanced electronics.");
            Res(db, "co2", "Carbon Dioxide", raw, "#C9A27E", "Harvested from the Martian atmosphere.");

            Res(db, "iron_plate", "Iron Plate", mat, "#B7B7B7", "Basic structural material.");
            Res(db, "copper_wire", "Copper Wire", mat, "#E8A33D", "Used in all electronics.");
            Res(db, "steel", "Steel", mat, "#7F8C8D", "Strong alloy for heavy structures.");
            Res(db, "aluminum_sheet", "Aluminum Sheet", mat, "#D6E4F0", "Lightweight aerospace material.");
            Res(db, "silicon_wafer", "Silicon Wafer", mat, "#5D6D7E", "Base for photovoltaics and chips.");
            Res(db, "plastic", "Plastic", mat, "#F5F5DC", "Insulation and housings.");
            Res(db, "titanium_alloy", "Titanium Alloy", mat, "#AEB6BF", "High-performance spacecraft alloy.");

            Res(db, "rocket_fuel", "Rocket Fuel (RP-1)", fuel, "#FF8C00", "Refined kerosene.");
            Res(db, "liquid_oxygen", "Liquid Oxygen", fuel, "#7FDBFF", "Cryogenic oxidizer.");
            Res(db, "hydrogen", "Liquid Hydrogen", fuel, "#E6F7FF", "High-efficiency fuel produced from water.");

            Res(db, "pipe", "Pipe", comp, "#95A5A6", "Plumbing for engines and tanks.");
            Res(db, "circuit", "Circuit Board", comp, "#27AE60", "Basic electronics.");
            Res(db, "structural_frame", "Structural Frame", comp, "#BDC3C7", "Rocket body segment.");
            Res(db, "fuel_tank", "Fuel Tank", comp, "#E67E22", "Propellant tank.");
            Res(db, "rocket_engine", "Rocket Engine", comp, "#E74C3C", "Liquid-fuel engine.");
            Res(db, "guidance_computer", "Guidance Computer", comp, "#1ABC9C", "Navigation and control.");
            Res(db, "landing_system", "Landing System", comp, "#F1C40F", "Legs, radar and throttling for soft landings.");

            Res(db, "sounding_rocket", "Sounding Rocket", veh, "#FFFFFF", "Small suborbital rocket.");
            Res(db, "orbital_rocket", "Orbital Rocket", veh, "#FFFFFF", "Two-stage orbital launcher.");
            Res(db, "lunar_rocket", "Lunar Rocket", veh, "#FFFFFF", "Heavy rocket with lander.");
            Res(db, "cargo_rocket", "Reusable Cargo Rocket", veh, "#FFFFFF", "Reusable heavy-lift freighter.");
            Res(db, "lunar_shuttle", "Lunar Shuttle", veh, "#FFFFFF", "Built on the Moon from local materials.");
            Res(db, "mars_transfer", "Mars Transfer Vehicle", veh, "#FFFFFF", "Interplanetary transport.");

            Res(db, "science", "Science", ResourceCategory.Science, "#00E5FF", "Research points shared by your whole empire.", true);
        }

        static void Rec(GameDatabase db, string id, string name, List<ResourceAmount> inp, List<ResourceAmount> outp, double dur,
            string tech = "", string deposit = "", bool manual = false, string atmosphere = "")
            => db.Add(new RecipeDef { Id = id, Name = name, Inputs = inp, Outputs = outp, Duration = dur, RequiredTech = tech, RequiredDeposit = deposit, ManualCraftable = manual, RequiredAtmosphere = atmosphere });

        static void AddRecipes(GameDatabase db)
        {
            var none = new List<ResourceAmount>();
            // Extraction (no inputs, needs a deposit, speed scales with richness)
            Rec(db, "mine_iron", "Mine Iron Ore", none, R("iron_ore", 1), 2, deposit: "iron_ore");
            Rec(db, "mine_copper", "Mine Copper Ore", none, R("copper_ore", 1), 2, deposit: "copper_ore");
            Rec(db, "mine_sand", "Quarry Quartz Sand", none, R("quartz_sand", 1), 2, deposit: "quartz_sand");
            Rec(db, "mine_coal", "Mine Coal", none, R("coal", 1), 2, deposit: "coal");
            Rec(db, "mine_bauxite", "Mine Bauxite", none, R("bauxite", 1), 3, "aluminum_processing", "bauxite");
            Rec(db, "mine_regolith", "Excavate Regolith", none, R("regolith", 2), 2, deposit: "regolith");
            Rec(db, "mine_titanium", "Mine Titanium Ore", none, R("titanium_ore", 1), 3, "lunar_industry", "titanium_ore");
            Rec(db, "mine_rare_earths", "Mine Rare Earths", none, R("rare_earths", 1), 5, "lunar_industry", "rare_earths");
            Rec(db, "pump_oil", "Pump Crude Oil", none, R("crude_oil", 1), 2, deposit: "crude_oil");
            Rec(db, "pump_water", "Pump Water", none, R("water", 2), 2, deposit: "water");
            Rec(db, "extract_ice", "Extract Water Ice", none, R("water_ice", 1), 2, deposit: "water_ice");
            Rec(db, "harvest_he3", "Harvest Helium-3", none, R("helium3", 1), 4, deposit: "helium3");
            Rec(db, "extract_co2", "Compress Atmospheric CO2", none, R("co2", 2), 2, deposit: "co2");

            // Smelting / processing
            Rec(db, "smelt_iron", "Smelt Iron Plate", R("iron_ore", 2), R("iron_plate", 1), 2, manual: true);
            Rec(db, "draw_wire", "Draw Copper Wire", R("copper_ore", 1), R("copper_wire", 2), 2, manual: true);
            Rec(db, "make_steel", "Make Steel", R("iron_plate", 3, "coal", 1), R("steel", 1), 5, "steelmaking");
            Rec(db, "smelt_aluminum", "Smelt Aluminum", R("bauxite", 3), R("aluminum_sheet", 1), 4, "aluminum_processing");
            Rec(db, "make_wafer", "Grow Silicon Wafer", R("quartz_sand", 2, "coal", 1), R("silicon_wafer", 1), 5, "silicon_processing");
            Rec(db, "smelt_titanium", "Smelt Titanium Alloy", R("titanium_ore", 3, "aluminum_sheet", 1), R("titanium_alloy", 1), 6, "titanium_metallurgy");
            Rec(db, "refine_fuel", "Refine RP-1 Fuel", R("crude_oil", 2), R("rocket_fuel", 2), 4, "petroleum");
            Rec(db, "make_plastic", "Produce Plastic", R("crude_oil", 2), R("plastic", 1), 4, "petroleum");
            // Real LOX is made by cryogenic air separation (Linde process) - only power needed, but only where the air contains oxygen.
            Rec(db, "make_lox", "Air Separation (LOX)", new List<ResourceAmount>(), R("liquid_oxygen", 1), 2, "cryogenics", atmosphere: "Oxygen");
            Rec(db, "electrolysis", "Water Electrolysis", R("water", 2), R("liquid_oxygen", 1, "hydrogen", 2), 4, "cryogenics");
            Rec(db, "melt_ice", "Melt Ice", R("water_ice", 1), R("water", 2), 2, "lunar_industry");
            Rec(db, "process_regolith", "Process Regolith", R("regolith", 5), R("iron_ore", 1, "quartz_sand", 1, "bauxite", 1), 4, "lunar_industry");
            Rec(db, "co2_electrolysis", "CO2 Electrolysis (MOXIE)", R("co2", 2), R("liquid_oxygen", 1), 3, "mars_industry");

            // Components
            Rec(db, "make_pipe", "Make Pipe", R("iron_plate", 1), R("pipe", 1), 2, manual: true);
            Rec(db, "make_circuit", "Make Circuit Board", R("iron_plate", 1, "copper_wire", 3), R("circuit", 1), 4, manual: true);
            Rec(db, "make_frame", "Make Structural Frame", R("iron_plate", 4), R("structural_frame", 1), 6, "basic_rocketry");
            Rec(db, "make_tank", "Make Fuel Tank", R("iron_plate", 3, "pipe", 2), R("fuel_tank", 1), 6, "basic_rocketry");
            Rec(db, "make_engine", "Make Rocket Engine", R("iron_plate", 3, "pipe", 4, "circuit", 2), R("rocket_engine", 1), 10, "basic_rocketry");
            Rec(db, "make_guidance", "Make Guidance Computer", R("circuit", 4, "plastic", 1), R("guidance_computer", 1), 10, "electronics_1");
            Rec(db, "make_landing", "Make Landing System", R("steel", 4, "circuit", 3, "aluminum_sheet", 2), R("landing_system", 1), 12, "lunar_navigation");

            // Rocket assembly
            Rec(db, "assemble_sounding", "Assemble Sounding Rocket",
                R("rocket_engine", 1, "fuel_tank", 1, "structural_frame", 1, "guidance_computer", 1), R("sounding_rocket", 1), 20, "basic_rocketry");
            Rec(db, "assemble_orbital", "Assemble Orbital Rocket",
                R("rocket_engine", 3, "fuel_tank", 3, "structural_frame", 2, "guidance_computer", 1, "aluminum_sheet", 10), R("orbital_rocket", 1), 30, "orbital_rocketry");
            Rec(db, "assemble_lunar", "Assemble Lunar Rocket",
                R("rocket_engine", 6, "fuel_tank", 6, "structural_frame", 4, "guidance_computer", 2, "landing_system", 2, "aluminum_sheet", 20, "steel", 10), R("lunar_rocket", 1), 60, "lunar_navigation");
            Rec(db, "assemble_cargo", "Assemble Reusable Cargo Rocket",
                R("rocket_engine", 9, "fuel_tank", 8, "structural_frame", 6, "guidance_computer", 3, "landing_system", 2, "steel", 30, "aluminum_sheet", 30), R("cargo_rocket", 1), 90, "heavy_lift");
            Rec(db, "assemble_shuttle", "Assemble Lunar Shuttle",
                R("rocket_engine", 2, "titanium_alloy", 4, "landing_system", 2, "guidance_computer", 1), R("lunar_shuttle", 1), 60, "titanium_metallurgy");
            Rec(db, "assemble_mars", "Assemble Mars Transfer Vehicle",
                R("rocket_engine", 12, "fuel_tank", 12, "structural_frame", 8, "guidance_computer", 4, "landing_system", 4, "titanium_alloy", 40, "steel", 20), R("mars_transfer", 1), 120, "interplanetary_navigation");

            // Research
            Rec(db, "research_basic", "Basic Experiments", R("iron_plate", 1, "copper_wire", 2), R("science", 1), 3);
            Rec(db, "research_circuits", "Electronics Experiments", R("circuit", 1), R("science", 4), 4, "electronics_1");
            Rec(db, "research_wafers", "Semiconductor Research", R("silicon_wafer", 1, "circuit", 1), R("science", 10), 5, "electronics_2");
            Rec(db, "research_lunar", "Lunar Geology", R("rare_earths", 1, "regolith", 4), R("science", 14), 6, "lunar_industry");

            // Power plant fuel recipes
            Rec(db, "burn_coal", "Burn Coal", R("coal", 1), new List<ResourceAmount>(), 4);
            Rec(db, "fuse_he3", "Fuse Helium-3", R("helium3", 1), new List<ResourceAmount>(), 20, "fusion_power");
        }

        static BuildingDef B(string id, string name, BuildingCategory cat, List<ResourceAmount> cost, string desc)
            => new BuildingDef { Id = id, Name = name, Category = cat, BuildCost = cost, Description = desc };

        static void AddBuildings(GameDatabase db)
        {
            var mine = B("surface_mine", "Surface Mine", BuildingCategory.Extraction, R("iron_plate", 12), "Extracts surface ore deposits. Select which deposit to mine.");
            mine.Recipes = L("mine_iron", "mine_copper", "mine_sand", "mine_coal", "mine_bauxite", "mine_regolith", "mine_titanium", "mine_rare_earths");
            mine.PowerUse = 2; mine.ColorHex = "#C0794A"; mine.Shape = "tower";
            db.Add(mine);

            var oil = B("oil_derrick", "Oil Derrick", BuildingCategory.Extraction, R("iron_plate", 20, "pipe", 6), "Pumps crude oil.");
            oil.Recipes = L("pump_oil"); oil.PowerUse = 3; oil.RequiredTech = "petroleum"; oil.ColorHex = "#444466"; oil.Shape = "tower";
            db.Add(oil);

            var pump = B("water_pump", "Water Pump", BuildingCategory.Extraction, R("iron_plate", 10, "copper_wire", 5), "Pumps groundwater.");
            pump.Recipes = L("pump_water"); pump.PowerUse = 1; pump.ColorHex = "#3FA9F5"; pump.Shape = "cylinder";
            db.Add(pump);

            var ice = B("ice_extractor", "Ice Extractor", BuildingCategory.Extraction, R("iron_plate", 15, "circuit", 6), "Heats and extracts frozen volatiles.");
            ice.Recipes = L("extract_ice"); ice.PowerUse = 3; ice.RequiredTech = "lunar_industry"; ice.ColorHex = "#BFEFFF"; ice.Shape = "cylinder";
            db.Add(ice);

            var he3 = B("he3_harvester", "Helium-3 Harvester", BuildingCategory.Extraction, R("iron_plate", 25, "circuit", 10), "Sifts huge amounts of regolith for helium-3.");
            he3.Recipes = L("harvest_he3"); he3.PowerUse = 6; he3.RequiredTech = "lunar_industry"; he3.ColorHex = "#E0F7FF"; he3.Shape = "dome";
            db.Add(he3);

            var atmo = B("atmo_processor", "Atmospheric Processor", BuildingCategory.Extraction, R("steel", 20, "circuit", 10, "pipe", 10), "Compresses atmospheric gases.");
            atmo.Recipes = L("extract_co2"); atmo.PowerUse = 5; atmo.RequiredTech = "mars_industry"; atmo.ColorHex = "#C9A27E"; atmo.Shape = "tower";
            db.Add(atmo);

            var smelter = B("smelter", "Smelter", BuildingCategory.Processing, R("iron_plate", 15), "Turns ore into metals.");
            smelter.Recipes = L("smelt_iron", "draw_wire", "make_steel", "smelt_aluminum", "make_wafer", "smelt_titanium");
            smelter.PowerUse = 3; smelter.ColorHex = "#E67E22"; smelter.Shape = "cube";
            db.Add(smelter);

            var refinery = B("refinery", "Refinery", BuildingCategory.Processing, R("iron_plate", 30, "pipe", 10, "copper_wire", 5), "Refines crude oil.");
            refinery.Recipes = L("refine_fuel", "make_plastic"); refinery.PowerUse = 4; refinery.RequiredTech = "petroleum"; refinery.ColorHex = "#8E44AD"; refinery.Shape = "cylinder";
            db.Add(refinery);

            var chem = B("chemical_plant", "Chemical Plant", BuildingCategory.Processing, R("iron_plate", 30, "pipe", 12, "circuit", 6), "Cryogenics, electrolysis and mineral processing.");
            chem.Recipes = L("make_lox", "electrolysis", "melt_ice", "process_regolith", "co2_electrolysis");
            chem.PowerUse = 4; chem.RequiredTech = "cryogenics"; chem.ColorHex = "#16A085"; chem.Shape = "dome";
            db.Add(chem);

            var factory = B("component_factory", "Component Factory", BuildingCategory.Manufacturing, R("iron_plate", 20, "copper_wire", 10), "Manufactures parts and electronics.");
            factory.Recipes = L("make_pipe", "make_circuit", "make_frame", "make_tank", "make_engine", "make_guidance", "make_landing");
            factory.PowerUse = 3; factory.ColorHex = "#2980B9"; factory.Shape = "cube";
            db.Add(factory);

            var asm = B("rocket_assembly", "Rocket Assembly Building", BuildingCategory.Assembly, R("iron_plate", 50, "pipe", 20, "circuit", 10), "Assembles complete launch vehicles.");
            asm.Recipes = L("assemble_sounding", "assemble_orbital", "assemble_lunar", "assemble_cargo", "assemble_shuttle", "assemble_mars");
            asm.PowerUse = 6; asm.RequiredTech = "basic_rocketry"; asm.ColorHex = "#ECF0F1"; asm.Shape = "pad"; asm.CostScaling = 1.6;
            db.Add(asm);

            var lab = B("research_lab", "Research Laboratory", BuildingCategory.Research, R("iron_plate", 15, "copper_wire", 10), "Generates science from experiments.");
            lab.Recipes = L("research_basic", "research_circuits", "research_wafers", "research_lunar");
            lab.PowerUse = 2; lab.ColorHex = "#00E5FF"; lab.Shape = "dome";
            db.Add(lab);

            var wind = B("wind_turbine", "Wind Turbine", BuildingCategory.Power, R("iron_plate", 10, "copper_wire", 5), "Clean power. Requires an atmosphere with wind.");
            wind.PowerSource = PowerSource.Wind; wind.PowerOutput = 6; wind.ColorHex = "#FFFFFF"; wind.Shape = "tower";
            db.Add(wind);

            var coal = B("coal_plant", "Coal Power Plant", BuildingCategory.Power, R("iron_plate", 25, "copper_wire", 10, "pipe", 5), "Burns coal for steady power. Needs an oxygen atmosphere.");
            coal.PowerSource = PowerSource.Fuel; coal.PowerOutput = 20; coal.Recipes = L("burn_coal"); coal.AllowedLocations = L("earth"); coal.ColorHex = "#555555"; coal.Shape = "cylinder";
            db.Add(coal);

            var solar = B("solar_array", "Solar Array", BuildingCategory.Power, R("iron_plate", 10, "silicon_wafer", 4, "copper_wire", 6), "Output depends on sunlight at the location.");
            solar.PowerSource = PowerSource.Solar; solar.PowerOutput = 8; solar.RequiredTech = "photovoltaics"; solar.ColorHex = "#2E86C1"; solar.Shape = "panel";
            db.Add(solar);

            var fusion = B("fusion_reactor", "Fusion Reactor", BuildingCategory.Power, R("steel", 60, "circuit", 40, "titanium_alloy", 20), "Fuses helium-3. Enormous output.");
            fusion.PowerSource = PowerSource.Fuel; fusion.PowerOutput = 200; fusion.Recipes = L("fuse_he3"); fusion.RequiredTech = "fusion_power"; fusion.ColorHex = "#F4D03F"; fusion.Shape = "dome";
            db.Add(fusion);

            var wh = B("warehouse", "Warehouse", BuildingCategory.Storage, R("iron_plate", 20), "+50% storage capacity per level at this location.");
            wh.StorageBonus = 0.5; wh.CostScaling = 1.3; wh.ColorHex = "#7F8C8D"; wh.Shape = "cube";
            db.Add(wh);
        }

        static TechDef T(string id, string name, TechCategory c, int tier, double cost, string desc, params string[] prereq)
            => new TechDef { Id = id, Name = name, Category = c, Tier = tier, ScienceCost = cost, Description = desc, Prerequisites = L(prereq) };

        static void AddTechs(GameDatabase db)
        {
            db.Add(T("petroleum", "Petroleum Engineering", TechCategory.Chemistry, 1, 15, "Drill for oil and refine it into fuel and plastics."));
            db.Add(T("electronics_1", "Electronics I", TechCategory.Electronics, 1, 15, "Guidance computers and circuit-based experiments."));
            db.Add(T("steelmaking", "Steelmaking", TechCategory.Metallurgy, 1, 20, "Alloy iron with carbon."));
            var am = T("advanced_mining", "Advanced Mining", TechCategory.Mining, 1, 25, "Better drills: +25% extraction speed.");
            am.Modifiers.Add(new ModifierDef(ModifierType.ExtractionSpeed, 0.25)); am.Modifiers.Add(new ModifierDef(ModifierType.ManualYield, 1.0));
            db.Add(am);

            var br = T("basic_rocketry", "Basic Rocketry", TechCategory.Rocketry, 2, 40, "Rocket engines, tanks and the assembly building.", "petroleum", "electronics_1");
            br.UnlockFeatures = L(Features.Spacecraft, Features.Missions);
            db.Add(br);
            db.Add(T("cryogenics", "Cryogenics", TechCategory.Chemistry, 2, 40, "Liquefy oxygen. Unlocks the chemical plant.", "petroleum"));
            db.Add(T("aluminum_processing", "Aluminum Processing", TechCategory.Metallurgy, 2, 40, "Mine bauxite and smelt aluminum.", "steelmaking"));
            var a1 = T("automation_1", "Automation I", TechCategory.Automation, 2, 50, "Conveyors and robotic arms: +25% production speed.", "electronics_1");
            a1.Modifiers.Add(new ModifierDef(ModifierType.ProductionSpeed, 0.25)); db.Add(a1);
            var st = T("storage_logistics", "Storage Logistics", TechCategory.Logistics, 2, 30, "+50% storage at all locations.", "steelmaking");
            st.Modifiers.Add(new ModifierDef(ModifierType.StorageCapacity, 0.5)); db.Add(st);

            db.Add(T("silicon_processing", "Silicon Processing", TechCategory.Electronics, 3, 60, "Grow silicon wafers.", "electronics_1", "steelmaking"));
            var orb = T("orbital_rocketry", "Orbital Rocketry", TechCategory.Rocketry, 3, 120, "Two-stage rockets that reach orbit.", "basic_rocketry", "cryogenics", "aluminum_processing");
            orb.MaterialCost = R("aluminum_sheet", 10); db.Add(orb);
            var off = T("offline_operations", "Offline Operations", TechCategory.Automation, 3, 80, "Facilities keep working while you are away: +25% offline efficiency, +4h offline cap.", "automation_1");
            off.Modifiers.Add(new ModifierDef(ModifierType.OfflineEfficiency, 0.25)); off.Modifiers.Add(new ModifierDef(ModifierType.OfflineCapHours, 4)); db.Add(off);

            db.Add(T("photovoltaics", "Photovoltaics", TechCategory.Energy, 4, 100, "Solar arrays.", "silicon_processing"));
            var e2 = T("electronics_2", "Electronics II", TechCategory.Electronics, 4, 150, "Semiconductor research: +25% research speed.", "silicon_processing");
            e2.Modifiers.Add(new ModifierDef(ModifierType.ResearchSpeed, 0.25)); db.Add(e2);
            var la = T("launch_automation", "Launch Automation", TechCategory.Automation, 4, 200, "Automatic recurring launches and transport routes. +10% mission speed.", "orbital_rocketry", "automation_1");
            la.UnlockFeatures = L(Features.LaunchAutomation, Features.Logistics); la.Modifiers.Add(new ModifierDef(ModifierType.MissionSpeed, 0.1)); db.Add(la);
            var dm = T("deep_mining", "Deep Mining", TechCategory.Mining, 4, 150, "Deep shafts: +40% extraction speed.", "advanced_mining", "steelmaking");
            dm.Modifiers.Add(new ModifierDef(ModifierType.ExtractionSpeed, 0.4)); db.Add(dm);

            var ln = T("lunar_navigation", "Lunar Navigation", TechCategory.Rocketry, 5, 300, "Landing systems and lunar rockets. +5% rocket reliability.", "orbital_rocketry", "electronics_2");
            ln.Modifiers.Add(new ModifierDef(ModifierType.RocketReliability, 0.05)); ln.MaterialCost = R("guidance_computer", 2); db.Add(ln);
            var ar = T("automated_research", "Automated Research", TechCategory.ArtificialIntelligence, 5, 250, "Labs automatically pick the next research project.", "electronics_2");
            ar.UnlockFeatures = L(Features.AutoResearch); ar.Modifiers.Add(new ModifierDef(ModifierType.ResearchSpeed, 0.15)); db.Add(ar);

            db.Add(T("lunar_industry", "Lunar Industry", TechCategory.PlanetaryEngineering, 6, 400, "Ice extraction, regolith processing, helium-3 and titanium mining.", "lunar_navigation"));
            var hl = T("heavy_lift", "Heavy Lift", TechCategory.Rocketry, 6, 600, "Reusable heavy cargo rockets.", "lunar_navigation", "launch_automation");
            hl.MaterialCost = R("steel", 50); db.Add(hl);

            db.Add(T("titanium_metallurgy", "Titanium Metallurgy", TechCategory.Metallurgy, 7, 700, "Titanium alloys and the lunar shuttle.", "lunar_industry"));
            var fu = T("fusion_power", "Helium-3 Fusion", TechCategory.Fusion, 7, 3000, "Fusion reactors burning lunar helium-3. +20% energy output.", "lunar_industry", "photovoltaics");
            fu.Modifiers.Add(new ModifierDef(ModifierType.EnergyOutput, 0.2)); fu.MaterialCost = R("helium3", 20); db.Add(fu);

            var ip = T("interplanetary_navigation", "Interplanetary Navigation", TechCategory.Propulsion, 8, 6000, "Transfer orbits to Mars. +20% mission speed.", "heavy_lift", "titanium_metallurgy");
            ip.Modifiers.Add(new ModifierDef(ModifierType.MissionSpeed, 0.2)); db.Add(ip);
            var a2 = T("automation_2", "Automation II", TechCategory.Robotics, 8, 2500, "Robotic factories: +50% production, +15% offline efficiency.", "launch_automation", "automated_research");
            a2.Modifiers.Add(new ModifierDef(ModifierType.ProductionSpeed, 0.5)); a2.Modifiers.Add(new ModifierDef(ModifierType.OfflineEfficiency, 0.15)); db.Add(a2);

            var mi = T("mars_industry", "Martian Industry", TechCategory.PlanetaryEngineering, 9, 8000, "Atmospheric processing and oxygen from CO2. +20% extraction.", "interplanetary_navigation");
            mi.Modifiers.Add(new ModifierDef(ModifierType.ExtractionSpeed, 0.2)); db.Add(mi);
        }

        static RocketStage S(string n, double dry, double prop, double isp, PropellantType p) => new RocketStage(n, dry, prop, isp, p);

        // Stage masses are game designs tuned with the real rocket equation; Isp values are typical:
        // kerosene/LOX ~300-345 s, hydrogen/LOX ~420-450 s. See Docs/SCIENCE.md.
        static void AddRockets(GameDatabase db)
        {
            var K = PropellantType.Kerolox; var H = PropellantType.Hydrolox;
            db.Add(new RocketDef { Id = "sounding", Name = "Sounding Rocket", Category = RocketCategory.BasicRocket, VehicleResourceId = "sounding_rocket",
                RangeTier = 0, CargoCapacity = 0, Reliability = 0.92, FuelCost = R("rocket_fuel", 20),
                Stages = new List<RocketStage> { S("Single stage", 0.4, 1.2, 230, K) },
                Description = "Suborbital research rocket (think WAC Corporal). Uses storable propellant - no liquid oxygen needed." });
            db.Add(new RocketDef { Id = "orbital", Name = "Orbital Rocket", Category = RocketCategory.BasicRocket, VehicleResourceId = "orbital_rocket",
                RangeTier = 1, CargoCapacity = 20, Reliability = 0.9,
                Stages = new List<RocketStage> { S("Booster", 25, 400, 300, K), S("Upper stage", 4, 90, 345, K) },
                Description = "Two-stage kerosene/LOX launcher. Staging drops empty tanks so the upper stage accelerates less dead mass." });
            db.Add(new RocketDef { Id = "lunar", Name = "Lunar Rocket", Category = RocketCategory.CrewSpacecraft, VehicleResourceId = "lunar_rocket",
                RangeTier = 2, CanLand = true, CargoCapacity = 60, Reliability = 0.88,
                Stages = new List<RocketStage> { S("Kerosene booster", 130, 2100, 300, K), S("Hydrogen 2nd stage", 36, 440, 420, H), S("Transfer & lander", 8, 60, 440, H) },
                Description = "Saturn V-style: kerosene first stage, high-efficiency hydrogen upper stages, lander." });
            db.Add(new RocketDef { Id = "cargo", Name = "Reusable Cargo Rocket", Category = RocketCategory.HeavyLift, VehicleResourceId = "cargo_rocket",
                RangeTier = 2, CanLand = true, Reusable = true, CargoCapacity = 300, Reliability = 0.95, SpeedMultiplier = 1.2,
                Stages = new List<RocketStage> { S("Reusable booster", 220, 3300, 310, K), S("Hydrogen core", 70, 800, 450, H), S("Cargo lander", 14, 110, 450, H) },
                Description = "Heavy lifter whose stages land and fly again. (Recovery propellant is abstracted.)" });
            db.Add(new RocketDef { Id = "shuttle", Name = "Lunar Shuttle", Category = RocketCategory.CargoRocket, VehicleResourceId = "lunar_shuttle",
                RangeTier = 2, CanLand = true, Reusable = true, CargoCapacity = 150, Reliability = 0.96,
                Stages = new List<RocketStage> { S("Hydrolox shuttle", 8, 22, 450, H) },
                Description = "Single stage, built and fuelled on the Moon. Low lunar gravity (1/6 g) and aerobraking at Earth make this possible." });
            db.Add(new RocketDef { Id = "mars", Name = "Mars Transfer Vehicle", Category = RocketCategory.InterplanetaryTransport, VehicleResourceId = "mars_transfer",
                RangeTier = 3, CanLand = true, CargoCapacity = 300, Reliability = 0.85,
                Stages = new List<RocketStage> { S("Booster", 220, 3300, 310, K), S("Transfer stage", 80, 900, 450, H), S("Entry & lander", 16, 130, 450, H) },
                Description = "Interplanetary lander with heat shield: uses the Martian atmosphere to brake." });
        }

        static void AddLocations(GameDatabase db)
        {
            db.Add(new LocationDef { Id = "earth", Name = "Earth", BodyType = BodyType.Planet, Gravity = 1, Atmosphere = "Nitrogen / Oxygen", TemperatureC = 15,
                SolarMultiplier = 1, HasWind = true, StartUnlocked = true, ColorHex = "#2E86DE", OrbitRadius = 30, VisualRadius = 1.0, OrbitPeriod = 600,
                RadiusKm = 6371.0, EscapeVelocityKms = 11.19, DayLengthHours = 24.0, DistanceAU = 1.0, IrradianceWm2 = 1361.0, SurfaceTransmission = 0.75,
                Description = "Home. Abundant resources, a breathable atmosphere and deep gravity well.",
                Deposits = new List<DepositDef> { new DepositDef("iron_ore", 1), new DepositDef("copper_ore", 1), new DepositDef("bauxite", 0.8), new DepositDef("quartz_sand", 1),
                    new DepositDef("coal", 1), new DepositDef("crude_oil", 1), new DepositDef("water", 1.5) },
                Hazards = L("Weather") });
            db.Add(new LocationDef { Id = "moon", Name = "Moon", BodyType = BodyType.Moon, ParentId = "earth", Gravity = 0.165, Atmosphere = "None (vacuum)", TemperatureC = -20,
                SolarMultiplier = 1.4, ColorHex = "#BDBDBD", OrbitRadius = 3.5, VisualRadius = 0.35, OrbitPeriod = 90,
                RadiusKm = 1737.4, EscapeVelocityKms = 2.38, DayLengthHours = 708.7, DistanceAU = 1.0, IrradianceWm2 = 1361.0, SurfaceTransmission = 1.0,
                Description = "Low gravity makes it the perfect shipyard. Regolith, titanium, ice and helium-3.",
                Deposits = new List<DepositDef> { new DepositDef("regolith", 1.5), new DepositDef("iron_ore", 0.6), new DepositDef("titanium_ore", 0.8),
                    new DepositDef("helium3", 0.5), new DepositDef("water_ice", 0.6), new DepositDef("rare_earths", 0.4) },
                Hazards = L("Radiation", "Micrometeorites", "Two-week nights") });
            db.Add(new LocationDef { Id = "mars", Name = "Mars", BodyType = BodyType.Planet, Gravity = 0.38, Atmosphere = "Thin CO2 (~6 mbar)", TemperatureC = -65,
                SolarMultiplier = 0.45, ColorHex = "#C0392B", OrbitRadius = 46, VisualRadius = 0.55, OrbitPeriod = 1100,
                RadiusKm = 3389.5, EscapeVelocityKms = 5.03, DayLengthHours = 24.66, DistanceAU = 1.524, IrradianceWm2 = 586.2, SurfaceTransmission = 0.85,
                Description = "Iron-rich soil, polar ice and a CO2 atmosphere waiting to be processed.",
                Deposits = new List<DepositDef> { new DepositDef("iron_ore", 1.3), new DepositDef("quartz_sand", 1), new DepositDef("water_ice", 0.9),
                    new DepositDef("co2", 2), new DepositDef("rare_earths", 0.6), new DepositDef("regolith", 1) },
                Hazards = L("Dust storms", "Radiation") });
        }

        static void AddMissions(GameDatabase db)
        {
            db.Add(new MissionDef { Id = "suborbital_test", Name = "Suborbital Test Flight", Type = MissionType.Launch, Origin = "earth", Destination = "earth",
                RequiredRangeTier = 0, Duration = 30, BaseSuccess = 1, RewardScience = 20, FirstTimeRewards = R("science", 40), RequiredTech = "basic_rocketry",
                Description = "Prove your rockets fly. Gathers upper-atmosphere data.",
                DeltaVKms = 2.0, RealTransitDays = 0.01, ScienceNote = "Reaching 100 km altitude needs ~1.4 km/s plus drag and gravity losses - far less than orbit, because you only go up, not sideways." });
            db.Add(new MissionDef { Id = "orbital_launch", Name = "Orbital Launch", Type = MissionType.Launch, Origin = "earth", Destination = "earth",
                RequiredRangeTier = 1, Duration = 60, BaseSuccess = 1, RewardScience = 60, FirstTimeRewards = R("science", 100), RequiredTech = "orbital_rocketry",
                UnlockFeatures = L(Features.Map), Description = "Place a satellite in orbit and open the solar system map.",
                DeltaVKms = 9.4, RealTransitDays = 0.01, ScienceNote = "Orbit means 7.8 km/s sideways. Gravity and air drag add 1.5-2 km/s: ~9.4 km/s to reach LEO." });
            db.Add(new MissionDef { Id = "lunar_landing", Name = "Lunar Landing", Type = MissionType.Landing, Origin = "earth", Destination = "moon",
                RequiredRangeTier = 2, RequiresLanding = true, Duration = 150, BaseSuccess = 0.95, RewardScience = 120, AllowsCargo = true, RequiredTech = "lunar_navigation",
                UnlockLocation = "moon", FirstTimeRewards = R("iron_plate", 80, "circuit", 40, "copper_wire", 40, "pipe", 20, "steel", 10, "silicon_wafer", 30),
                Description = "Land on the Moon and deploy a starter base kit.",
                DeltaVKms = 15.4, RealTransitDays = 3, ScienceNote = "LEO 9.4 + trans-lunar injection 3.2 + lunar orbit insertion 0.9 + landing ~1.9 km/s. No air on the Moon: every metre per second of braking costs fuel." });
            db.Add(new MissionDef { Id = "lunar_supply", Name = "Lunar Supply Run", Type = MissionType.Cargo, Origin = "earth", Destination = "moon",
                RequiredRangeTier = 2, RequiresLanding = true, Duration = 120, BaseSuccess = 1, AllowsCargo = true, RequiredLocations = L("moon"),
                Description = "Ship cargo from Earth to your lunar base.",
                DeltaVKms = 15.4, RealTransitDays = 3, ScienceNote = "Payload costs delta-v: every tonne of cargo lowers what the rocket can achieve. Light, valuable goods ship best." });
            db.Add(new MissionDef { Id = "lunar_export", Name = "Lunar Export", Type = MissionType.Cargo, Origin = "moon", Destination = "earth",
                RequiredRangeTier = 2, RequiresLanding = true, Duration = 90, BaseSuccess = 1, AllowsCargo = true, RequiredLocations = L("moon"),
                RequiredTech = "lunar_industry", Description = "Ship lunar products (helium-3, titanium) down to Earth.",
                DeltaVKms = 2.8, RealTransitDays = 3, ScienceNote = "Leaving the Moon needs only ~2.8 km/s (escape velocity 2.38 km/s) and Earth's atmosphere brakes for free. The Moon is the natural shipyard of the inner solar system." });
            db.Add(new MissionDef { Id = "mars_landing", Name = "Mars Landing", Type = MissionType.Landing, Origin = "earth", Destination = "mars",
                RequiredRangeTier = 3, RequiresLanding = true, Duration = 300, BaseSuccess = 0.92, RewardScience = 400, AllowsCargo = true, RequiredTech = "interplanetary_navigation",
                UnlockLocation = "mars", FirstTimeRewards = R("steel", 60, "circuit", 60, "copper_wire", 60, "pipe", 30, "iron_plate", 100, "silicon_wafer", 60),
                Description = "Establish the first Martian outpost.",
                DeltaVKms = 14.7, WindowPenaltyKms = 5, SynodicPeriodSeconds = 1200, RealTransitDays = 255,
                ScienceNote = "LEO 9.4 + Mars transfer 4.3 km/s, then aerobraking and a short powered landing. Earth and Mars line up every 780 days (synodic period); outside the window the trip costs far more delta-v." });
            db.Add(new MissionDef { Id = "mars_supply", Name = "Mars Supply Run", Type = MissionType.Cargo, Origin = "earth", Destination = "mars",
                RequiredRangeTier = 3, RequiresLanding = true, Duration = 240, BaseSuccess = 1, AllowsCargo = true, RequiredLocations = L("mars"),
                Description = "Ship cargo to Mars.",
                DeltaVKms = 14.7, WindowPenaltyKms = 5, SynodicPeriodSeconds = 1200, RealTransitDays = 255,
                ScienceNote = "Launch windows open every 780 days (compressed to 20 minutes here). Automated routes wait for them." });
        }

        static void AddAchievements(GameDatabase db)
        {
            void A(string id, string name, string desc, AchievementCondition c, string target, double th, bool obj)
                => db.Add(new AchievementDef { Id = id, Name = name, Description = desc, Condition = c, TargetId = target, Threshold = th, IsObjective = obj });
            A("first_plates", "Industrial Revolution", "Produce 30 iron plates.", AchievementCondition.ResourceProduced, "iron_plate", 30, true);
            A("first_research", "Eureka", "Complete your first research.", AchievementCondition.TechCount, "", 1, true);
            A("rocketry", "Rocket Science", "Research Basic Rocketry.", AchievementCondition.TechCompleted, "basic_rocketry", 1, true);
            A("first_rocket", "Assembled", "Build your first rocket.", AchievementCondition.ResourceProduced, "sounding_rocket", 1, true);
            A("first_launch", "Liftoff!", "Complete the Suborbital Test Flight.", AchievementCondition.MissionCompleted, "suborbital_test", 1, true);
            A("orbit", "Low Earth Orbit", "Complete an Orbital Launch.", AchievementCondition.MissionCompleted, "orbital_launch", 1, true);
            A("moon", "Magnificent Desolation", "Land on the Moon.", AchievementCondition.LocationUnlocked, "moon", 1, true);
            A("he3", "Fuel of the Future", "Harvest 50 helium-3.", AchievementCondition.ResourceProduced, "helium3", 50, true);
            A("mars", "Red Planet", "Land on Mars.", AchievementCondition.LocationUnlocked, "mars", 1, true);
            A("industrialist", "Industrialist", "Own 25 buildings.", AchievementCondition.BuildingCount, "", 25, false);
            A("launches", "Frequent Flyer", "Launch 10 rockets.", AchievementCondition.RocketsLaunched, "", 10, false);
            A("scholar", "Scholar", "Complete 10 technologies.", AchievementCondition.TechCount, "", 10, false);
        }
    }
}
