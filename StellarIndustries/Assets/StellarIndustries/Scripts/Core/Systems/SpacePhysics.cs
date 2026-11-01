using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;

namespace StellarIndustries.Core.Systems
{
    /// <summary>
    /// Simplified but real physics used by the game:
    ///  - Tsiolkovsky rocket equation with staging: Δv = Isp · g0 · ln(m0 / mf) per stage.
    ///  - Launch windows: Δv requirement varies over a (compressed) synodic period.
    ///  - Solar power from irradiance (inverse-square law) and atmospheric transmission.
    ///  - Reliability growth with flight heritage.
    /// Simplifications are deliberate to keep the game playable (see Docs/SCIENCE.md).
    /// </summary>
    public static class SpacePhysics
    {
        public const double G0 = 9.80665;               // m/s², standard gravity
        public const double SolarConstant = 1361.0;     // W/m² at 1 AU (NASA fact sheet)
        /// <summary>Clear-sky average transmission of Earth's atmosphere (simplified). Earth surface = solar factor 1.0.</summary>
        public const double EarthTransmission = 0.75;
        /// <summary>One propellant unit in the inventory equals this many tonnes.</summary>
        public const double PropellantUnitT = 10;

        public const string KeroseneId = "rocket_fuel", OxygenId = "liquid_oxygen", HydrogenId = "hydrogen";

        /// <summary>Oxidizer-to-fuel mass ratios (typical engine values).</summary>
        public static double MixtureRatio(PropellantType p) => p == PropellantType.Hydrolox ? 6.0 : 2.56;

        public static double MassPerUnit(ResourceDef r)
        {
            if (r == null) return 0;
            if (r.MassPerUnitT >= 0) return r.MassPerUnitT;
            switch (r.Category)
            {
                case ResourceCategory.Raw:
                case ResourceCategory.Material: return 0.1;
                case ResourceCategory.Component: return 0.5;
                case ResourceCategory.Fuel: return PropellantUnitT;
                case ResourceCategory.Vehicle: return 100;
                default: return 0;
            }
        }

        public static bool HasPhysics(RocketDef r) => r != null && r.Stages != null && r.Stages.Count > 0;

        /// <summary>Total delta-v (km/s) of a staged rocket carrying payloadT tonnes.</summary>
        public static double DeltaV(RocketDef r, double payloadT)
        {
            if (!HasPhysics(r)) return 0;
            double total = 0;
            for (int i = 0; i < r.Stages.Count; i++)
            {
                double above = payloadT;
                for (int j = i + 1; j < r.Stages.Count; j++) above += r.Stages[j].DryMassT + r.Stages[j].PropellantMassT;
                var s = r.Stages[i];
                double m0 = s.DryMassT + s.PropellantMassT + above, mf = s.DryMassT + above;
                if (mf <= 0) continue;
                total += s.IspSeconds * G0 * Math.Log(m0 / mf) / 1000.0;
            }
            return total;
        }

        /// <summary>Largest payload (t) that still reaches requiredKms; negative if even an empty rocket cannot.</summary>
        public static double MaxPayloadT(RocketDef r, double requiredKms)
        {
            if (DeltaV(r, 0) < requiredKms) return -1;
            double lo = 0, hi = 1;
            while (DeltaV(r, hi) >= requiredKms && hi < 1e6) hi *= 2;
            for (int i = 0; i < 50; i++)
            {
                double mid = (lo + hi) / 2;
                if (DeltaV(r, mid) >= requiredKms) lo = mid; else hi = mid;
            }
            return lo;
        }

        public static double TotalMassT(RocketDef r) => HasPhysics(r) ? r.Stages.Sum(s => s.DryMassT + s.PropellantMassT) : 0;

        /// <summary>Propellant to load, in inventory units. Explicit FuelCost wins, otherwise derived from stages.</summary>
        public static List<ResourceAmount> Propellant(RocketDef r)
        {
            if (r.FuelCost != null && r.FuelCost.Count > 0) return r.FuelCost.Select(f => new ResourceAmount(f.ResourceId, f.Amount)).ToList();
            var tonnes = new Dictionary<string, double>();
            void AddT(string id, double t) { tonnes.TryGetValue(id, out var c); tonnes[id] = c + t; }
            if (HasPhysics(r))
                foreach (var s in r.Stages)
                {
                    double ratio = MixtureRatio(s.Propellant);
                    double fuel = s.PropellantMassT / (1 + ratio), ox = s.PropellantMassT - fuel;
                    AddT(s.Propellant == PropellantType.Hydrolox ? HydrogenId : KeroseneId, fuel);
                    AddT(OxygenId, ox);
                }
            return tonnes.Select(kv => new ResourceAmount(kv.Key, Math.Max(1, Math.Ceiling(kv.Value / PropellantUnitT - 1e-9)))).ToList();
        }

        /// <summary>0 at the optimal window, 1 at the worst point.</summary>
        public static double WindowPhase(MissionDef m, double gameTime)
        {
            if (m.SynodicPeriodSeconds <= 0) return 0;
            return (1 - Math.Cos(2 * Math.PI * gameTime / m.SynodicPeriodSeconds)) / 2;
        }

        public static double RequiredDeltaV(MissionDef m, double gameTime) => m.DeltaVKms + m.WindowPenaltyKms * WindowPhase(m, gameTime);

        /// <summary>Seconds until the next optimal launch window (0 if windows do not apply).</summary>
        public static double TimeToOptimalWindow(MissionDef m, double gameTime)
        {
            if (m.SynodicPeriodSeconds <= 0) return 0;
            double t = gameTime % m.SynodicPeriodSeconds;
            return t < 1e-6 ? 0 : m.SynodicPeriodSeconds - t;
        }

        /// <summary>Solar output factor relative to Earth's surface (1.0).</summary>
        public static double SolarFactor(LocationDef l)
        {
            if (l.IrradianceWm2 > 0) return l.IrradianceWm2 * l.SurfaceTransmission / (SolarConstant * EarthTransmission);
            return l.SolarMultiplier;
        }

        /// <summary>Reliability bonus from successful flights of a design: up to +6%, half of it after ~6 flights.</summary>
        public static double HeritageBonus(int successfulFlights) => 0.06 * (1 - Math.Exp(-Math.Max(0, successfulFlights) / 8.0));
    }
}
