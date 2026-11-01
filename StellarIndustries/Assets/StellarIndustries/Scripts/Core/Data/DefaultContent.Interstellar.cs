using System;
using System.Collections.Generic;
using StellarIndustries.Core.Systems;

namespace StellarIndustries.Core.Data
{
    /// <summary>
    /// Era 6 content: real nearby star systems with confirmed exoplanets.
    /// Sources (read 2026-11-01): Wikipedia "List of nearest exoplanets", "Proxima Centauri b", "Barnard's Star",
    /// "Epsilon Eridani", "TRAPPIST-1e", "Alpha Centauri". Values marked "derived" are computed here from those data;
    /// values marked "assumption" are game choices (compositions/deposits of exoplanets are unknown!).
    /// </summary>
    public static partial class DefaultContent
    {
        /// <summary>Cruise speed of the seed ship as a fraction of c (game choice, see Docs/SCIENCE.md).</summary>
        public const double SeedShipCruiseC = 0.03;
        /// <summary>Laser-sail probe speed as a fraction of c (Breakthrough Starshot target).</summary>
        public const double ProbeCruiseC = 0.2;

        static void AddInterstellar(GameDatabase db)
        {
            // --- resources -------------------------------------------------------------------
            Res(db, "deuterium", "Deuterium", ResourceCategory.Material, "#9FD8FF", "Heavy hydrogen separated from water. Fusion fuel component.");
            Res(db, "fusion_fuel", "D-He3 Fusion Fuel", ResourceCategory.Fuel, "#F7DC6F", "Deuterium / helium-3 mix for fusion drives. 1 unit = 10 t.");
            Res(db, "beam_energy", "Laser Capacitor Charge", ResourceCategory.Fuel, "#FF66CC", "Stored energy for the laser launch array. Massless.");
            db.Resource("beam_energy").MassPerUnitT = 0;
            Res(db, "starshot_probe", "Laser-Sail Probe", ResourceCategory.Vehicle, "#FFFFFF", "Gram-scale chip on a light sail (Breakthrough Starshot concept).");
            Res(db, "seed_ship", "Fusion Seed Ship", ResourceCategory.Vehicle, "#FFFFFF", "Robotic ship carrying a self-replicating factory kit.");

            // --- recipes ---------------------------------------------------------------------
            Rec(db, "separate_deuterium", "Separate Deuterium", R("water", 10), R("deuterium", 1), 6, "fusion_drive");
            Rec(db, "make_fusion_fuel", "Blend D-He3 Fusion Fuel", R("helium3", 60, "deuterium", 40), R("fusion_fuel", 1), 20, "fusion_drive");
            Rec(db, "charge_capacitors", "Charge Laser Capacitors", new List<ResourceAmount>(), R("beam_energy", 1), 2, "laser_sails");
            Rec(db, "assemble_starshot", "Assemble Laser-Sail Probe", R("silicon_wafer", 4, "circuit", 4, "aluminum_sheet", 2), R("starshot_probe", 1), 30, "laser_sails");
            Rec(db, "assemble_seed_ship", "Assemble Fusion Seed Ship",
                R("titanium_alloy", 80, "rocket_engine", 20, "guidance_computer", 20, "landing_system", 10, "circuit", 100, "steel", 60, "silicon_wafer", 40),
                R("seed_ship", 1), 300, "interstellar_colonization");
            db.Building("chemical_plant").Recipes.AddRange(new[] { "separate_deuterium", "make_fusion_fuel" });
            db.Building("rocket_assembly").Recipes.AddRange(new[] { "assemble_starshot", "assemble_seed_ship" });

            var laser = B("laser_array", "Laser Launch Array", BuildingCategory.Assembly, R("steel", 200, "silicon_wafer", 150, "circuit", 100, "copper_wire", 200),
                "Phased laser array that pushes light sails to 15-20% of light speed (Starshot concept: up to 100 GW). Enormous power draw.");
            laser.Recipes = L("charge_capacitors"); laser.PowerUse = 120; laser.RequiredTech = "laser_sails"; laser.AllowedLocations = L("earth");
            laser.ColorHex = "#FF66CC"; laser.Shape = "panel"; laser.CostScaling = 1.5;
            db.Add(laser);

            // --- technologies ----------------------------------------------------------------
            var ls = T("laser_sails", "Laser Sails", TechCategory.InterstellarTravel, 10, 80000, "Light-sail probes pushed by ground-based lasers.", "photovoltaics", "interplanetary_navigation");
            ls.MaterialCost = R("silicon_wafer", 100); db.Add(ls);
            var fd = T("fusion_drive", "Fusion Drive", TechCategory.Propulsion, 11, 120000, "Deuterium/helium-3 fusion rocket (Project Daedalus heritage).", "fusion_power", "laser_sails");
            fd.MaterialCost = R("helium3", 100); db.Add(fd);
            var ic = T("interstellar_colonization", "Interstellar Colonization", TechCategory.InterstellarTravel, 12, 200000,
                "Robotic seed ships that build industry from local resources around other stars.", "fusion_drive", "automation_2");
            ic.Modifiers.Add(new ModifierDef(ModifierType.RocketReliability, 0.03)); db.Add(ic);

            // --- spacecraft ------------------------------------------------------------------
            db.Add(new RocketDef { Id = "starshot", Name = "Laser-Sail Probe", Category = RocketCategory.Probe, VehicleResourceId = "starshot_probe",
                RangeTier = 5, CargoCapacity = 0, Reliability = 0.75, FuelCost = R("beam_energy", 100),
                Description = "No propellant on board: the laser array supplies the push. Many probes are lost to dust impacts - send several." });
            db.Add(new RocketDef { Id = "seed", Name = "Fusion Seed Ship", Category = RocketCategory.InterstellarShip, VehicleResourceId = "seed_ship",
                RangeTier = 5, CanLand = true, CargoCapacity = 200, Reliability = 0.85,
                Stages = new List<RocketStage> { new RocketStage("D-He3 fusion stage", 150, 900, 1_000_000, PropellantType.FusionDHe3) },
                Description = "Accelerates to 3% of light speed and brakes again at the target. Exhaust velocity ~9,800 km/s, within Project Daedalus' 9,210-10,600 km/s." });

            // --- star systems (distance, RA/Dec, spectral type, luminosity) ----------------
            db.Add(new StarSystemDef { Id = "sol", Name = "Sol", DistanceLy = 0, SpectralType = "G2V", LuminositySun = 1, ColorHex = "#FFE9A8", Description = "Home." });
            db.Add(new StarSystemDef { Id = "alpha_centauri", RadiusSun = 0.154, Name = "Alpha Centauri", DistanceLy = 4.2465, RightAscensionDeg = Ra(14, 29, 42.9487), DeclinationDeg = Dec(-62, 40, 46.141),
                SpectralType = "M5.5V (Proxima) + G2V/K1V (A/B)", LuminositySun = 0.00155, ColorHex = "#FF8A5C",
                Description = "Nearest star system. Proxima Centauri is a flare star orbiting the Sun-like pair Alpha Centauri A and B (4.344 ly).",
                Notes = "Proxima b and d are confirmed. Proxima c is disputed. Alpha Centauri Ab is an unconfirmed candidate seen by JWST." });
            db.Add(new StarSystemDef { Id = "barnard", RadiusSun = 0.187, Name = "Barnard's Star", DistanceLy = 5.9629, RightAscensionDeg = Ra(17, 57, 48.49847), DeclinationDeg = Dec(4, 41, 36.1139),
                SpectralType = "M4.0V", LuminositySun = 0.0034, ColorHex = "#FF7043",
                Description = "Second-nearest single star. Four small planets confirmed (2024-2025), all closer than the habitable zone.",
                Notes = "A 2018 super-Earth candidate was refuted in 2021." });
            db.Add(new StarSystemDef { Id = "eps_eridani", RadiusSun = 0.735, Name = "Epsilon Eridani", DistanceLy = 10.475, RightAscensionDeg = Ra(3, 32, 55.84496), DeclinationDeg = Dec(-9, 27, 29.7312),
                SpectralType = "K2V", LuminositySun = 0.326, ColorHex = "#FFC27A",
                Description = "Young orange star with a Jupiter-like planet, warm dust (3-20 AU) and a Kuiper-belt analogue at 70 AU.",
                Notes = "The gap in the debris disc between 20 and 70 AU suggests further, undiscovered planets." });
            db.Add(new StarSystemDef { Id = "trappist1", RadiusSun = 0.12, Name = "TRAPPIST-1", DistanceLy = 40.7, RightAscensionDeg = Ra(23, 6, 29.283), DeclinationDeg = Dec(-5, 2, 28.59),
                SpectralType = "Ultra-cool red dwarf", LuminositySun = 0.000566, ColorHex = "#E0563B",
                Description = "Seven Earth-sized planets; e, f and g orbit in the habitable zone.",
                Notes = "JWST (2025) could not yet decide whether TRAPPIST-1e has an atmosphere." });

            // --- exoplanets --------------------------------------------------------------------
            AddExo(db, "proxima_b", "Proxima b", "alpha_centauri", BodyType.Planet, "Confirmed (2016) · min. mass 1.055 Earth masses · 11.19 d orbit",
                massEarth: 1.055, radiusEarth: 0, aAU: 0.04848, flux: -1, teqK: 234, transmission: 0.8,
                desc: "In the habitable zone of Proxima Centauri - but whether it has an atmosphere is unknown. Probably tidally locked.",
                deposits: new[] { ("iron_ore", 1.1), ("quartz_sand", 0.9), ("regolith", 1.2), ("water_ice", 0.8), ("rare_earths", 0.5) },
                hazards: new[] { "Stellar flares (brightness x100 within hours)", "Likely tidally locked", "Atmosphere unknown" });
            AddExo(db, "proxima_d", "Proxima d", "alpha_centauri", BodyType.Planet, "Confirmed (2022) · min. mass 0.26 Earth masses · 5.12 d orbit",
                massEarth: 0.26, radiusEarth: 0, aAU: KeplerAU(0.122, 5.122), flux: -1, teqK: 0, transmission: 0.8,
                desc: "Small, hot inner world. Rich sunlight for solar industry, no water expected.",
                deposits: new[] { ("iron_ore", 1.3), ("quartz_sand", 1.0), ("regolith", 1.0) },
                hazards: new[] { "Stellar flares", "Close orbit: likely tidally locked (assumption)" });
            AddExo(db, "barnard_e", "Barnard's Star e", "barnard", BodyType.Planet, "Confirmed (2025) · min. mass 0.193 Earth masses · 6.74 d orbit",
                massEarth: 0.193, radiusEarth: 0, aAU: 0.0381, flux: -1, teqK: 0, transmission: 0.8,
                desc: "Least massive planet ever found by the radial-velocity method. Too hot for liquid water, ideal for solar-powered mining.",
                deposits: new[] { ("iron_ore", 1.2), ("quartz_sand", 1.0), ("regolith", 1.0), ("rare_earths", 0.7) },
                hazards: new[] { "Inside the inner edge of the habitable zone" });
            AddExo(db, "eps_eri_b", "Epsilon Eridani b (Ægir)", "eps_eridani", BodyType.GasGiant, "Confirmed (2000) · ~318 Earth masses (about 1 Jupiter) · 3.53 AU",
                massEarth: 318, radiusEarth: 11.2, aAU: 3.53, flux: -1, teqK: 0, transmission: 1.0,
                desc: "Jupiter-like gas giant. Orbital stations scoop helium-3 from its atmosphere; sunlight is too weak for solar power - use fusion.",
                deposits: new[] { ("helium3", 2.5) },
                hazards: new[] { "No solid surface: orbital industry only", "Weak sunlight (~2.6% of Earth)" });
            AddExo(db, "trappist1_e", "TRAPPIST-1e", "trappist1", BodyType.Planet, "Confirmed · 0.692 Earth masses · 0.920 Earth radii · 6.10 d orbit",
                massEarth: 0.692, radiusEarth: 0.920, aAU: 0.02925, flux: 0.604, teqK: 249.7, transmission: 0.8,
                desc: "One of the best-studied potentially habitable worlds. Mass and radius are both measured, so its gravity is well known.",
                deposits: new[] { ("iron_ore", 1.0), ("quartz_sand", 1.0), ("water_ice", 1.2), ("regolith", 1.0), ("rare_earths", 0.5) },
                hazards: new[] { "Atmosphere unknown (JWST 2025 inconclusive)" });

            // Artist's impressions (assumption): textures of other bodies tinted by the star's light.
            Visual(db, "proxima_b", "exo_rock_a", "#FFB08A", 0.25, "#FFC9A8");
            Visual(db, "proxima_d", "exo_hot", tint: "#FFB38F");
            Visual(db, "barnard_e", "exo_rock_c", tint: "#FFB89A");
            Visual(db, "eps_eri_b", "jupiter", "#FFD9A8", 0.6, "#FFE2C2");
            Visual(db, "trappist1_e", "exo_ice", "#FF9C80", 0.3, "#FFC0A8");

            // --- missions ---------------------------------------------------------------------
            foreach (var (sys, target, science) in new[] { ("alpha_centauri", "proxima_b", 4000.0), ("barnard", "barnard_e", 5000.0), ("eps_eridani", "eps_eri_b", 8000.0), ("trappist1", "trappist1_e", 15000.0) })
            {
                var st = db.StarSystem(sys);
                db.Add(new MissionDef
                {
                    Id = "survey_" + sys, Name = "Probe Swarm: " + st.Name, Type = MissionType.Survey, Origin = "earth", Destination = target,
                    RequiredRangeTier = 5, Duration = Math.Round(120 * Math.Sqrt(st.DistanceLy)), BaseSuccess = 1, RewardScience = science,
                    FirstTimeRewards = R("science", science * 2), RequiredTech = "laser_sails",
                    RealTransitDays = (st.DistanceLy / ProbeCruiseC + st.DistanceLy) * 365.25,
                    Description = "Fly a laser-sail probe past " + st.Name + " (" + st.DistanceLy.ToString("0.##") + " ly) and map its planets.",
                    ScienceNote = "At 20% of light speed the flight takes " + (st.DistanceLy / ProbeCruiseC).ToString("0") + " years, and the data needs another "
                                  + st.DistanceLy.ToString("0.#") + " years to reach Earth at light speed.",
                });
            }
            void Seed(string target, string sys, bool landing, List<ResourceAmount> kit)
            {
                var st = db.StarSystem(sys); var loc = db.Location(target);
                db.Add(new MissionDef
                {
                    Id = "seed_" + target, Name = "Seed Ship to " + loc.Name, Type = MissionType.Landing, Origin = "moon", Destination = target,
                    RequiredRangeTier = 5, RequiresLanding = landing, Duration = Math.Round(600 + 30 * st.DistanceLy), BaseSuccess = 0.95,
                    DeltaVKms = SpacePhysics.InterstellarDeltaV(SeedShipCruiseC), AllowsCargo = true, RewardScience = 5000,
                    RequiredTech = "interstellar_colonization", RequiredMissions = L("survey_" + sys), UnlockLocation = target, FirstTimeRewards = kit,
                    RealTransitDays = st.DistanceLy / SeedShipCruiseC * 365.25,
                    Description = "Launch a robotic seed factory from the lunar shipyard. Colonies must be self-sufficient: there is no supply line across light-years.",
                    ScienceNote = "Accelerating to 3% of c and braking again needs ~18,000 km/s of delta-v - only fusion exhaust (~10,000 km/s) makes that possible.",
                });
            }
            var rocky = R("iron_plate", 150, "circuit", 80, "copper_wire", 100, "steel", 80, "silicon_wafer", 60, "pipe", 50);
            Seed("proxima_b", "alpha_centauri", true, rocky);
            Seed("proxima_d", "alpha_centauri", true, rocky);
            Seed("barnard_e", "barnard", true, rocky);
            Seed("eps_eri_b", "eps_eridani", false, R("steel", 120, "circuit", 120, "titanium_alloy", 40, "iron_plate", 100, "copper_wire", 80, "pipe", 40));
            Seed("trappist1_e", "trappist1", true, rocky);

            db.Add(new AchievementDef { Id = "first_probe", Name = "Pale Red Dot", Description = "Survey the Alpha Centauri system.", Condition = AchievementCondition.MissionCompleted, TargetId = "survey_alpha_centauri", IsObjective = true });
            db.Add(new AchievementDef { Id = "exoplanet", Name = "Another Sun", Description = "Establish industry on Proxima b.", Condition = AchievementCondition.LocationUnlocked, TargetId = "proxima_b", IsObjective = true });
            db.Add(new AchievementDef { Id = "four_stars", Name = "Interstellar Network", Description = "Have industry in 4 star systems (incl. Sol).", Condition = AchievementCondition.StarSystemsReached, Threshold = 4 });
        }

        static double StarMassSun(string sys) => sys == "alpha_centauri" ? 0.122 : sys == "barnard" ? 0.162 : sys == "trappist1" ? 0.09 : sys == "eps_eridani" ? 0.82 : 1;

        static void Visual(GameDatabase db, string id, string texture, string atmoHex = "", double atmo = 0, string tint = null)
        {
            var l = db.Location(id);
            l.SurfaceTexture = texture; l.AtmosphereColorHex = atmoHex; l.AtmosphereThickness = atmo;
            if (tint != null) l.TextureTintHex = tint;
        }

        static double Ra(double h, double m, double s) => (h + m / 60 + s / 3600) * 15;
        static double Dec(double d, double m, double s) => Math.Sign(d == 0 ? 1 : d) * (Math.Abs(d) + m / 60 + s / 3600);

        /// <summary>Derived: semi-major axis from Kepler's third law (a³ = M·P², AU / solar masses / years).</summary>
        public static double KeplerAU(double starMassSun, double periodDays) => Math.Pow(starMassSun * Math.Pow(periodDays / 365.25, 2), 1.0 / 3.0);

        static void AddExo(GameDatabase db, string id, string name, string sys, BodyType type, string status, double massEarth, double radiusEarth,
            double aAU, double flux, double teqK, double transmission, string desc, (string, double)[] deposits, string[] hazards)
        {
            var st = db.StarSystem(sys);
            // Derived: flux from luminosity and distance (inverse-square law) unless a measured value is given.
            double f = flux > 0 ? flux : st.LuminositySun / (aAU * aAU);
            // Assumption: rocky radius from the mass-radius relation R ~ M^0.27 when only the mass is known.
            double r = radiusEarth > 0 ? radiusEarth : Math.Pow(massEarth, 0.27);
            double t = teqK > 0 ? teqK : SpacePhysics.EquilibriumTempK(f);
            var loc = new LocationDef
            {
                Id = id, Name = name, StarSystem = sys, BodyType = type, Status = status, Description = desc,
                Gravity = Math.Round(massEarth / (r * r), 2), RadiusKm = Math.Round(6371.0 * r), EscapeVelocityKms = Math.Round(11.19 * Math.Sqrt(massEarth / r), 2),
                DistanceAU = aAU, IrradianceWm2 = Math.Round(SpacePhysics.SolarConstant * f, 1), SurfaceTransmission = transmission,
                TemperatureC = Math.Round(t - 273.15), Atmosphere = type == BodyType.GasGiant ? "Hydrogen / helium" : "Unknown",
                ColorHex = st.ColorHex, VisualRadius = 1, Hazards = new List<string>(hazards),
                ArtistImpression = true, TextureTintHex = st.ColorHex,
                // Close-in planets are probably tidally locked: one rotation per orbit (period from Kepler's third law).
                RotationPeriodHours = Math.Round(365.25 * 24 * Math.Sqrt(aAU * aAU * aAU / Math.Max(0.05, StarMassSun(sys))), 1),
            };
            foreach (var (res, rich) in deposits) loc.Deposits.Add(new DepositDef(res, rich));
            db.Add(loc);
        }
    }
}
