using System;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    public enum TechStatus { Locked, Available, Queued, Researching, Completed }

    /// <summary>
    /// Technology research. Labs produce the global "science" resource; the active project
    /// draws science until its cost is met. Material costs are paid from the HQ location
    /// when a project starts.
    /// </summary>
    public class ResearchSystem
    {
        public const string ScienceId = "science";
        readonly GameDatabase db;
        readonly GameState state;
        readonly InventorySystem inv;
        readonly ModifierSystem mods;
        readonly GameEvents events;
        readonly Action onCompleted;

        public ResearchSystem(GameDatabase db, GameState state, InventorySystem inv, ModifierSystem mods, GameEvents events, Action onCompleted)
        { this.db = db; this.state = state; this.inv = inv; this.mods = mods; this.events = events; this.onCompleted = onCompleted; }

        string Hq => db.Start.StartLocation;
        ResearchState R => state.Research;

        public TechStatus Status(string techId)
        {
            if (R.Completed.Contains(techId)) return TechStatus.Completed;
            if (R.ActiveTech == techId) return TechStatus.Researching;
            if (R.Queue.Contains(techId)) return TechStatus.Queued;
            var t = db.Tech(techId);
            if (t == null) return TechStatus.Locked;
            return t.Prerequisites.All(p => R.Completed.Contains(p)) ? TechStatus.Available : TechStatus.Locked;
        }

        public string StartProblem(string techId)
        {
            var t = db.Tech(techId);
            if (t == null) return "Unknown technology";
            var s = Status(techId);
            if (s == TechStatus.Completed) return "Already researched";
            if (s == TechStatus.Locked) return "Prerequisites missing";
            if (s == TechStatus.Researching || s == TechStatus.Queued) return "Already scheduled";
            return null;
        }

        /// <summary>Starts immediately when idle, otherwise appends to the queue.</summary>
        public bool Start(string techId)
        {
            if (StartProblem(techId) != null) return false;
            if (string.IsNullOrEmpty(R.ActiveTech))
            {
                if (!TryActivate(techId)) { R.Queue.Add(techId); events.Notify("Queued (waiting for materials): " + db.Tech(techId).Name, NotificationLevel.Warning); }
            }
            else R.Queue.Add(techId);
            return true;
        }

        public void Cancel(string techId)
        {
            if (R.Queue.Remove(techId)) return;
            if (R.ActiveTech == techId)
            {
                // Refund invested science and materials.
                inv.Add(Hq, ScienceId, R.ActiveProgress, false);
                var t = db.Tech(techId);
                if (t != null) foreach (var c in t.MaterialCost) inv.Add(Hq, c.ResourceId, c.Amount, false);
                R.ActiveTech = ""; R.ActiveProgress = 0;
            }
        }

        bool TryActivate(string techId)
        {
            var t = db.Tech(techId);
            if (t == null || !inv.TryPay(Hq, t.MaterialCost)) return false;
            R.ActiveTech = techId;
            R.ActiveProgress = 0;
            return true;
        }

        public double Rate(TechDef t) => Math.Max(db.Start.BaseResearchRate, t.ScienceCost / 20.0) * mods.Multiplier(ModifierType.ResearchSpeed);

        public double Fraction => string.IsNullOrEmpty(R.ActiveTech) ? 0 : R.ActiveProgress / Math.Max(1e-9, db.Tech(R.ActiveTech)?.ScienceCost ?? 1);

        public void Tick(double dt)
        {
            if (string.IsNullOrEmpty(R.ActiveTech))
            {
                for (int i = 0; i < R.Queue.Count; i++)
                {
                    var id = R.Queue[i];
                    if (Status(id) == TechStatus.Completed) { R.Queue.RemoveAt(i--); continue; }
                    if (TryActivate(id)) { R.Queue.RemoveAt(i); break; }
                }
                if (string.IsNullOrEmpty(R.ActiveTech) && R.AutoResearch && state.HasFeature(Features.AutoResearch))
                {
                    var next = db.Techs.Where(t => Status(t.Id) == TechStatus.Available && inv.Has(Hq, t.MaterialCost))
                                       .OrderBy(t => t.ScienceCost).FirstOrDefault();
                    if (next != null) TryActivate(next.Id);
                }
            }
            if (string.IsNullOrEmpty(R.ActiveTech)) return;
            var tech = db.Tech(R.ActiveTech);
            if (tech == null) { R.ActiveTech = ""; return; }
            double remaining = tech.ScienceCost - R.ActiveProgress;
            double draw = Math.Min(Math.Min(Rate(tech) * dt, inv.Get(Hq, ScienceId)), remaining);
            if (draw > 0 && inv.Remove(Hq, ScienceId, draw)) R.ActiveProgress += draw;
            if (R.ActiveProgress >= tech.ScienceCost - 1e-6) Complete(tech.Id);
        }

        public void Complete(string techId)
        {
            var t = db.Tech(techId);
            if (t == null || R.Completed.Contains(techId)) return;
            R.Completed.Add(techId);
            R.Queue.Remove(techId);
            if (R.ActiveTech == techId) { R.ActiveTech = ""; R.ActiveProgress = 0; }
            foreach (var f in t.UnlockFeatures) if (!state.Features.Contains(f)) state.Features.Add(f);
            onCompleted?.Invoke();
            events.Notify("Research complete: " + t.Name, NotificationLevel.Success);
            events.RaiseResearchCompleted(techId);
        }
    }
}
