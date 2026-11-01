using System.Collections.Generic;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    /// <summary>Aggregates additive bonuses from completed technologies. Cached; call Rebuild after research.</summary>
    public class ModifierSystem
    {
        readonly GameDatabase db;
        readonly GameState state;
        readonly Dictionary<ModifierType, double> sums = new Dictionary<ModifierType, double>();

        public ModifierSystem(GameDatabase db, GameState state) { this.db = db; this.state = state; Rebuild(); }

        public void Rebuild()
        {
            sums.Clear();
            foreach (var id in state.Research.Completed)
            {
                var t = db.Tech(id);
                if (t == null) continue;
                foreach (var m in t.Modifiers) sums[m.Type] = Sum(m.Type) + m.Value;
            }
        }

        public double Sum(ModifierType t) => sums.TryGetValue(t, out var v) ? v : 0;
        /// <summary>1 + sum of bonuses.</summary>
        public double Multiplier(ModifierType t) => 1 + Sum(t);

        public double OfflineEfficiency => System.Math.Min(1.0, db.Start.BaseOfflineEfficiency + Sum(ModifierType.OfflineEfficiency));
        public double OfflineCapSeconds => (db.Start.BaseOfflineCapHours + Sum(ModifierType.OfflineCapHours)) * 3600.0;
    }
}
