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
        int ei = Array.IndexOf(args, "--export-preview");
        if (ei >= 0 && ei + 1 < args.Length) return PreviewExport.Run(args[ei + 1]);
        Console.WriteLine("Stellar Industries - headless simulation tests");
        var results = SimulationTestSuite.RunAll(Console.WriteLine);
        int failed = results.Count(r => !r.Passed);
        Console.WriteLine("Bot: " + SimulationTestSuite.LastBotReport);
        Console.WriteLine($"{results.Count - failed}/{results.Count} passed");
        return failed == 0 ? 0 : 1;
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
