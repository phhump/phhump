using System;
using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.Systems;
using StellarIndustries.Core.Util;
using StellarIndustries.Unity.UI.Panels;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.UI
{
    /// <summary>
    /// Immediate-mode UI. Presentation only: reads simulation state and calls system APIs.
    /// Layout uses a virtual resolution (900 px high) scaled to the screen and the user's UI scale.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public GameController GC { get; private set; }
        public SpaceView View { get; private set; }
        public Surface.SurfaceWorld Surface { get; private set; }
        /// <summary>Player wants to be on the surface (engineer mode) rather than in the orbital view.</summary>
        public bool WantSurface = true;
        /// <summary>Command center (all management screens) open on top of the surface.</summary>
        public bool CommandOpen;
        SurfaceHud hud;
        public GameSimulation Sim => GC != null ? GC.Sim : null;

        public float Scale { get; private set; } = 1;
        public float VW { get; private set; }
        public float VH { get; private set; }
        public Rect PanelRect { get; private set; }

        Panel[] panels;
        public int Active;
        MainMenu menu;

        public class Toast { public string Text; public NotificationLevel Level; public float Time; }
        readonly List<Toast> toasts = new List<Toast>();
        public readonly List<Toast> Log = new List<Toast>();

        string confirmText; Action confirmAction;
        GUIStyle labelStyle;
        Vector2 modalScroll;
        bool dragging; Vector2 dragStart;
        readonly List<SpaceView.Label> labels = new List<SpaceView.Label>();
        const float TopH = 62, NavW = 176, Gap = 10;
        bool locationMenuOpen;

        public void Init(GameController gc, SpaceView view, Surface.SurfaceWorld surface = null)
        {
            GC = gc; View = view; Surface = surface;
            hud = new SurfaceHud(this);
            if (surface != null) surface.OpenScreen += screen =>
            {
                CommandOpen = true;
                if (screen == "research") Open<ResearchPanel>(); else if (screen == "missions") Open<MissionsPanel>();
            };
            gc.SimulationStopped += () => { Surface?.Exit(); CommandOpen = false; };
            if (gc.Perf != null) gc.Perf.Notice += msg => Notify(msg, NotificationLevel.Warning);
            // A preset change rebuilds the visuals that depend on it (terrain grid, LOD, spheres, clouds, budgets).
            Performance.GraphicsQuality.Changed += () =>
            {
                View.RefreshQuality();
                if (Surface != null && Surface.Active && Sim != null)
                {
                    var pos = Surface.Engineer != null ? Surface.Engineer.transform.position : Vector3.zero;
                    Surface.Enter(Sim, Sim.State.CurrentLocation, pos);
                }
            };
            menu = new MainMenu(this);
            panels = new Panel[]
            {
                new OverviewPanel(this), new IndustriesPanel(this), new ResourcesPanel(this), new ResearchPanel(this),
                new SpacecraftPanel(this), new MissionsPanel(this), new LogisticsPanel(this), new MapPanel(this),
                new StatisticsPanel(this), new SettingsPanel(this),
            };
            gc.SimulationStarted += s => { s.Events.Notification += OnNotification; Active = 0; toasts.Clear(); Log.Clear(); WantSurface = true; CommandOpen = false; };
        }

        void OnNotification(string text, NotificationLevel level)
        {
            var t = new Toast { Text = text, Level = level, Time = Time.unscaledTime };
            toasts.Add(t);
            if (toasts.Count > 5) toasts.RemoveAt(0);
            Log.Insert(0, t);
            if (Log.Count > 60) Log.RemoveAt(Log.Count - 1);
        }

        public void Notify(string text, NotificationLevel level = NotificationLevel.Info) => OnNotification(text, level);

        public void Confirm(string text, Action onYes) { confirmText = text; confirmAction = onYes; }

        public void Open<T>() where T : Panel
        {
            for (int i = 0; i < panels.Length; i++) if (panels[i] is T) Active = i;
        }

        void OnGUI()
        {
            Theme.Ensure();
            var ev = Event.current;
            if (GC.Perf != null)
            {
                if (ev.isKey || ev.isMouse || ev.type == EventType.ScrollWheel) GC.Perf.NotifyInput();
                GC.Perf.IdleAllowed = Surface == null || !Surface.Active;
            }
            float userScale = Sim != null ? Sim.State.Settings.UiScale : 1;
            Scale = Mathf.Clamp(Screen.height / 900f, 0.55f, 3f) * userScale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1));
            VW = Screen.width / Scale; VH = Screen.height / Scale;

            if (Sim == null)
            {
                View.ViewportLeft = Mathf.Min(0.42f, (Mathf.Min(560, VW * 0.4f) + 30) / VW);
                menu.Draw(new Rect(0, 0, VW, VH));
                DrawConfirm();
                DrawTooltip();
                return;
            }

            if (!panels[Active].IsUnlocked) Active = 0;
            var panel = panels[Active];
            // Panels that need the orbital/galaxy view take the player off the surface.
            if (CommandOpen && panel.ViewMode != SpaceView.ViewMode.Location && Surface != null && Surface.Active) WantSurface = false;
            SyncSurface();
            if (Surface != null && Surface.Active && !CommandOpen)
            {
                Surface.SurfaceInput_SetBlocked(!string.IsNullOrEmpty(confirmText) || GC.PendingOfflineReport != null || GC.PendingWarnings.Count > 0 || locationMenuOpen);
                HandleSurfaceKeys();
                DrawTopBar();
                hud.Draw(Surface);
                DrawObjective();
                DrawToasts();
                DrawLocationMenu();
                DrawOfflineReport();
                DrawWarnings();
                DrawConfirm();
                DrawTooltip();
                Surface.SurfaceInput_Feed(Event.current, Scale);
                return;
            }
            if (Surface != null && Surface.Active) { Surface.SurfaceInput_SetBlocked(true); HandleSurfaceKeys(); }
            float width = Mathf.Min(panel.PreferredWidth, VW - NavW - Gap * 3 - 220);
            PanelRect = new Rect(NavW + Gap * 2, TopH + Gap, Mathf.Max(320, width), VH - TopH - Gap * 2);
            View.ViewportLeft = (PanelRect.xMax + Gap) / VW;
            if (!View.Suspended) View.SetMode(panel.ViewMode);

            HandleKeys();
            DrawTopBar();
            DrawNav();
            GUI.Box(PanelRect, GUIContent.none, Theme.Box);
            var inner = new Rect(PanelRect.x + 12, PanelRect.y + 10, PanelRect.width - 24, PanelRect.height - 20);
            try { panel.Draw(inner); }
            catch (ExitGUIException) { throw; }
            catch (Exception e) { Debug.LogException(e); }
            DrawObjective();
            DrawMapLabels();
            DrawToasts();
            HandleViewInput();
            DrawLocationMenu();
            DrawOfflineReport();
            DrawWarnings();
            DrawConfirm();
            DrawTooltip();
        }

        void SyncSurface()
        {
            if (Surface == null || Event.current.type != EventType.Layout) return;
            bool want = WantSurface && Sim != null;
            if (want && (!Surface.Active || Surface.LocationId != Sim.State.CurrentLocation)) Surface.Enter(Sim, Sim.State.CurrentLocation);
            else if (!want && Surface.Active) Surface.Exit();
            View.Suspended = Surface.Active;
        }

        void HandleSurfaceKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0) return;
            // Reset held keys on every mode switch: a key released while the command center is open would otherwise stick.
            if (e.keyCode == KeyCode.Tab) { CommandOpen = !CommandOpen; Surface.SurfaceInput_Clear(); e.Use(); Gx.Changed(); }
            else if (e.keyCode == KeyCode.M) { WantSurface = !WantSurface; CommandOpen = false; Surface.SurfaceInput_Clear(); e.Use(); Gx.Changed(); }
            else if (e.keyCode == KeyCode.Escape && CommandOpen) { CommandOpen = false; Surface.SurfaceInput_Clear(); e.Use(); Gx.Changed(); }
        }

        void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0) return;
            if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9) { int i = e.keyCode - KeyCode.Alpha1; if (i < panels.Length && panels[i].IsUnlocked) Active = i; e.Use(); Gx.Changed(); }
            else if (e.keyCode == KeyCode.Alpha0 && panels[9].IsUnlocked) { Active = 9; e.Use(); Gx.Changed(); }
            else if (e.keyCode == KeyCode.F5) { GC.Save(); Notify("Game saved", NotificationLevel.Success); e.Use(); }
        }

        void DrawTopBar()
        {
            var r = new Rect(0, 0, VW, TopH);
            GUI.Box(r, GUIContent.none, Theme.Glass);
            Gx.Fill(new Rect(0, TopH - 2, VW * 0.35f, 2), Theme.Cyan * new Color(1, 1, 1, 0.7f));
            Gx.Fill(new Rect(VW * 0.35f, TopH - 1, VW * 0.65f, 1), Theme.Border);
            GUI.Label(new Rect(18, 6, 300, 30), "STELLAR <color=#FF963C>INDUSTRIES</color>", Theme.Title);
            GUI.Label(new Rect(19, 35, 300, 20), Sim.State.SaveName + "  ·  " + NumberFormat.Duration(Sim.State.PlayTime), Theme.DimLabel);

            // Location selector (dropdown - scales to any number of worlds)
            float x = 300;
            var cur = Sim.Db.Location(Sim.State.CurrentLocation);
            var sys = cur.StarSystem != "sol" ? Sim.Db.StarSystem(cur.StarSystem)?.Name + " · " : "";
            // Two lines (name / system + world count) so long star-system names never wrap or overflow.
            var selRect = new Rect(x, 9, 210, 44);
            if (GUI.Button(selRect, new GUIContent("", "Switch location"), Theme.NavActive)) { locationMenuOpen = !locationMenuOpen; Gx.Changed(); }
            GUI.Label(new Rect(x + 14, 10, 160, 24), "<b>" + cur.Name + "</b>", Theme.Label);
            GUI.Label(new Rect(x + 14, 30, 180, 18), "<color=#8498B0>" + (sys.Length > 0 ? sys : "Sol · ") + Sim.State.UnlockedLocations.Count + " worlds</color>", Theme.Small);
            GUI.Label(new Rect(selRect.xMax - 26, 18, 20, 22), locationMenuOpen ? "^" : "v", Theme.Label);
            x += 226;

            var loc = Sim.State.Location(Sim.State.CurrentLocation);
            double sup = loc?.EnergySupply ?? 0, dem = loc?.EnergyDemand ?? 0;
            Color ec = dem <= sup + 1e-6 ? Theme.Good : (loc.EnergySatisfaction > 0.5 ? Theme.Warn : Theme.Bad);
            x = Stat(x, 150, "ENERGY", Gx.Colored(NumberFormat.Short(sup) + " / " + NumberFormat.Short(dem) + " MW", ec));
            double sciRate = 0;
            foreach (var l in Sim.State.Locations.Values) if (l.Rates != null && l.Rates.TryGetValue(ResearchSystem.ScienceId, out var sr)) sciRate += sr;
            if (Performance.GraphicsQuality.ShowFps && GC.Perf != null)
                GUI.Label(new Rect(VW - 390, 40, 80, 18), "<size=11><color=#8498B0>" + GC.Perf.Fps.ToString("0") + " fps</color></size>", Theme.Small);
            x = Stat(x, 150, "SCIENCE", Gx.Colored(NumberFormat.Short(Sim.Inventory.Get("earth", ResearchSystem.ScienceId)), Theme.Cyan) + "  " + Gx.Colored(NumberFormat.Rate(sciRate), Theme.Dim));
            var active = Sim.Db.Tech(Sim.State.Research.ActiveTech);
            GUI.Label(new Rect(x, 9, 220, 16), "RESEARCH", Theme.DimLabel);
            if (active != null) Gx.Bar(new Rect(x, 30, 210, 18), (float)Sim.Research.Fraction, new Color(0.25f, 0.8f, 1f, 0.9f), active.Name);
            else
            {
                // Explain *why* research is idle: usually a material cost that is not available on Earth yet.
                var waiting = Sim.State.Research.Queue.Select(q => Sim.Db.Tech(q)).FirstOrDefault(t => t != null);
                string why = waiting != null ? Sim.Inventory.MissingText(Sim.Db.Start.StartLocation, waiting.MaterialCost) : "Open the Research tree";
                GUI.Label(new Rect(x, 27, 210, 22), new GUIContent(Gx.Colored(waiting != null ? "waiting for materials" : "idle - choose a project", Theme.Warn), (waiting != null ? waiting.Name + " - " : "") + why), Theme.Label);
            }
            x += 228;
            if (x + 120 < VW - 310) Stat(x, 120, "MISSIONS", Sim.State.ActiveMissions.Count + " in flight");
            if (GUI.Button(new Rect(VW - 86, 14, 72, 34), new GUIContent("Save", "Save now (F5)"), Theme.Button)) { GC.Save(); Notify("Game saved", NotificationLevel.Success); }
            bool onSurface = Surface != null && Surface.Active;
            if (Surface != null && GUI.Button(new Rect(VW - 300, 14, 120, 34), new GUIContent(onSurface ? "Orbit view" : "Go to surface", "Hotkey M"), onSurface ? Theme.Button : Theme.Accent))
            { WantSurface = !onSurface; CommandOpen = false; Gx.Changed(); }
            if (onSurface && GUI.Button(new Rect(VW - 172, 14, 80, 34), new GUIContent(CommandOpen ? "Close" : "Command", "All management screens (Tab)"), CommandOpen ? Theme.NavActive : Theme.Button))
            { CommandOpen = !CommandOpen; Surface.SurfaceInput_Clear(); Gx.Changed(); }
        }

        float Stat(float x, float w, string label, string value)
        {
            GUI.Label(new Rect(x, 9, w, 16), label, Theme.DimLabel);
            GUI.Label(new Rect(x, 26, w, 24), value, Theme.Label);
            return x + w + 10;
        }

        void DrawLocationMenu()
        {
            if (!locationMenuOpen) return;
            var ids = Sim.State.UnlockedLocations;
            var r = new Rect(300, TopH - 4, 260, 18 + ids.Count * 38);
            GUI.Box(r, GUIContent.none, Theme.Box);
            float y = r.y + 10;
            string lastSys = null;
            foreach (var id in ids)
            {
                var def = Sim.Db.Location(id);
                if (def.StarSystem != lastSys) { lastSys = def.StarSystem; }
                bool cur = Sim.State.CurrentLocation == id;
                var label = def.Name + "  <size=11><color=#8498B0>" + (Sim.Db.StarSystem(def.StarSystem)?.Name ?? "") + " · " + Sim.State.Buildings.Count(b => b.LocationId == id) + " facilities</color></size>";
                if (GUI.Button(new Rect(r.x + 8, y, r.width - 16, 34), label, cur ? Theme.NavActive : Theme.Nav))
                { Sim.State.CurrentLocation = id; locationMenuOpen = false; Gx.Changed(); }
                y += 38;
            }
            var e = Event.current;
            if (e.type == EventType.MouseDown && !r.Contains(e.mousePosition) && !new Rect(300, 11, 210, 40).Contains(e.mousePosition))
            { locationMenuOpen = false; e.Use(); }
        }

        void DrawNav()
        {
            var r = new Rect(Gap, TopH + Gap, NavW, VH - TopH - Gap * 2);
            GUI.Box(r, GUIContent.none, Theme.Box);
            float y = r.y + 12;
            for (int i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                bool unlocked = p.IsUnlocked;
                string key = i == 9 ? "0" : (i + 1).ToString();
                var br = new Rect(r.x + 8, y, NavW - 16, 38);
                var content = new GUIContent((unlocked ? "" : "<color=#56657A>") + p.Title + (unlocked ? "" : "</color>"), unlocked ? "Hotkey " + key : p.LockedHint);
                bool old = GUI.enabled; GUI.enabled = unlocked;
                if (GUI.Button(br, content, i == Active ? Theme.NavActive : Theme.Nav)) { Active = i; Gx.Changed(); }
                GUI.enabled = old;
                if (i == Active) Gx.RoundFill(new Rect(br.x + 4, br.y + 9, 4, br.height - 18), Theme.Cyan);
                if (!unlocked) GUI.Label(new Rect(br.xMax - 46, br.y + 10, 40, 18), "<size=10><color=#56657A>LOCKED</color></size>", Theme.Small);
                if (!unlocked && Event.current.type == EventType.Repaint && br.Contains(Event.current.mousePosition)) GUI.tooltip = p.LockedHint;
                y += 42;
            }
            GUI.Label(new Rect(r.x + 14, r.yMax - 44, NavW - 24, 40), "Drag: rotate view\nWheel: zoom · F5: save", Theme.DimLabel);
        }

        void DrawObjective()
        {
            if (!Sim.State.Settings.ShowHints) return;
            var obj = Sim.Achievements.CurrentObjective;
            if (obj == null) return;
            float w = 330, h = 84;
            var r = new Rect(VW - w - Gap, VH - h - Gap, w, h);
            GUI.Box(r, GUIContent.none, Theme.Box);
            Gx.RoundFill(new Rect(r.x + 6, r.y + 12, 4, h - 24), Theme.Orange);
            GUI.Label(new Rect(r.x + 18, r.y + 7, w - 30, 18), "CURRENT OBJECTIVE", Theme.DimLabel);
            GUI.Label(new Rect(r.x + 18, r.y + 24, w - 30, 22), Gx.Colored(obj.Name, Theme.Orange) + "  <size=12>" + obj.Description + "</size>", Theme.Label);
            double p = Sim.Achievements.Progress(obj);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
            Gx.Bar(new Rect(r.x + 18, r.y + 54, w - 34, 16), (float)(p / Math.Max(1e-9, obj.Threshold)), Theme.Orange * new Color(pulse, pulse, pulse, 1),
                NumberFormat.Short(Math.Min(p, obj.Threshold)) + " / " + NumberFormat.Short(obj.Threshold));
        }

        void DrawToasts()
        {
            float y = TopH + Gap, w = 340;
            for (int i = toasts.Count - 1; i >= 0; i--) if (Time.unscaledTime - toasts[i].Time > 7f) toasts.RemoveAt(i);
            foreach (var t in toasts)
            {
                float age = Time.unscaledTime - t.Time;
                float a = Mathf.Clamp01(Mathf.Min(age * 4, (7f - age) * 1.5f));
                float slide = (1 - Mathf.Clamp01(age * 5)) * 40;
                var r = new Rect(VW - w - Gap + slide, y, w, 46);
                var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
                GUI.Box(r, GUIContent.none, Theme.Box);
                Gx.RoundFill(new Rect(r.x + 6, r.y + 10, 4, r.height - 20), Gx.LevelColor(t.Level));
                GUI.Label(new Rect(r.x + 18, r.y + 4, w - 26, 40), t.Text, Theme.Wrap);
                GUI.color = old;
                y += 52;
            }
        }

        void DrawMapLabels()
        {
            if (View.Mode == SpaceView.ViewMode.Location) return;
            View.CollectLabels(labels);
            foreach (var l in labels)
            {
                var p = new Vector2(l.Screen.x / Scale, (Screen.height - l.Screen.y) / Scale);
                if (p.x < PanelRect.xMax) continue;
                if (labelStyle == null) labelStyle = new GUIStyle(Theme.Small) { alignment = TextAnchor.LowerCenter };
                GUI.Label(new Rect(p.x - 90, p.y - 20, 180, 18), l.Unlocked ? Gx.Colored(l.Name, Theme.Cyan) : Gx.Colored(l.Name + " (unexplored)", Theme.Dim), labelStyle);
            }
        }

        bool OverUi(Vector2 p)
        {
            if (p.y < TopH || p.x < PanelRect.xMax + Gap) return true;
            if (locationMenuOpen && p.x < 570 && p.y < TopH + 20 + Sim.State.UnlockedLocations.Count * 38) return true;
            if (p.x > VW - 360 && p.y < TopH + Gap + toasts.Count * 52) return true;
            return Sim.State.Settings.ShowHints && p.x > VW - 345 && p.y > VH - 100;
        }

        void HandleViewInput()
        {
            var e = Event.current;
            if (View.Suspended) return;
            if (!string.IsNullOrEmpty(confirmText) || GC.PendingOfflineReport != null) return;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!OverUi(e.mousePosition)) { dragging = true; dragStart = e.mousePosition; }
                    break;
                case EventType.MouseDrag:
                    if (dragging) { View.Orbit(e.delta); e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (dragging && (e.mousePosition - dragStart).sqrMagnitude < 16 && e.button == 0)
                    {
                        var id = View.Pick(new Vector2(e.mousePosition.x * Scale, Screen.height - e.mousePosition.y * Scale));
                        if (id != null) SelectBody(id);
                    }
                    dragging = false;
                    break;
                case EventType.ScrollWheel:
                    if (!OverUi(e.mousePosition)) { View.Zoom(e.delta.y); e.Use(); }
                    break;
            }
        }

        /// <summary>Star system selected in the galaxy view (read by the map panel).</summary>
        public string SelectedStarSystem = "alpha_centauri";

        public void SelectBody(string id)
        {
            if (id.StartsWith("sys:")) { SelectedStarSystem = id.Substring(4); View.FocusBody = id; return; }
            View.FocusBody = id;
            if (Sim.State.IsUnlocked(id)) Sim.State.CurrentLocation = id;
            else Notify(Sim.Db.Location(id)?.Name + " has not been reached yet. Check the Missions screen.", NotificationLevel.Info);
        }

        void DrawOfflineReport()
        {
            var rep = GC.PendingOfflineReport;
            if (rep == null) return;
            var r = new Rect(VW / 2 - 290, VH / 2 - 240, 580, 480);
            GUI.ModalWindow(9001, r, id =>
            {
                GUILayout.Space(14);
                GUILayout.Label("Welcome back, Director", Theme.Title);
                Gx.HLine();
                if (rep.ClockAnomaly) GUILayout.Label(Gx.Colored("The system clock moved backwards. No offline progress was granted.", Theme.Warn), Theme.Wrap);
                GUILayout.Label($"Away for {NumberFormat.Duration(rep.RequestedSeconds)}. Simulated {NumberFormat.Duration(rep.SimulatedSeconds)} at {NumberFormat.Percent(rep.Efficiency)} efficiency" +
                                (rep.RequestedSeconds > rep.SimulatedSeconds + 1 ? " (offline cap reached - research Offline Operations to extend it)." : "."), Theme.Wrap);
                modalScroll = GUILayout.BeginScrollView(modalScroll, GUILayout.Height(270));
                if (rep.CompletedResearch.Count > 0) { GUILayout.Label("Research completed", Theme.Header); foreach (var t in rep.CompletedResearch) GUILayout.Label("  " + t, Theme.Label); }
                if (rep.MissionResults.Count > 0) { GUILayout.Label("Missions", Theme.Header); foreach (var t in rep.MissionResults) GUILayout.Label("  " + t, Theme.Label); }
                GUILayout.Label("Resource changes", Theme.Header);
                foreach (var kv in rep.Changes.OrderByDescending(k => k.Value))
                {
                    var parts = kv.Key.Split('/');
                    string where = parts[0] == "global" ? "" : (Sim.Db.Location(parts[0])?.Name ?? parts[0]) + ": ";
                    GUILayout.Label("  " + where + Sim.Db.ResourceName(parts[1]) + "  " + Gx.Colored((kv.Value > 0 ? "+" : "") + NumberFormat.Short(kv.Value), kv.Value > 0 ? Theme.Good : Theme.Bad), Theme.Label);
                }
                GUILayout.EndScrollView();
                if (GUILayout.Button("Continue", Theme.Accent, GUILayout.Height(34))) { GC.PendingOfflineReport = null; Gx.Changed(); }
            }, "", Theme.Box);
        }

        void DrawWarnings()
        {
            if (GC.PendingWarnings.Count == 0 || GC.PendingOfflineReport != null) return;
            var r = new Rect(VW / 2 - 240, VH / 2 - 150, 480, 300);
            GUI.ModalWindow(9002, r, id =>
            {
                GUILayout.Label("Save repaired", Theme.Title);
                GUILayout.Label("The save was loaded, but some data had to be repaired:", Theme.Wrap);
                modalScroll = GUILayout.BeginScrollView(modalScroll, GUILayout.Height(170));
                foreach (var w in GC.PendingWarnings) GUILayout.Label("• " + w, Theme.Wrap);
                GUILayout.EndScrollView();
                if (GUILayout.Button("OK", Theme.Accent, GUILayout.Height(32))) { GC.PendingWarnings.Clear(); Gx.Changed(); }
            }, "", Theme.Box);
        }

        void DrawConfirm()
        {
            if (string.IsNullOrEmpty(confirmText)) return;
            var r = new Rect(VW / 2 - 200, VH / 2 - 80, 400, 160);
            GUI.ModalWindow(9003, r, id =>
            {
                GUILayout.Space(10);
                GUILayout.Label(confirmText, Theme.Wrap);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Cancel", Theme.Button, GUILayout.Height(32))) { confirmText = null; Gx.Changed(); }
                if (GUILayout.Button("Confirm", Theme.Accent, GUILayout.Height(32))) { var a = confirmAction; confirmText = null; a?.Invoke(); Gx.Changed(); }
                GUILayout.EndHorizontal();
            }, "", Theme.Box);
        }

        void DrawTooltip()
        {
            if (string.IsNullOrEmpty(GUI.tooltip)) return;
            var content = new GUIContent(GUI.tooltip);
            float w = Mathf.Min(340, Theme.Tooltip.CalcSize(content).x + 4);
            float h = Theme.Tooltip.CalcHeight(content, w);
            var m = Event.current.mousePosition;
            var r = new Rect(Mathf.Min(m.x + 16, VW - w - 4), Mathf.Min(m.y + 18, VH - h - 4), w, h);
            GUI.Label(r, content, Theme.Tooltip);
        }
    }
}
