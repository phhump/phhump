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
        Vector2 modalScroll;
        bool dragging; Vector2 dragStart;
        readonly List<SpaceView.Label> labels = new List<SpaceView.Label>();
        const float TopH = 52, NavW = 150, Gap = 8;

        public void Init(GameController gc, SpaceView view)
        {
            GC = gc; View = view;
            menu = new MainMenu(this);
            panels = new Panel[]
            {
                new OverviewPanel(this), new IndustriesPanel(this), new ResourcesPanel(this), new ResearchPanel(this),
                new SpacecraftPanel(this), new MissionsPanel(this), new LogisticsPanel(this), new MapPanel(this),
                new StatisticsPanel(this), new SettingsPanel(this),
            };
            gc.SimulationStarted += s => { s.Events.Notification += OnNotification; Active = 0; toasts.Clear(); Log.Clear(); };
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
            float userScale = Sim != null ? Sim.State.Settings.UiScale : 1;
            Scale = Mathf.Clamp(Screen.height / 900f, 0.55f, 3f) * userScale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1));
            VW = Screen.width / Scale; VH = Screen.height / Scale;

            if (Sim == null)
            {
                View.ViewportLeft = 0;
                menu.Draw(new Rect(0, 0, VW, VH));
                DrawConfirm();
                DrawTooltip();
                return;
            }

            if (!panels[Active].IsUnlocked) Active = 0;
            var panel = panels[Active];
            float width = Mathf.Min(panel.PreferredWidth, VW - NavW - Gap * 3 - 220);
            PanelRect = new Rect(NavW + Gap * 2, TopH + Gap, Mathf.Max(320, width), VH - TopH - Gap * 2);
            View.ViewportLeft = (PanelRect.xMax + Gap) / VW;
            View.SetMode(panel.ViewMode);

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
            DrawOfflineReport();
            DrawWarnings();
            DrawConfirm();
            DrawTooltip();
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
            Gx.Fill(r, new Color(0.02f, 0.04f, 0.08f, 0.94f));
            Gx.Fill(new Rect(0, TopH - 1, VW, 1), Theme.Border);
            GUI.Label(new Rect(14, 6, 260, 22), "STELLAR INDUSTRIES", Theme.Title);
            GUI.Label(new Rect(14, 27, 260, 18), Sim.State.SaveName + "  ·  " + NumberFormat.Duration(Sim.State.PlayTime), Theme.DimLabel);

            float x = 270;
            foreach (var id in Sim.State.UnlockedLocations)
            {
                var def = Sim.Db.Location(id);
                bool cur = Sim.State.CurrentLocation == id;
                if (GUI.Button(new Rect(x, 10, 92, 32), def.Name, cur ? Theme.NavActive : Theme.Nav)) { Sim.State.CurrentLocation = id; Gx.Changed(); }
                x += 96;
            }
            x += 12;
            var loc = Sim.State.Location(Sim.State.CurrentLocation);
            double sup = loc?.EnergySupply ?? 0, dem = loc?.EnergyDemand ?? 0;
            Color ec = dem <= sup + 1e-6 ? Theme.Good : (loc.EnergySatisfaction > 0.5 ? Theme.Warn : Theme.Bad);
            GUI.Label(new Rect(x, 8, 170, 18), "ENERGY", Theme.DimLabel);
            GUI.Label(new Rect(x, 24, 170, 20), Gx.Colored(NumberFormat.Short(sup) + " / " + NumberFormat.Short(dem) + " MW", ec), Theme.Label);
            x += 150;
            GUI.Label(new Rect(x, 8, 170, 18), "SCIENCE", Theme.DimLabel);
            double sciRate = 0;
            foreach (var l in Sim.State.Locations.Values) if (l.Rates != null && l.Rates.TryGetValue(ResearchSystem.ScienceId, out var sr)) sciRate += sr;
            GUI.Label(new Rect(x, 24, 170, 20), Gx.Colored(NumberFormat.Short(Sim.Inventory.Get("earth", ResearchSystem.ScienceId)), Theme.Cyan) + "  " + Gx.Colored(NumberFormat.Rate(sciRate), Theme.Dim), Theme.Label);
            x += 150;
            var active = Sim.Db.Tech(Sim.State.Research.ActiveTech);
            GUI.Label(new Rect(x, 8, 220, 18), "RESEARCH", Theme.DimLabel);
            if (active != null) Gx.Bar(new Rect(x, 27, 190, 15), (float)Sim.Research.Fraction, new Color(0.17f, 0.85f, 1f, 0.75f), active.Name);
            else GUI.Label(new Rect(x, 24, 200, 20), Gx.Colored("idle", Theme.Warn), Theme.Label);
            x += 205;
            if (x + 120 < VW)
            {
                GUI.Label(new Rect(x, 8, 120, 18), "MISSIONS", Theme.DimLabel);
                GUI.Label(new Rect(x, 24, 120, 20), Sim.State.ActiveMissions.Count + " in flight", Theme.Label);
            }
            if (GUI.Button(new Rect(VW - 74, 12, 62, 28), new GUIContent("Save", "Save now (F5)"), Theme.Button)) { GC.Save(); Notify("Game saved", NotificationLevel.Success); }
        }

        void DrawNav()
        {
            var r = new Rect(Gap, TopH + Gap, NavW, VH - TopH - Gap * 2);
            GUI.Box(r, GUIContent.none, Theme.Box);
            float y = r.y + 10;
            for (int i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                bool unlocked = p.IsUnlocked;
                string key = i == 9 ? "0" : (i + 1).ToString();
                var content = new GUIContent((unlocked ? "" : "[locked] ") + p.Title, unlocked ? "Hotkey " + key : p.LockedHint);
                bool old = GUI.enabled; GUI.enabled = unlocked;
                if (GUI.Button(new Rect(r.x + 8, y, NavW - 16, 34), content, i == Active ? Theme.NavActive : Theme.Nav)) { Active = i; Gx.Changed(); }
                GUI.enabled = old;
                if (!unlocked && Event.current.type == EventType.Repaint && new Rect(r.x + 8, y, NavW - 16, 34).Contains(Event.current.mousePosition))
                    GUI.tooltip = p.LockedHint;
                y += 38;
            }
            GUI.Label(new Rect(r.x + 10, r.yMax - 40, NavW - 20, 36), "Drag: rotate view\nWheel: zoom", Theme.DimLabel);
        }

        void DrawObjective()
        {
            if (!Sim.State.Settings.ShowHints) return;
            var obj = Sim.Achievements.CurrentObjective;
            if (obj == null) return;
            float w = 300, h = 66;
            var r = new Rect(VW - w - Gap, VH - h - Gap, w, h);
            GUI.Box(r, GUIContent.none, Theme.Box);
            GUI.Label(new Rect(r.x + 10, r.y + 6, w - 20, 18), "OBJECTIVE", Theme.DimLabel);
            GUI.Label(new Rect(r.x + 10, r.y + 20, w - 20, 20), Gx.Colored(obj.Name, Theme.Orange) + "  " + obj.Description, Theme.Small);
            double p = Sim.Achievements.Progress(obj);
            Gx.Bar(new Rect(r.x + 10, r.y + 44, w - 20, 12), (float)(p / Math.Max(1e-9, obj.Threshold)), Theme.Orange, NumberFormat.Short(Math.Min(p, obj.Threshold)) + " / " + NumberFormat.Short(obj.Threshold));
        }

        void DrawToasts()
        {
            float y = TopH + Gap, w = 320;
            for (int i = toasts.Count - 1; i >= 0; i--) if (Time.unscaledTime - toasts[i].Time > 7f) toasts.RemoveAt(i);
            foreach (var t in toasts)
            {
                float age = Time.unscaledTime - t.Time;
                float a = Mathf.Clamp01(Mathf.Min(age * 4, (7f - age) * 1.5f));
                var r = new Rect(VW - w - Gap, y, w, 40);
                var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
                GUI.Box(r, GUIContent.none, Theme.Box);
                Gx.Fill(new Rect(r.x, r.y, 3, r.height), Gx.LevelColor(t.Level));
                GUI.Label(new Rect(r.x + 10, r.y + 4, w - 16, 34), t.Text, Theme.Wrap);
                GUI.color = old;
                y += 44;
            }
        }

        void DrawMapLabels()
        {
            if (View.Mode != SpaceView.ViewMode.Map) return;
            View.CollectLabels(labels);
            foreach (var l in labels)
            {
                var p = new Vector2(l.Screen.x / Scale, (Screen.height - l.Screen.y) / Scale);
                if (p.x < PanelRect.xMax) continue;
                var s = new GUIStyle(Theme.Small) { alignment = TextAnchor.LowerCenter };
                GUI.Label(new Rect(p.x - 60, p.y - 20, 120, 18), l.Unlocked ? Gx.Colored(l.Name, Theme.Cyan) : Gx.Colored(l.Name + " (unexplored)", Theme.Dim), s);
            }
        }

        bool OverUi(Vector2 p)
        {
            if (p.y < TopH || p.x < PanelRect.xMax + Gap) return true;
            if (p.x > VW - 330 && p.y < TopH + Gap + toasts.Count * 44) return true;
            return Sim.State.Settings.ShowHints && p.x > VW - 310 && p.y > VH - 80;
        }

        void HandleViewInput()
        {
            var e = Event.current;
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

        public void SelectBody(string id)
        {
            View.FocusBody = id;
            if (Sim.State.IsUnlocked(id)) Sim.State.CurrentLocation = id;
            else Notify(Sim.Db.Location(id)?.Name + " has not been reached yet. Check the Missions screen.", NotificationLevel.Info);
        }

        void DrawOfflineReport()
        {
            var rep = GC.PendingOfflineReport;
            if (rep == null) return;
            var r = new Rect(VW / 2 - 260, VH / 2 - 220, 520, 440);
            GUI.ModalWindow(9001, r, id =>
            {
                GUILayout.Space(8);
                GUILayout.Label("Welcome back, Director", Theme.Title);
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
