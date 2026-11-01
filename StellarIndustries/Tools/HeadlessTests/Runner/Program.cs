using System;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Tests;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Contains("--bot-trace")) return BotTrace();
        if (args.Contains("--bench")) return Bench();
        int ei = Array.IndexOf(args, "--export-preview");
        if (ei >= 0 && ei + 1 < args.Length) return PreviewExport.Run(args[ei + 1]);
        Console.WriteLine("Stellar Industries - headless simulation tests");
        var results = SimulationTestSuite.RunAll(Console.WriteLine);
        int failed = results.Count(r => !r.Passed);
        Console.WriteLine("Bot: " + SimulationTestSuite.LastBotReport);
        Console.WriteLine($"{results.Count - failed}/{results.Count} passed");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>Performance benchmark: tick cost and garbage per tick for large bases.</summary>
    static int Bench()
    {
        foreach (int n in new[] { 100, 1000, 5000 })
        {
            var sim = GameSimulation.NewGame(DefaultContent.Create(), "Bench", 1);
            foreach (var t in sim.Db.Techs) sim.Research.Complete(t.Id);
            sim.Missions.UnlockLocation("moon");
            var defs = new[] { "surface_mine", "smelter", "component_factory", "research_lab", "wind_turbine", "chemical_plant" };
            var sw0 = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++)
            {
                var d = sim.Db.Building(defs[i % defs.Length]);
                sim.Construction.Place(d, i % 3 == 0 ? "moon" : "earth");
            }
            double placeMs = sw0.Elapsed.TotalMilliseconds;
            for (int i = 0; i < 50; i++) sim.Tick(0.1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int ticks = 600;
            for (int i = 0; i < ticks; i++) sim.Tick(1.0 / 60);
            sw.Stop();
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            Console.WriteLine($"{n,5} buildings: place {placeMs,8:0} ms total | tick {sw.Elapsed.TotalMilliseconds / ticks,6:0.000} ms | {alloc / ticks,7:0} bytes garbage per tick");
        }
        return 0;
    }

    /// <summary>Balancing aid: runs the progression bot and prints its state every 15 simulated minutes.</summary>
    static int BotTrace()
    {
        var sim = GameSimulation.NewGame(DefaultContent.Create(), "Trace", 2024);
        var bot = new ProgressionBot(sim);
        Console.WriteLine("  min | techs | science stock | sci/s | active research          | blocked% | buildings | milestones");
        double lastSci = 0; int lastT = 0;
        for (int t = 0; t <= 6 * 3600; t++)
        {
            if (t % 5 == 0) bot.Decide();
            sim.Tick(1);
            if (t % 900 == 0 && t > 0)
            {
                double produced = sim.State.Stats.Produced.TryGetValue("science", out var p) ? p : 0;
                double rate = (produced - lastSci) / (t - lastT); lastSci = produced; lastT = t;
                int blocked = sim.State.Buildings.Count(b => b.Status == StellarIndustries.Core.State.BuildingStatus.MissingInput || b.Status == StellarIndustries.Core.State.BuildingStatus.OutputFull);
                Console.WriteLine($"{t / 60,5} | {sim.State.Research.Completed.Count,5} | {sim.Inventory.Get("earth", "science"),13:0} | {rate,5:0.0} | {(sim.Db.Tech(sim.State.Research.ActiveTech)?.Name ?? "-"),-24} | {100.0 * blocked / Math.Max(1, sim.State.Buildings.Count),7:0}% | {sim.State.Buildings.Count,9} | {string.Join(",", sim.State.Achievements)}");
            }
        }
        return 0;
    }
}
