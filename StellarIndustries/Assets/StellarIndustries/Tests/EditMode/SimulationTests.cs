using System.Collections.Generic;
using NUnit.Framework;

namespace StellarIndustries.Tests
{
    /// <summary>Runs the shared simulation suite inside the Unity Test Runner (Window > General > Test Runner > EditMode).</summary>
    public class SimulationTests
    {
        static IEnumerable<TestCaseData> Cases()
        {
            foreach (var name in SimulationTestSuite.Names) yield return new TestCaseData(name).SetName(name);
        }

        [TestCaseSource(nameof(Cases))]
        public void Simulation(string name)
        {
            var r = SimulationTestSuite.Run(name);
            Assert.IsTrue(r.Passed, r.Message);
        }
    }
}
