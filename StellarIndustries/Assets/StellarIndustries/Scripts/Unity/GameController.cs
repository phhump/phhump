using System;
using System.Collections.Generic;
using System.IO;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Save;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Unity.Content;
using StellarIndustries.Unity.UI;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity
{
    /// <summary>
    /// Composition root for the Unity layer: owns the content database, the save manager and
    /// the running simulation; drives the simulation clock, autosave and offline progression.
    /// Views/UI receive this controller explicitly (no global singleton).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public GameDatabase Db { get; private set; }
        public SaveManager Saves { get; private set; }
        public GameSimulation Sim { get; private set; }
        public int ActiveSlot { get; private set; } = 1;
        public bool InGame => Sim != null;

        /// <summary>Developer time multiplier (Settings > Developer tools).</summary>
        public float TimeScale = 1;

        public OfflineReport PendingOfflineReport;
        public readonly List<string> PendingWarnings = new List<string>();

        public event Action<GameSimulation> SimulationStarted;
        public event Action SimulationStopped;

        float autosaveTimer;
        DateTime pausedAtUtc;
        bool paused;

        public string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");

        void Awake()
        {
            Application.targetFrameRate = 60;
            Db = ContentLoader.Load();
            Saves = new SaveManager(new FileSaveStorage(SaveDirectory), Db);
            // Note: no '??' here - Unity's overloaded null check must be used for components.
            var view = GetComponent<SpaceView>();
            if (view == null) view = gameObject.AddComponent<SpaceView>();
            var ui = GetComponent<GameUI>();
            if (ui == null) ui = gameObject.AddComponent<GameUI>();
            view.Init(this);
            ui.Init(this, view);
        }

        public void NewGame(int slot, string name)
        {
            StopSimulation(false);
            ActiveSlot = slot;
            uint seed = (uint)Environment.TickCount;
            var sim = GameSimulation.NewGame(Db, name, seed == 0 ? 1u : seed);
            StartSimulation(sim);
            Save();
        }

        public bool LoadGame(int slot, out string error)
        {
            error = null;
            var result = Saves.Load(slot);
            if (!result.Ok) { error = result.Error; return false; }
            StopSimulation(false);
            ActiveSlot = slot;
            PendingWarnings.Clear();
            PendingWarnings.AddRange(result.Warnings);
            var sim = new GameSimulation(Db, result.State);
            double elapsed = OfflineProgression.ElapsedSince(result.State.LastSavedUtcTicks, DateTime.UtcNow, out bool anomaly);
            StartSimulation(sim);
            ApplyOffline(elapsed, anomaly);
            return true;
        }

        void StartSimulation(GameSimulation sim)
        {
            Sim = sim;
            autosaveTimer = 0;
            SimulationStarted?.Invoke(sim);
        }

        public void StopSimulation(bool save)
        {
            if (Sim == null) return;
            if (save) Save();
            Sim = null;
            PendingOfflineReport = null;
            SimulationStopped?.Invoke();
        }

        public void Save()
        {
            if (Sim == null) return;
            try { Saves.Save(ActiveSlot, Sim.State); }
            catch (Exception e)
            {
                Debug.LogError("[StellarIndustries] Save failed: " + e);
                Sim.Events.Notify("Save failed: " + e.Message, Core.Systems.NotificationLevel.Error);
            }
        }

        public void ApplyOffline(double elapsedSeconds, bool anomaly = false)
        {
            if (Sim == null) return;
            var report = OfflineProgression.Apply(Sim, elapsedSeconds);
            report.ClockAnomaly = anomaly;
            if (report.IsMeaningful || anomaly) PendingOfflineReport = report;
        }

        void Update()
        {
            if (Sim == null) return;
            // Clamp frame time: long hitches are not "free" production; real absences go through offline progression.
            double dt = Math.Min(Time.unscaledDeltaTime, 0.25) * Math.Max(0, TimeScale);
            Sim.Tick(dt);
            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= Sim.State.Settings.AutosaveSeconds) { autosaveTimer = 0; Save(); }
        }

        void OnApplicationPause(bool pause)
        {
            if (Sim == null) return;
            if (pause) { paused = true; pausedAtUtc = DateTime.UtcNow; Save(); }
            else if (paused)
            {
                paused = false;
                double away = (DateTime.UtcNow - pausedAtUtc).TotalSeconds;
                if (away > 30) ApplyOffline(away);
            }
        }

        void OnApplicationQuit() => Save();
    }

    /// <summary>Creates the game automatically in any scene that has no GameController.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindFirstObjectByType<GameController>();
#else
            var existing = UnityEngine.Object.FindObjectOfType<GameController>();
#endif
            if (existing == null) new GameObject("Stellar Industries").AddComponent<GameController>();
        }
    }
}
