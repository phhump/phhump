using System;
using System.Collections.Generic;
using StellarIndustries.Core.Data;

namespace StellarIndustries.Core.Simulation
{
    public class OfflineReport
    {
        public double RequestedSeconds;
        public double SimulatedSeconds;
        public double Efficiency;
        public bool ClockAnomaly;
        /// <summary>Net change per "location/resource" key (global resources use "global").</summary>
        public readonly Dictionary<string, double> Changes = new Dictionary<string, double>();
        public readonly List<string> CompletedResearch = new List<string>();
        public readonly List<string> MissionResults = new List<string>();
        public bool IsMeaningful => SimulatedSeconds >= 60;
    }

    /// <summary>
    /// Offline progression: re-runs the regular simulation in coarse steps for the time the
    /// player was away. Respects storage limits, energy, inputs and transport capacity
    /// automatically because it uses the same systems. Capped by OfflineCapSeconds and scaled
    /// by OfflineEfficiency (both upgradeable through research) to avoid exploits.
    /// </summary>
    public static class OfflineProgression
    {
        public const int MaxSteps = 2000;

        public static double ElapsedSince(long lastSavedUtcTicks, DateTime nowUtc, out bool anomaly)
        {
            anomaly = false;
            if (lastSavedUtcTicks <= 0) return 0;
            double s = (nowUtc.Ticks - lastSavedUtcTicks) / (double)TimeSpan.TicksPerSecond;
            if (s < 0) { anomaly = true; return 0; } // clock moved backwards: grant nothing
            return s;
        }

        public static OfflineReport Apply(GameSimulation sim, double elapsedSeconds)
        {
            var report = new OfflineReport { RequestedSeconds = Math.Max(0, elapsedSeconds) };
            double capped = Math.Min(report.RequestedSeconds, sim.Modifiers.OfflineCapSeconds);
            report.Efficiency = sim.Modifiers.OfflineEfficiency;
            report.SimulatedSeconds = capped;
            if (capped <= 0) return report;

            var before = Snapshot(sim);
            Action<string> onResearch = id => report.CompletedResearch.Add(sim.Db.Tech(id)?.Name ?? id);
            Action<string, bool> onMission = (id, ok) => report.MissionResults.Add((sim.Db.Mission(id)?.Name ?? id) + (ok ? " - success" : " - FAILED"));
            sim.Events.ResearchCompleted += onResearch;
            sim.Events.MissionCompleted += onMission;
            sim.Events.Muted = true;
            try
            {
                double step = Math.Max(1.0, Math.Min(60.0, capped / MaxSteps));
                double remaining = capped;
                while (remaining > 1e-9)
                {
                    double dt = Math.Min(step, remaining);
                    sim.Step(dt, report.Efficiency);
                    remaining -= dt;
                }
            }
            finally
            {
                sim.Events.Muted = false;
                sim.Events.ResearchCompleted -= onResearch;
                sim.Events.MissionCompleted -= onMission;
            }
            sim.State.Stats.OfflineSeconds += capped;
            var after = Snapshot(sim);
            foreach (var kv in after)
            {
                before.TryGetValue(kv.Key, out var b);
                if (Math.Abs(kv.Value - b) > 1e-6) report.Changes[kv.Key] = kv.Value - b;
            }
            foreach (var kv in before) if (!after.ContainsKey(kv.Key) && kv.Value > 1e-6) report.Changes[kv.Key] = -kv.Value;
            return report;
        }

        static Dictionary<string, double> Snapshot(GameSimulation sim)
        {
            var d = new Dictionary<string, double>();
            foreach (var loc in sim.State.Locations.Values)
                foreach (var kv in loc.Inventory) d[loc.Id + "/" + kv.Key] = kv.Value;
            foreach (var kv in sim.State.GlobalInventory) d["global/" + kv.Key] = kv.Value;
            return d;
        }
    }
}
