using System;

namespace StellarIndustries.Core.Systems
{
    public enum NotificationLevel { Info, Success, Warning, Error }

    /// <summary>
    /// Central event hub. Gameplay systems raise events; UI, visuals and audio subscribe.
    /// Keeps systems decoupled (no system references the UI).
    /// </summary>
    public class GameEvents
    {
        public event Action<string, NotificationLevel> Notification;
        public event Action<string> ResearchCompleted;
        public event Action<string, string, string> RocketLaunched;   // missionId, rocketId, originLocation
        public event Action<string, bool> MissionCompleted;          // missionId, success
        public event Action<string> LocationUnlocked;
        public event Action<int> BuildingsChanged;                   // uid (0 = many)
        public event Action<string> AchievementUnlocked;

        /// <summary>When true (offline simulation), notifications are suppressed.</summary>
        public bool Muted;

        public void Notify(string msg, NotificationLevel level = NotificationLevel.Info) { if (!Muted) Notification?.Invoke(msg, level); }
        public void RaiseResearchCompleted(string id) => ResearchCompleted?.Invoke(id);
        public void RaiseRocketLaunched(string mission, string rocket, string origin) => RocketLaunched?.Invoke(mission, rocket, origin);
        public void RaiseMissionCompleted(string id, bool success) => MissionCompleted?.Invoke(id, success);
        public void RaiseLocationUnlocked(string id) => LocationUnlocked?.Invoke(id);
        public void RaiseBuildingsChanged(int uid) => BuildingsChanged?.Invoke(uid);
        public void RaiseAchievement(string id) => AchievementUnlocked?.Invoke(id);
    }
}
