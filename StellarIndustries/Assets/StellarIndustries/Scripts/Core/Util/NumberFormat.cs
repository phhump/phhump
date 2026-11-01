using System;
using System.Globalization;

namespace StellarIndustries.Core.Util
{
    public static class NumberFormat
    {
        static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

        public static string Short(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            double a = Math.Abs(v);
            if (a < 1000) return v.ToString(a < 10 && a != Math.Floor(a) ? "0.#" : "0", CultureInfo.InvariantCulture);
            int idx = 0;
            while (a >= 1000 && idx < Suffixes.Length - 1) { a /= 1000; v /= 1000; idx++; }
            return v.ToString(a < 100 ? "0.##" : "0.#", CultureInfo.InvariantCulture) + Suffixes[idx];
        }

        public static string Rate(double perSecond)
        {
            string sign = perSecond > 0 ? "+" : "";
            if (Math.Abs(perSecond) < 0.1 && perSecond != 0)
                return sign + (perSecond * 60).ToString("0.##", CultureInfo.InvariantCulture) + "/min";
            return sign + perSecond.ToString("0.##", CultureInfo.InvariantCulture) + "/s";
        }

        public static string Duration(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var ts = TimeSpan.FromSeconds(Math.Round(seconds));
            if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        public static string Percent(double fraction) => (fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
