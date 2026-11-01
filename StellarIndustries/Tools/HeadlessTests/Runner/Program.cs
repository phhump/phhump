using System;
using System.Linq;
using StellarIndustries.Tests;

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("Stellar Industries - headless simulation tests");
        var results = SimulationTestSuite.RunAll(Console.WriteLine);
        int failed = results.Count(r => !r.Passed);
        Console.WriteLine("Bot: " + SimulationTestSuite.LastBotReport);
        Console.WriteLine($"{results.Count - failed}/{results.Count} passed");
        return failed == 0 ? 0 : 1;
    }
}
