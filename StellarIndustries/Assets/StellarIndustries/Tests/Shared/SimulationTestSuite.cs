using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Save;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Systems;

namespace StellarIndustries.Tests
{
    /// <summary>
    /// Framework-free test suite for the simulation core. Executed by
    ///  - Tools/HeadlessTests (dotnet, no Unity required) and
    ///  - the Unity Test Runner (EditMode wrapper in Tests/EditMode).
    /// </summary>
    public static class SimulationTestSuite
    {
        public class TestResult { public string Name; public bool Passed; public string Message; }

        static readonly List<(string name, Action test)> Tests = new List<(string, Action)>
        {
            ("Content validates", ContentValidates),
            ("Starting production produces plates", StartingProduction),
            ("No power stops production", PowerShortageStopsProduction),
            ("Storage capacity is respected", StorageCapRespected),
            ("Construction cost scaling and upgrades", ConstructionAndUpgrade),
            ("Manual gather and craft", ManualActions),
            ("Research unlocks buildings and features", ResearchUnlocks),
            ("Mission launch, rewards and location unlock", MissionFlow),
            ("Reusable rocket returns, cargo delivered", ReusableCargo),
            ("Logistics route launches automatically", LogisticsRoute),
            ("Save/load round trip", SaveRoundTrip),
            ("Corrupted save falls back to backup", CorruptSaveBackup),
            ("Incompatible and garbage saves are rejected", RejectBadSaves),
            ("Sanitizer repairs unknown content", SanitizerRepairs),
            ("Offline progression cap and efficiency", OfflineCapAndEfficiency),
            ("Offline progression respects storage", OfflineRespectsStorage),
            ("Clock rollback grants nothing", ClockRollback),
            ("Simulation is deterministic", Determinism),
            ("Rocket equation matches analytic value", RocketEquation),
            ("Every mission is flyable with useful payload", MissionsFlyable),
            ("Overloaded rocket is blocked by delta-v", DeltaVBlocksOverload),
            ("Mars launch window changes delta-v", MarsLaunchWindow),
            ("Flight heritage improves reliability", FlightHeritage),
            ("Solar power follows irradiance", SolarPhysics),
            ("Exoplanet data is physically consistent", ExoplanetData),
            ("Seed ship needs fusion, chemistry is not enough", SeedShipPhysics),
            ("Interstellar flow: survey, seed ship, colony industry", InterstellarFlow),
            ("Bot playthrough reaches the Moon and harvests He-3", BotPlaythrough),
        };

        public static IEnumerable<string> Names => Tests.Select(t => t.name);

        public static TestResult Run(string name)
        {
            var (n, test) = Tests.First(t => t.name == name);
            var r = new TestResult { Name = n };
            try { test(); r.Passed = true; }
            catch (Exception e) { r.Message = e.Message + "\n" + e.StackTrace; }
            return r;
        }

        public static List<TestResult> RunAll(Action<string> log = null)
        {
            var results = new List<TestResult>();
            foreach (var (name, test) in Tests)
            {
                var r = new TestResult { Name = name };
                var start = DateTime.UtcNow;
                try { test(); r.Passed = true; }
                catch (Exception e) { r.Passed = false; r.Message = e.Message + "\n" + e.StackTrace; }
                results.Add(r);
                log?.Invoke((r.Passed ? "[PASS] " : "[FAIL] ") + name + " (" + (DateTime.UtcNow - start).TotalMilliseconds.ToString("0") + " ms)" + (r.Passed ? "" : "\n       " + r.Message));
            }
            return results;
        }

        // ---------------------------------------------------------------- helpers
        static void Assert(bool cond, string msg) { if (!cond) throw new Exception("Assertion failed: " + msg); }
        static void Near(double a, double b, double eps, string msg) { if (Math.Abs(a - b) > eps) throw new Exception($"Assertion failed: {msg} (expected {b}, got {a})"); }
        static GameSimulation NewSim(uint seed = 7) => GameSimulation.NewGame(DefaultContent.Create(), "Test", seed);
        static void Run(GameSimulation sim, double seconds, double dt = 1) { for (double t = 0; t < seconds; t += dt) sim.Tick(dt); }
        static double Get(GameSimulation sim, string res, string loc = "earth") => sim.Inventory.Get(loc, res);
        static void Give(GameSimulation sim, string res, double amt, string loc = "earth") => sim.Inventory.Add(loc, res, amt, false);
        static void CompleteTech(GameSimulation sim, params string[] ids) { foreach (var id in ids) sim.Research.Complete(id); }
        static void CompleteAllTechs(GameSimulation sim) { foreach (var t in sim.Db.Techs) sim.Research.Complete(t.Id); }
        static void GiveLaunch(GameSimulation sim, string mission, string rocket, string loc = "earth")
        {
            var m = sim.Db.Mission(mission); var r = sim.Db.Rocket(rocket);
            Give(sim, r.VehicleResourceId, 1, loc);
            foreach (var f in sim.Missions.FuelCost(m, r)) Give(sim, f.ResourceId, f.Amount, loc);
        }

        // ---------------------------------------------------------------- tests
        static void ContentValidates()
        {
            var errors = DefaultContent.Create().Validate();
            Assert(errors.Count == 0, "content errors:\n  " + string.Join("\n  ", errors));
        }

        static void StartingProduction()
        {
            var sim = NewSim();
            double before = Get(sim, "iron_plate");
            Run(sim, 60);
            Assert(Get(sim, "iron_plate") > before + 10, "iron plates should be produced, have " + Get(sim, "iron_plate"));
            var loc = sim.State.Location("earth");
            Assert(loc.EnergySupply > 0 && loc.EnergySatisfaction >= 0.999, "starting power should be sufficient");
        }

        static void PowerShortageStopsProduction()
        {
            var sim = NewSim();
            var wind = sim.State.Buildings.First(b => b.DefId == "wind_turbine");
            sim.Construction.SetEnabled(wind.Uid, false);
            double ore = Get(sim, "iron_ore");
            Run(sim, 30);
            Assert(Math.Abs(Get(sim, "iron_ore") - ore) < 1e-9 || Get(sim, "iron_ore") < ore, "mine must not produce without power");
            var mine = sim.State.Buildings.First(b => b.DefId == "surface_mine");
            Assert(mine.Status == BuildingStatus.NoPower, "mine status should be NoPower but is " + mine.Status);
        }

        static void StorageCapRespected()
        {
            var sim = NewSim();
            double cap = sim.Inventory.Capacity("earth", "iron_ore");
            double added = sim.Inventory.Add("earth", "iron_ore", cap * 3);
            Assert(Get(sim, "iron_ore") <= cap + 1e-9, "inventory exceeds capacity");
            Assert(added < cap * 3, "Add should report clamped amount");
            Give(sim, "iron_plate", 500);
            int before = sim.State.Buildings.Count;
            sim.Construction.Build("warehouse", "earth");
            Assert(sim.State.Buildings.Count == before + 1, "warehouse built");
            Assert(sim.Inventory.Capacity("earth", "iron_ore") > cap, "warehouse increases capacity");
            // Smelter consumes ore while mine output is blocked when ore storage is full
            var mine = sim.State.Buildings.First(b => b.DefId == "surface_mine");
            sim.Inventory.Add("earth", "iron_ore", 1e9);
            Run(sim, 5);
            Assert(Get(sim, "iron_ore") <= sim.Inventory.Capacity("earth", "iron_ore") + 1e-9, "never exceeds capacity during production");
        }

        static void ConstructionAndUpgrade()
        {
            var sim = NewSim();
            var def = sim.Db.Building("surface_mine");
            var c1 = sim.Construction.BuildCost(def, "earth")[0].Amount;
            Give(sim, "iron_plate", 1000);
            for (int i = 0; i < 4; i++) sim.Construction.Place(sim.Db.Building("wind_turbine"), "earth");
            var b = sim.Construction.Build("surface_mine", "earth", "mine_copper");
            Assert(b != null && b.RecipeId == "mine_copper", "mine built with copper recipe");
            var c2 = sim.Construction.BuildCost(def, "earth")[0].Amount;
            Assert(c2 > c1, "cost scales with count");
            Assert(!sim.Construction.SetRecipe(b.Uid, "mine_titanium"), "cannot select a recipe without deposit / tech");
            Run(sim, 20);
            double rate1 = b.EffectiveSpeed;
            Assert(sim.Construction.Upgrade(b.Uid), "upgrade succeeds");
            Run(sim, 2);
            Assert(b.Level == 2 && b.EffectiveSpeed > rate1 * 1.4, "upgrade increases speed");
            Assert(sim.Construction.Demolish(b.Uid), "demolish");
            Assert(sim.Construction.Find(b.Uid) == null, "building removed");
            var coal = sim.Db.Building("coal_plant");
            Assert(sim.Construction.PlacementProblem(sim.Db.Building("wind_turbine"), "earth") == null, "wind on earth ok");
            Assert(sim.Construction.PlacementProblem(coal, "earth") == null, "coal plant on earth ok");
        }

        static void ManualActions()
        {
            var sim = NewSim();
            double ore = Get(sim, "copper_ore");
            Assert(sim.Construction.Gather("earth", "copper_ore") > 0, "gather copper");
            Assert(Get(sim, "copper_ore") > ore, "copper increased");
            Assert(sim.Construction.Gather("earth", "titanium_ore") == 0, "cannot gather resources without deposit");
            Assert(sim.Construction.Gather("earth", "bauxite") == 0, "cannot gather before extraction is researched");
            Assert(sim.Construction.ManualCraft("earth", "draw_wire"), "manual craft wire");
            Assert(!sim.Construction.ManualCraft("earth", "make_engine"), "engines are not hand-craftable");
        }

        static void ResearchUnlocks()
        {
            var sim = NewSim();
            var oil = sim.Db.Building("oil_derrick");
            Assert(sim.Construction.PlacementProblem(oil, "earth") != null, "oil derrick locked initially");
            Assert(sim.Research.Status("basic_rocketry") == TechStatus.Locked, "rocketry locked");
            Give(sim, "science", 15);
            Assert(sim.Research.Start("petroleum"), "start research");
            Run(sim, 10);
            Assert(sim.Research.Status("petroleum") == TechStatus.Completed, "petroleum completed, status " + sim.Research.Status("petroleum"));
            Assert(sim.Construction.PlacementProblem(oil, "earth") == null, "oil derrick unlocked");
            Give(sim, "science", 1000);
            sim.Research.Start("electronics_1");
            sim.Research.Start("basic_rocketry");
            Assert(sim.Research.Status("basic_rocketry") == TechStatus.Locked, "cannot queue before prerequisites");
            Run(sim, 30);
            Assert(sim.Research.Start("basic_rocketry"), "queue rocketry after prereqs");
            Run(sim, 30);
            Assert(sim.State.HasFeature(Features.Spacecraft), "basic rocketry grants spacecraft feature");
            double before = sim.Modifiers.Multiplier(ModifierType.ProductionSpeed);
            CompleteTech(sim, "automation_1");
            Assert(sim.Modifiers.Multiplier(ModifierType.ProductionSpeed) > before, "automation increases production multiplier");
        }

        static void MissionFlow()
        {
            var sim = NewSim(99);
            CompleteTech(sim, "petroleum", "electronics_1", "basic_rocketry", "steelmaking", "aluminum_processing", "cryogenics", "orbital_rocketry", "silicon_processing", "electronics_2", "lunar_navigation");
            Assert(sim.Missions.LaunchProblem("suborbital_test", "sounding", "", 0) != null, "cannot launch without rocket");
            Give(sim, "sounding_rocket", 1); Give(sim, "rocket_fuel", 20);
            Assert(sim.Missions.LaunchProblem("suborbital_test", "sounding", "", 0) == null, "launch possible");
            Assert(sim.Missions.LaunchProblem("lunar_landing", "sounding", "", 0) != null, "sounding rocket cannot reach the moon");
            // Force success for deterministic test
            var mi = sim.Missions.Launch("suborbital_test", "sounding");
            Assert(mi != null && Get(sim, "sounding_rocket") == 0 && Get(sim, "rocket_fuel") == 0, "vehicle and fuel consumed");
            mi.SuccessChance = 1;
            Run(sim, mi.Duration + 1);
            Assert(sim.State.Completions("suborbital_test") == 1, "mission completed");
            Assert(Get(sim, "science") >= 60 - 1e-6, "science reward granted (incl. first time bonus)");

            GiveLaunch(sim, "lunar_landing", "lunar"); Give(sim, "steel", 30);
            var lm = sim.Missions.Launch("lunar_landing", "lunar", "steel", 30);
            Assert(lm != null, "lunar landing launched: " + sim.Missions.LaunchProblem("lunar_landing", "lunar", "steel", 30));
            lm.SuccessChance = 1;
            Run(sim, lm.Duration + 1);
            Assert(sim.State.IsUnlocked("moon"), "moon unlocked");
            Assert(Get(sim, "steel", "moon") >= 40 - 1e-6, "cargo + base kit delivered to moon, steel=" + Get(sim, "steel", "moon"));
            Assert(sim.Construction.Build("solar_array", "moon") == null, "solar needs photovoltaics");
            CompleteTech(sim, "photovoltaics");
            Assert(sim.Construction.Build("solar_array", "moon") != null, "solar array on moon built from local kit");
            Assert(sim.Construction.Build("wind_turbine", "moon") == null, "no wind turbines on the moon");
            Assert(sim.Construction.Build("surface_mine", "moon") != null, "lunar mine");
            var mine = sim.State.Buildings.Last(b => b.LocationId == "moon" && b.DefId == "surface_mine");
            Assert(mine.RecipeId == "mine_regolith" || mine.RecipeId == "mine_iron", "moon mine uses a lunar deposit, got " + mine.RecipeId);
            Run(sim, 30);
            Assert(sim.State.Location("moon").Inventory.Values.Sum() > 0, "moon produces");
        }

        static void ReusableCargo()
        {
            var sim = NewSim(5);
            CompleteAllTechs(sim);
            sim.Missions.UnlockLocation("moon");
            GiveLaunch(sim, "lunar_supply", "cargo"); Give(sim, "circuit", 90);
            var m = sim.Missions.Launch("lunar_supply", "cargo", "circuit", 60);
            Assert(m != null, "cargo launch: " + sim.Missions.LaunchProblem("lunar_supply", "cargo", "circuit", 60));
            Assert(sim.Missions.LaunchProblem("lunar_supply", "cargo", "circuit", 1000) != null, "cargo capacity enforced");
            m.SuccessChance = 1;
            Run(sim, m.Duration + 1);
            Near(Get(sim, "circuit", "moon"), 60, 1e-6, "cargo delivered");
            Near(Get(sim, "cargo_rocket"), 1, 1e-6, "reusable rocket returned");
        }

        static void LogisticsRoute()
        {
            var sim = NewSim(3);
            CompleteAllTechs(sim);
            sim.Missions.UnlockLocation("moon");
            Give(sim, "cargo_rocket", 1); Give(sim, "rocket_fuel", 600); Give(sim, "liquid_oxygen", 600); Give(sim, "hydrogen", 100); Give(sim, "steel", 300);
            var route = sim.Logistics.CreateRoute("lunar_supply", "cargo", "steel", 100);
            Assert(route != null, "route created");
            Run(sim, 2);
            Assert(sim.State.ActiveMissions.Count == 1 && route.Launches == 1, "route launched automatically");
            Give(sim, "liquid_oxygen", 600); // LOX is ~72% of kerolox propellant mass: refill for the second flight
            sim.State.ActiveMissions[0].SuccessChance = 1;
            Run(sim, sim.State.ActiveMissions[0].Duration + 3);
            Assert(route.Launches == 2, "route relaunched after return, launches=" + route.Launches);
            Assert(Get(sim, "steel", "moon") >= 100 - 1e-6, "steel arrived on the moon");
        }

        static void SaveRoundTrip()
        {
            var sim = NewSim();
            Give(sim, "science", 100);
            sim.Research.Start("petroleum");
            Run(sim, 120);
            var storage = new MemorySaveStorage();
            var sm = new SaveManager(storage, sim.Db);
            sm.Save(1, sim.State);
            var lr = sm.Load(1);
            Assert(lr.Ok, "load ok: " + lr.Error);
            Assert(lr.Warnings.Count == 0, "no warnings: " + string.Join("; ", lr.Warnings));
            string a = SaveSerializer.Serialize(sim.State), b = SaveSerializer.Serialize(lr.State);
            Assert(a.Substring(a.IndexOf('\n')) == b.Substring(b.IndexOf('\n')), "state identical after round trip");
            var sim2 = new GameSimulation(sim.Db, lr.State);
            Run(sim2, 10);
            Assert(sm.Info(1).Exists && sm.Info(1).Header.Buildings == sim.State.Buildings.Count, "slot info");
            Assert(!sm.Info(2).Exists, "slot 2 empty");
        }

        static void CorruptSaveBackup()
        {
            var sim = NewSim();
            var storage = new MemorySaveStorage();
            var sm = new SaveManager(storage, sim.Db);
            sm.Save(1, sim.State);
            Run(sim, 30);
            sm.Save(1, sim.State);
            string text = storage.Files["slot1"];
            storage.Files["slot1"] = text.Substring(0, text.Length / 2); // truncated write
            var lr = sm.Load(1);
            Assert(lr.Ok && lr.UsedBackup, "backup restored: " + lr.Error);
        }

        static void RejectBadSaves()
        {
            var db = DefaultContent.Create();
            Assert(!SaveSerializer.Deserialize("garbage", db).Ok, "garbage rejected");
            Assert(!SaveSerializer.Deserialize("", db).Ok, "empty rejected");
            var s = SaveSerializer.Serialize(GameSimulation.NewGame(db).State).Replace("\"Version\":" + SaveSerializer.CurrentVersion, "\"Version\":999");
            var r = SaveSerializer.Deserialize(s, db);
            Assert(!r.Ok && r.Error.Contains("newer"), "newer version rejected");
            var sm = new SaveManager(new MemorySaveStorage(), db);
            Assert(!sm.Load(3).Ok, "empty slot");
        }

        static void SanitizerRepairs()
        {
            var sim = NewSim();
            sim.State.Buildings.Add(new BuildingInstance { Uid = 500, DefId = "does_not_exist", LocationId = "earth" });
            sim.State.Buildings[0].RecipeId = "bogus";
            sim.State.Buildings[0].Level = 999;
            sim.State.Locations["earth"].Inventory["unknown_res"] = 5;
            sim.State.Locations["earth"].Inventory["iron_ore"] = double.NaN;
            sim.State.Research.Completed.Add("fake_tech");
            var text = SaveSerializer.Serialize(sim.State);
            var r = SaveSerializer.Deserialize(text, sim.Db);
            Assert(r.Ok, "loads");
            Assert(r.State.Buildings.All(b => sim.Db.Building(b.DefId) != null), "unknown building removed");
            Assert(r.State.Buildings[0].Level <= sim.Db.Building(r.State.Buildings[0].DefId).MaxLevel, "level clamped");
            Assert(!r.State.Locations["earth"].Inventory.ContainsKey("unknown_res"), "unknown resource removed");
            Assert(!r.State.Research.Completed.Contains("fake_tech"), "unknown tech removed");
            Assert(r.Warnings.Count > 0, "warnings reported");
        }

        static void OfflineCapAndEfficiency()
        {
            var sim = NewSim();
            var rep = OfflineProgression.Apply(sim, 100 * 3600);
            Near(rep.SimulatedSeconds, 8 * 3600, 1e-6, "capped to 8h");
            Near(rep.Efficiency, 0.5, 1e-9, "50% efficiency");
            Assert(rep.Changes.ContainsKey("earth/iron_plate"), "plates produced offline");

            // Compare against online production for the same duration
            var online = NewSim(); var offline = NewSim();
            Run(online, 600);
            OfflineProgression.Apply(offline, 1200); // 1200s at 50% ~ 600s online
            double a = Get(online, "iron_plate"), b = Get(offline, "iron_plate");
            Assert(Math.Abs(a - b) / Math.Max(1, a) < 0.15, $"offline at 50% for 2x time ~ online (online {a}, offline {b})");

            CompleteTech(sim, "automation_1", "offline_operations");
            Near(sim.Modifiers.OfflineCapSeconds, 12 * 3600, 1e-6, "cap upgraded");
            Near(sim.Modifiers.OfflineEfficiency, 0.75, 1e-9, "efficiency upgraded");
        }

        static void OfflineRespectsStorage()
        {
            var sim = NewSim();
            OfflineProgression.Apply(sim, 8 * 3600);
            foreach (var kv in sim.State.Location("earth").Inventory)
                Assert(kv.Value <= sim.Inventory.Capacity("earth", kv.Key) + 1e-6, kv.Key + " exceeds storage");
        }

        static void ClockRollback()
        {
            var now = DateTime.UtcNow;
            double e = OfflineProgression.ElapsedSince(now.AddHours(1).Ticks, now, out bool anomaly);
            Assert(e == 0 && anomaly, "rollback detected");
            e = OfflineProgression.ElapsedSince(now.AddHours(-2).Ticks, now, out anomaly);
            Near(e, 7200, 1, "normal elapsed");
            Assert(!anomaly, "no anomaly");
        }

        static void Determinism()
        {
            string Play()
            {
                var s = NewSim(42);
                CompleteTech(s, "petroleum", "electronics_1", "basic_rocketry");
                for (int i = 0; i < 5; i++) { Give(s, "sounding_rocket", 1); Give(s, "rocket_fuel", 20); s.Missions.Launch("suborbital_test", "sounding"); Run(s, 40); }
                s.State.PlayTime = 0; s.State.CreatedUtcTicks = 0; s.State.LastSavedUtcTicks = 0;
                var t = SaveSerializer.Serialize(s.State);
                return t.Substring(t.IndexOf('\n'));
            }
            Assert(Play() == Play(), "same seed and inputs give identical state");
        }

        // ---------------------------------------------------------------- physics
        static void RocketEquation()
        {
            var r = new RocketDef { Stages = new List<RocketStage> { new RocketStage("s", 1, 9, 300, PropellantType.Kerolox) } };
            double expected = 300 * SpacePhysics.G0 * Math.Log(10.0) / 1000; // m0/mf = 10
            Near(SpacePhysics.DeltaV(r, 0), expected, 1e-9, "single stage Tsiolkovsky");
            var two = new RocketDef { Stages = new List<RocketStage> { new RocketStage("a", 1, 9, 300, PropellantType.Kerolox), new RocketStage("b", 1, 9, 300, PropellantType.Kerolox) } };
            double upper = expected, lower = 300 * SpacePhysics.G0 * Math.Log(20.0 / 11.0) / 1000;
            Near(SpacePhysics.DeltaV(two, 0), upper + lower, 1e-9, "two stages, upper stage is payload of the lower one");
            double p = SpacePhysics.MaxPayloadT(two, 5);
            Near(SpacePhysics.DeltaV(two, p), 5, 1e-3, "max payload hits the requirement");
            Assert(SpacePhysics.MaxPayloadT(r, 100) < 0, "impossible requirement reports -1");
            var prop = SpacePhysics.Propellant(new RocketDef { Stages = new List<RocketStage> { new RocketStage("k", 10, 356, 300, PropellantType.Kerolox) } });
            Near(prop.First(x => x.ResourceId == "liquid_oxygen").Amount, 26, 1e-9, "LOX is 2.56/3.56 of kerolox mass (256 t = 25.6 -> 26 units)");
        }

        static void MissionsFlyable()
        {
            var sim = NewSim();
            foreach (var m in sim.Db.Missions)
            {
                var rockets = sim.Missions.SuitableRockets(m).ToList();
                Assert(rockets.Count > 0, m.Id + " has a suitable rocket");
                if (m.AllowsCargo && m.DeltaVKms > 0)
                    Assert(rockets.Any(r => SpacePhysics.MaxPayloadT(r, m.DeltaVKms) >= 4), m.Id + " can carry at least 4 t at the best window");
            }
            Assert(!sim.Missions.RocketSuitable(sim.Db.Mission("orbital_launch"), sim.Db.Rocket("sounding")), "sounding rocket cannot reach orbit");
            Assert(!sim.Missions.RocketSuitable(sim.Db.Mission("lunar_landing"), sim.Db.Rocket("orbital")), "orbital rocket cannot land on the Moon");
            Assert(sim.Missions.RocketSuitable(sim.Db.Mission("lunar_export"), sim.Db.Rocket("shuttle")), "shuttle can leave the Moon");
            Assert(!sim.Missions.RocketSuitable(sim.Db.Mission("lunar_supply"), sim.Db.Rocket("shuttle")), "shuttle cannot launch from Earth");
        }

        static void DeltaVBlocksOverload()
        {
            var sim = NewSim(11);
            CompleteAllTechs(sim);
            sim.Missions.UnlockLocation("moon");
            var m = sim.Db.Mission("lunar_supply"); var r = sim.Db.Rocket("lunar");
            GiveLaunch(sim, "lunar_supply", "lunar"); Give(sim, "circuit", 100);
            double max = sim.Missions.MaxCargoUnits(m, r, "circuit");
            Assert(max > 0 && max < 60, "physics limits heavy cargo below the cargo bay, max=" + max);
            var problem = sim.Missions.LaunchProblem(m.Id, r.Id, "circuit", max + 1);
            Assert(problem != null && problem.Contains("delta-v"), "overload rejected: " + problem);
            Assert(sim.Missions.LaunchProblem(m.Id, r.Id, "circuit", max) == null, "max cargo accepted");
            Assert(sim.Missions.MaxCargoUnits(m, r, "helium3") == r.CargoCapacity, "light cargo limited by the bay, not by mass");
        }

        static void MarsLaunchWindow()
        {
            var sim = NewSim(12);
            var m = sim.Db.Mission("mars_landing");
            sim.State.GameTime = 0;
            Near(sim.Missions.RequiredDeltaV(m), 14.7, 1e-9, "optimal window");
            sim.State.GameTime = m.SynodicPeriodSeconds / 2;
            Near(sim.Missions.RequiredDeltaV(m), 19.7, 1e-9, "worst point");
            Near(SpacePhysics.TimeToOptimalWindow(m, sim.State.GameTime), m.SynodicPeriodSeconds / 2, 1e-6, "time to window");
            var r = sim.Db.Rocket("mars");
            double cargoWorst = sim.Missions.MaxCargoUnits(m, r, "steel");
            sim.State.GameTime = 0;
            double cargoBest = sim.Missions.MaxCargoUnits(m, r, "steel");
            Assert(cargoBest > cargoWorst * 2, $"window matters: best {cargoBest} vs worst {cargoWorst}");
            double t0 = sim.State.GameTime;
            OfflineProgression.Apply(sim, 100);
            Near(sim.State.GameTime - t0, 100, 1e-6, "game time advances offline");
        }

        static void FlightHeritage()
        {
            var sim = NewSim(13);
            var r = sim.Db.Rocket("sounding");
            double r0 = sim.Missions.Reliability(r);
            sim.State.RocketFlights["sounding"] = 10;
            double r10 = sim.Missions.Reliability(r);
            Assert(r10 > r0 + 0.03 && r10 <= 0.99, $"heritage {r0} -> {r10}");
            sim.State.RocketFlights["sounding"] = 1000;
            Assert(sim.Missions.Reliability(r) <= 0.99, "capped");
        }

        static void SolarPhysics()
        {
            var db = DefaultContent.Create();
            Near(SpacePhysics.SolarFactor(db.Location("earth")), 1.0, 1e-9, "Earth surface reference");
            double moon = SpacePhysics.SolarFactor(db.Location("moon")), mars = SpacePhysics.SolarFactor(db.Location("mars"));
            Assert(moon > 1.3 && moon < 1.4, "Moon: no atmosphere, ~1.33x (" + moon + ")");
            Near(586.2 / 1361.0, 1 / (1.524 * 1.524), 0.01, "NASA irradiance follows the inverse-square law");
            Assert(mars > 0.4 && mars < 0.55, "Mars ~0.49x (" + mars + ")");
        }

        static void ExoplanetData()
        {
            var db = DefaultContent.Create();
            double fluxB = db.Location("proxima_b").IrradianceWm2 / SpacePhysics.SolarConstant;
            Assert(fluxB > 0.6 && fluxB < 0.75, "Proxima b: L/a² gives ~65-70% of Earth's flux, got " + fluxB);
            Near(db.Location("proxima_b").TemperatureC, 234 - 273.15, 1, "Proxima b equilibrium temperature (234 K)");
            Near(DefaultContent.KeplerAU(0.122, 5.122), 0.0288, 0.0005, "Proxima d semi-major axis from Kepler's third law");
            double trappistFromL = 0.000566 / (0.02925 * 0.02925);
            Assert(Math.Abs(trappistFromL - 0.604) / 0.604 < 0.15, "TRAPPIST-1e: L/a² agrees with the published 60.4% within 15% (" + trappistFromL + ")");
            Near(db.Location("trappist1_e").Gravity, 0.82, 0.01, "TRAPPIST-1e gravity from measured mass and radius");
            foreach (var l in db.Locations.Where(x => x.StarSystem != "sol"))
            {
                Assert(db.StarSystem(l.StarSystem).DistanceLy > 4, l.Id + " is in another star system");
                Assert(!string.IsNullOrEmpty(l.Status), l.Id + " carries its scientific status");
            }
            Assert(db.Location("eps_eri_b") != null && SpacePhysics.SolarFactor(db.Location("eps_eri_b")) < 0.05, "Epsilon Eridani b: sunlight too weak for solar");
        }

        static void SeedShipPhysics()
        {
            var db = DefaultContent.Create();
            var seed = db.Rocket("seed"); var m = db.Mission("seed_proxima_b");
            double ve = seed.Stages[0].IspSeconds * SpacePhysics.G0 / 1000;
            Assert(ve >= 9210 && ve <= 10600, "exhaust velocity within Project Daedalus' range: " + ve);
            Near(m.DeltaVKms, 2 * 0.03 * SpacePhysics.SpeedOfLightKms, 1e-6, "accelerate to 3% c and brake");
            Assert(MissionSystem.CanEverFly(m, seed), "seed ship can fly");
            Assert(SpacePhysics.MaxPayloadT(seed, m.DeltaVKms) >= 20, "seed ship carries >= 20 t");
            foreach (var r in db.Rockets.Where(x => x.Id != "seed")) Assert(!MissionSystem.CanEverFly(m, r), r.Id + " (chemical) cannot cross interstellar space");
            Assert(!db.Missions.Any(x => x.Origin != "earth" && x.Origin != "moon" && x.Destination == "earth"), "no return supply lines from other stars");
        }

        static void InterstellarFlow()
        {
            var sim = NewSim(21);
            CompleteAllTechs(sim);
            sim.Missions.UnlockLocation("moon");
            Assert(!sim.Missions.IsAvailable(sim.Db.Mission("seed_proxima_b")), "seed ship needs a survey first");
            Assert(sim.Construction.Build("laser_array", "moon") == null, "laser array only on Earth");
            Give(sim, "starshot_probe", 1); Give(sim, "beam_energy", 100);
            var probe = sim.Missions.Launch("survey_alpha_centauri", "starshot");
            Assert(probe != null, "probe launched: " + sim.Missions.LaunchProblem("survey_alpha_centauri", "starshot", "", 0));
            probe.SuccessChance = 1;
            Run(sim, probe.Duration + 1, 5);
            Assert(sim.State.Completions("survey_alpha_centauri") == 1 && !sim.State.IsUnlocked("proxima_b"), "survey done, planet not yet reached");
            Assert(sim.Missions.IsAvailable(sim.Db.Mission("seed_proxima_b")), "seed mission available after survey");
            GiveLaunch(sim, "seed_proxima_b", "seed", "moon");
            var ship = sim.Missions.Launch("seed_proxima_b", "seed", "", 0);
            Assert(ship != null, "seed ship launched: " + sim.Missions.LaunchProblem("seed_proxima_b", "seed", "", 0));
            ship.SuccessChance = 1;
            Run(sim, ship.Duration + 1, 5);
            Assert(sim.State.IsUnlocked("proxima_b"), "Proxima b reached");
            Assert(sim.Construction.Build("solar_array", "proxima_b") != null, "solar array built from the seed kit");
            Assert(sim.Construction.Build("surface_mine", "proxima_b") != null, "mine built on Proxima b");
            Run(sim, 60);
            Assert(Get(sim, "iron_ore", "proxima_b") + Get(sim, "regolith", "proxima_b") > 0, "colony produces local resources");
            Assert(sim.Construction.Build("coal_plant", "proxima_b") == null, "no coal plants without an oxygen atmosphere");
        }

        // ---------------------------------------------------------------- bot playthrough
        /// <summary>
        /// A scripted player that only uses public gameplay APIs (build, research, launch,
        /// manual gathering). Proves the progression from the starting state to the Moon is
        /// achievable and estimates pacing.
        /// </summary>
        static void BotPlaythrough()
        {
            var sim = NewSim(2024);
            var bot = new ProgressionBot(sim);
            double limit = 6 * 3600;
            double t = 0;
            var milestones = new Dictionary<string, double>();
            while (t < limit)
            {
                if (((int)t) % 5 == 0) bot.Decide();
                sim.Tick(1); t += 1;
                foreach (var a in sim.Db.Achievements)
                    if (sim.State.Achievements.Contains(a.Id) && !milestones.ContainsKey(a.Id)) milestones[a.Id] = t;
                if (sim.State.Achievements.Contains("he3")) break;
            }
            LastBotReport = string.Join(", ", milestones.OrderBy(k => k.Value).Select(k => k.Key + "@" + (k.Value / 60).ToString("0") + "min"))
                + $" | buildings={sim.State.Buildings.Count} techs={sim.State.Research.Completed.Count} launches={sim.State.Stats.RocketsLaunched} failed={sim.State.Stats.MissionsFailed}";
            Assert(sim.State.Achievements.Contains("first_launch"), "bot completes the first launch. " + LastBotReport + "\n" + bot.Diagnostics());
            Assert(sim.State.IsUnlocked("moon"), "bot reaches the moon within 6h. " + LastBotReport + "\n" + bot.Diagnostics());
            Assert(sim.State.Achievements.Contains("he3"), "bot harvests helium-3 on the moon. " + LastBotReport + "\n" + bot.Diagnostics());
        }

        public static string LastBotReport = "";
    }
}
