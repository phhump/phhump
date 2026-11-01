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
        for (int t = 0; t < 4 * 3600; t++)
        {
            if (t % 5 == 0) bot.Decide();
            sim.Tick(1);
            if (t % 900 == 0 && t > 3600) Console.WriteLine($"=== {t / 60} min ===\n{bot.Diagnostics()}\n");
            if (sim.State.IsUnlocked("moon")) { Console.WriteLine("Moon at " + t / 60 + " min"); break; }
        }
        return 0;
    }
}
