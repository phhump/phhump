using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.Systems;
using StellarIndustries.Core.Util;
using StellarIndustries.Tests;

/// <summary>
/// Exports real simulation state (bot playthrough) as JSON for preview renders of the UI.
/// The renders are NOT Unity screenshots; they visualise genuine game data outside the engine.
/// </summary>
public static class PreviewExport
{
    public static int Run(string path)
    {
        var sim = GameSimulation.NewGame(DefaultContent.Create(), "Preview Empire", 2024);
        var bot = new ProgressionBot(sim);
        int t = 0;
        for (; t < 140 * 60; t++) { if (t % 5 == 0) bot.Decide(); sim.Tick(1); }
        var root = new Dictionary<string, object> { ["midgame"] = Snapshot(sim, "earth") };
        for (; t < 240 * 60; t++) { if (t % 5 == 0) bot.Decide(); sim.Tick(1); }
        root["moon"] = Snapshot(sim, "moon");
        // Late-game developer scenario for the star map: all techs, Alpha Centauri surveyed, Proxima b settled, a seed ship en route to TRAPPIST-1.
        foreach (var tech in sim.Db.Techs) sim.Research.Complete(tech.Id);
        foreach (var sys in new[] { "alpha_centauri", "barnard", "eps_eridani", "trappist1" }) sim.State.MissionCompletions["survey_" + sys] = 1;
        sim.Missions.UnlockLocation("proxima_b");
        sim.State.ActiveMissions.Add(new StellarIndustries.Core.State.MissionInstance { Uid = 99999, MissionId = "seed_trappist1_e", RocketId = "seed", Duration = 1821, Elapsed = 700, SuccessChance = 0.9 });
        root["late"] = Snapshot(sim, "earth");
        var stars = new List<object>();
        foreach (var s in sim.Db.StarSystems)
        {
            double r = 40 * Math.Sqrt(s.DistanceLy), ra = s.RightAscensionDeg * Math.PI / 180, dec = s.DeclinationDeg * Math.PI / 180;
            stars.Add(new Dictionary<string, object> { ["id"] = s.Id, ["name"] = s.Name, ["ly"] = s.DistanceLy, ["type"] = s.SpectralType, ["lum"] = s.LuminositySun, ["color"] = s.ColorHex,
                ["x"] = r * Math.Cos(dec) * Math.Cos(ra), ["y"] = r * Math.Sin(dec), ["z"] = r * Math.Cos(dec) * Math.Sin(ra), ["notes"] = s.Notes, ["desc"] = s.Description,
                ["planets"] = sim.Db.Locations.Where(l => l.StarSystem == s.Id).Select(l => (object)new Dictionary<string, object> {
                    ["name"] = l.Name, ["status"] = l.Status, ["g"] = l.Gravity, ["t"] = l.TemperatureC, ["sun"] = SpacePhysics.SolarFactor(l), ["unlocked"] = sim.State.IsUnlocked(l.Id),
                    ["hazards"] = string.Join(", ", l.Hazards), ["res"] = string.Join(", ", l.Deposits.Select(d => sim.Db.ResourceName(d.ResourceId))) }).ToList() });
        }
        root["stars"] = stars;
        File.WriteAllText(path, Json.Serialize(root));
        Console.WriteLine("Preview data written to " + path + " (" + sim.State.Buildings.Count + " buildings)");
        return 0;
    }

    static Dictionary<string, object> Snapshot(GameSimulation sim, string locId)
    {
        var loc = sim.State.Location(locId); var def = sim.Db.Location(locId);
        var buildings = sim.Production.BuildingsAt(locId).GroupBy(b => b.DefId).Select(g => (object)new Dictionary<string, object>
        {
            ["name"] = sim.Db.Building(g.Key).Name,
            ["items"] = g.OrderBy(b => b.Uid).Select(b => (object)new Dictionary<string, object> {
                ["lv"] = b.Level, ["recipe"] = sim.Db.Recipe(b.RecipeId)?.Name ?? "", ["status"] = b.Status.ToString(), ["progress"] = b.Progress, ["speed"] = b.EffectiveSpeed,
                ["power"] = sim.Db.Building(b.DefId).PowerOutput > 0 }).ToList(),
        }).ToList();
        var inv = sim.Db.Resources.Where(r => !r.IsGlobal && sim.Inventory.Get(locId, r.Id) > 0).Select(r => (object)new Dictionary<string, object>
        {
            ["name"] = r.Name, ["color"] = r.ColorHex, ["cat"] = r.Category.ToString(), ["amt"] = sim.Inventory.Get(locId, r.Id), ["cap"] = sim.Inventory.Capacity(locId, r.Id),
            ["rate"] = loc.Rates.TryGetValue(r.Id, out var rt) ? rt : 0.0,
        }).ToList();
        var techs = sim.Db.Techs.Select(t => (object)new Dictionary<string, object>
        {
            ["id"] = t.Id, ["name"] = t.Name, ["tier"] = t.Tier, ["cat"] = t.Category.ToString(), ["cost"] = t.ScienceCost, ["status"] = sim.Research.Status(t.Id).ToString(),
            ["pre"] = t.Prerequisites.Cast<object>().ToList(),
        }).ToList();
        var missions = sim.State.ActiveMissions.Select(m => (object)new Dictionary<string, object> { ["name"] = sim.Db.Mission(m.MissionId).Name, ["p"] = m.Elapsed / m.Duration, ["left"] = NumberFormat.Duration(m.Duration - m.Elapsed) }).ToList();
        var obj = sim.Achievements.CurrentObjective;
        return new Dictionary<string, object>
        {
            ["loc"] = def.Name, ["desc"] = def.Description, ["g"] = def.Gravity, ["atm"] = def.Atmosphere, ["temp"] = def.TemperatureC, ["solar"] = SpacePhysics.SolarFactor(def),
            ["radius"] = def.RadiusKm, ["esc"] = def.EscapeVelocityKms, ["day"] = def.DayLengthHours, ["irr"] = def.IrradianceWm2, ["au"] = def.DistanceAU,
            ["deposits"] = string.Join(", ", def.Deposits.Select(d => sim.Db.ResourceName(d.ResourceId) + " " + NumberFormat.Percent(d.Richness))),
            ["supply"] = loc.EnergySupply, ["demand"] = loc.EnergyDemand, ["science"] = sim.Inventory.Get("earth", "science"),
            ["research"] = sim.Db.Tech(sim.State.Research.ActiveTech)?.Name ?? "", ["rfrac"] = sim.Research.Fraction,
            ["play"] = NumberFormat.Duration(sim.State.PlayTime), ["locations"] = sim.State.UnlockedLocations.Select(l => (object)sim.Db.Location(l).Name).ToList(),
            ["buildings"] = buildings, ["inv"] = inv, ["techs"] = techs, ["missions"] = missions,
            ["objective"] = obj == null ? "" : obj.Name + " - " + obj.Description, ["objp"] = obj == null ? 1 : sim.Achievements.Progress(obj) / obj.Threshold,
            ["count"] = sim.State.Buildings.Count(b => b.LocationId == locId),
        };
    }
}
