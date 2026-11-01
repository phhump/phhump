using UnityEngine;

namespace StellarIndustries.Unity.Performance
{
    /// <summary>
    /// Measures frame times. Adaptive quality: if the game runs far below its target for ~16 s it steps the preset
    /// down once (never up, to avoid oscillation). Battery saver: 10 fps when the window is in the background and
    /// 30 fps after a minute without input in the orbit views (an idle game spends most of its time waiting).
    /// The simulation is time-based, so lower frame rates never change game speed.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        public float Fps { get; private set; } = 60;
        public float WorstFrameMs { get; private set; }
        public event System.Action<string> Notice;

        float windowTime, worst, idleTime, graceTime = 10;
        int frames, slowWindows;
        bool focused = true;

        /// <summary>Call after heavy scene changes (entering a surface) so loading hitches are not counted.</summary>
        public void Grace(float seconds = 8) { graceTime = seconds; slowWindows = 0; windowTime = 0; frames = 0; }

        public void NotifyInput() => idleTime = 0;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            frames++; windowTime += dt; worst = Mathf.Max(worst, dt);
            idleTime += dt;
            if (graceTime > 0) graceTime -= dt;
            if (windowTime >= 1f)
            {
                Fps = frames / windowTime;
                WorstFrameMs = worst * 1000;
                if (graceTime <= 0 && GraphicsQuality.Adaptive && focused)
                {
                    bool slow = Fps < GraphicsQuality.TargetFps * 0.55f;
                    slowWindows = slow ? slowWindows + 1 : Mathf.Max(0, slowWindows - 1);
                    if (slowWindows >= 16 && GraphicsQuality.Level > GfxQuality.Low)
                    {
                        GraphicsQuality.Set(GraphicsQuality.Level - 1);
                        Notice?.Invoke("Graphics lowered to " + GraphicsQuality.Level + " for smoother play (Settings > Graphics)");
                        Grace(12);
                    }
                }
                frames = 0; windowTime = 0; worst = 0;
            }
            int target = GraphicsQuality.TargetFps;
            if (GraphicsQuality.BatterySaver)
            {
                if (!focused) target = 10;
                else if (idleTime > 60 && IdleAllowed) target = 30;
            }
            if (Application.targetFrameRate != target)
            {
                Application.targetFrameRate = target;
                QualitySettings.vSyncCount = target < GraphicsQuality.TargetFps ? 0 : (GraphicsQuality.Level == GfxQuality.Low ? 0 : 1);
            }
        }

        /// <summary>Set by the UI: idle throttling only in orbit/menu views, never while walking around.</summary>
        public bool IdleAllowed = true;

        void OnApplicationFocus(bool f) { focused = f; idleTime = 0; }
    }
}
