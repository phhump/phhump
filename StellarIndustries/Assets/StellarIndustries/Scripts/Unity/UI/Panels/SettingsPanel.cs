using StellarIndustries.Core.Systems;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class SettingsPanel : Panel
    {
        public SettingsPanel(GameUI ui) : base(ui) { }
        public override string Title => "Settings";

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            Heading("Settings");
            Scroll = GUILayout.BeginScrollView(Scroll);
            var st = Sim.State.Settings;
            GUILayout.Label("Interface", Theme.Header);
            GUILayout.BeginHorizontal();
            GUILayout.Label("UI scale", Theme.Label, GUILayout.Width(160));
            float scale = GUILayout.HorizontalSlider(st.UiScale, 0.7f, 1.6f, GUILayout.Width(200));
            if (Mathf.Abs(scale - st.UiScale) > 0.01f) st.UiScale = Mathf.Round(scale * 20) / 20;
            GUILayout.Label(NumberFormat.Percent(st.UiScale), Theme.Label);
            GUILayout.EndHorizontal();
            st.ShowHints = GUILayout.Toggle(st.ShowHints, " Show objective hints", Theme.Label);

            GUILayout.Label("Graphics", Theme.Header);
            GUILayout.Label("Detected: " + Performance.GraphicsQuality.HardwareSummary + "  ->  recommended " + Performance.GraphicsQuality.Detected, Theme.DimLabel);
            GUILayout.BeginHorizontal();
            foreach (Performance.GfxQuality q in System.Enum.GetValues(typeof(Performance.GfxQuality)))
                if (GUILayout.Button(q.ToString(), Performance.GraphicsQuality.Level == q ? Theme.NavActive : Theme.Button, GUILayout.Width(96), GUILayout.Height(32)))
                { Performance.GraphicsQuality.Set(q); Gx.Changed(); }
            GUILayout.EndHorizontal();
            GUILayout.Label(new[]
            {
                "Low: half-resolution textures, no shadows or MSAA, simpler terrain, fewer drones and particles, 30 fps. Real planets, atmospheres and UI stay.",
                "Medium: hard shadows, 2x MSAA, cloud layer, moderate decoration and drone counts.",
                "High: soft shadows, 4x MSAA, full terrain and decoration detail.",
                "Ultra: 8x MSAA, long shadow distance, extra-dense terrain and decoration, 40 drones.",
            }[(int)Performance.GraphicsQuality.Level], Theme.DimLabel);
            bool ad = GUILayout.Toggle(Performance.GraphicsQuality.Adaptive, " Adaptive quality (lower the preset automatically if the frame rate stays low)", Theme.Label);
            bool bat = GUILayout.Toggle(Performance.GraphicsQuality.BatterySaver, " Battery saver (10 fps in the background, 30 fps when idle in orbit view)", Theme.Label);
            bool fps = GUILayout.Toggle(Performance.GraphicsQuality.ShowFps, " Show FPS counter", Theme.Label);
            if (ad != Performance.GraphicsQuality.Adaptive || bat != Performance.GraphicsQuality.BatterySaver || fps != Performance.GraphicsQuality.ShowFps)
            {
                Performance.GraphicsQuality.Adaptive = ad; Performance.GraphicsQuality.BatterySaver = bat; Performance.GraphicsQuality.ShowFps = fps;
                Performance.GraphicsQuality.Save();
            }
            if (UI.GC.Perf != null) GUILayout.Label("Now: " + UI.GC.Perf.Fps.ToString("0") + " fps · slowest frame " + UI.GC.Perf.WorstFrameMs.ToString("0") + " ms", Theme.DimLabel);

            GUILayout.Label("Saving", Theme.Header);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Autosave every", Theme.Label, GUILayout.Width(160));
            st.AutosaveSeconds = Mathf.Round(GUILayout.HorizontalSlider(st.AutosaveSeconds, 10, 300, GUILayout.Width(200)) / 5) * 5;
            GUILayout.Label(st.AutosaveSeconds + " s", Theme.Label);
            GUILayout.EndHorizontal();
            GUILayout.Label("Active slot: " + UI.GC.ActiveSlot + "   ·   Folder: " + UI.GC.SaveDirectory, Theme.DimLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save now", Theme.Accent, GUILayout.Width(120), GUILayout.Height(30))) { UI.GC.Save(); UI.Notify("Game saved", NotificationLevel.Success); }
            if (GUILayout.Button("Save & main menu", Theme.Button, GUILayout.Width(160), GUILayout.Height(30))) { UI.GC.StopSimulation(true); Gx.Changed(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Offline production: " + NumberFormat.Percent(Sim.Modifiers.OfflineEfficiency) + " efficiency, up to " + NumberFormat.Duration(Sim.Modifiers.OfflineCapSeconds) + ".", Theme.DimLabel);

            GUILayout.Label("Developer tools", Theme.Header);
            bool dev = GUILayout.Toggle(st.DeveloperTools, " Enable developer tools (testing only)", Theme.Label);
            if (dev != st.DeveloperTools) { st.DeveloperTools = dev; Gx.Changed(); }
            if (st.DeveloperTools)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Game speed", Theme.Label, GUILayout.Width(160));
                foreach (var s in new[] { 1f, 5f, 20f, 100f })
                    if (GUILayout.Button(s + "x", Mathf.Approximately(UI.GC.TimeScale, s) ? Theme.NavActive : Theme.Button, GUILayout.Width(50))) UI.GC.TimeScale = s;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Simulate 1h offline", Theme.Button, GUILayout.Width(160))) { UI.GC.ApplyOffline(3600); Gx.Changed(); }
                if (GUILayout.Button("Simulate 10h offline", Theme.Button, GUILayout.Width(160))) { UI.GC.ApplyOffline(36000); Gx.Changed(); }
                if (GUILayout.Button("+1000 science", Theme.Button, GUILayout.Width(130))) Sim.Inventory.Add("earth", ResearchSystem.ScienceId, 1000, false);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fill storage here", Theme.Button, GUILayout.Width(160)))
                    foreach (var res in Sim.Db.Resources) if (!res.IsGlobal) Sim.Inventory.Add(Loc, res.Id, 1e9, false);
                if (GUILayout.Button("Complete active research", Theme.Button, GUILayout.Width(200)) && !string.IsNullOrEmpty(Sim.State.Research.ActiveTech))
                    Sim.Research.Complete(Sim.State.Research.ActiveTech);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
