using System;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Systems
{
    /// <summary>Data-driven achievements and guided objectives.</summary>
    public class AchievementSystem
    {
        readonly GameDatabase db;
        readonly GameState state;
        readonly GameEvents events;

        public AchievementSystem(GameDatabase db, GameState state, GameEvents events) { this.db = db; this.state = state; this.events = events; }

        public double Progress(AchievementDef a)
        {
            switch (a.Condition)
            {
                case AchievementCondition.ResourceProduced: return state.Stats.Produced.TryGetValue(a.TargetId, out var p) ? p : 0;
                case AchievementCondition.MissionCompleted: return state.Completions(a.TargetId);
                case AchievementCondition.LocationUnlocked: return state.IsUnlocked(a.TargetId) ? 1 : 0;
                case AchievementCondition.TechCount: return state.Research.Completed.Count;
                case AchievementCondition.TechCompleted: return state.Research.Completed.Contains(a.TargetId) ? 1 : 0;
                case AchievementCondition.BuildingCount: return state.Buildings.Count;
                case AchievementCondition.RocketsLaunched: return state.Stats.RocketsLaunched;
                default: return 0;
            }
        }

        public bool IsDone(AchievementDef a) => state.Achievements.Contains(a.Id);

        /// <summary>The first unfinished objective, or null if all are done.</summary>
        public AchievementDef CurrentObjective => db.Achievements.FirstOrDefault(a => a.IsObjective && !IsDone(a));

        public void Check()
        {
            foreach (var a in db.Achievements)
            {
                if (IsDone(a) || Progress(a) < a.Threshold - 1e-9) continue;
                state.Achievements.Add(a.Id);
                events.Notify((a.IsObjective ? "Objective complete: " : "Achievement unlocked: ") + a.Name, NotificationLevel.Success);
                events.RaiseAchievement(a.Id);
            }
        }
    }
}
