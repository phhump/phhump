using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Util;
using StellarIndustries.Unity.Surface;
using UnityEngine;

namespace StellarIndustries.Unity.UI
{
    /// <summary>HUD for the engineer on the surface: prompts, build palette, building panel, minimap, resources.</summary>
    public class SurfaceHud
    {
        readonly GameUI ui;
        Vector2 paletteScroll;
        public SurfaceHud(GameUI ui) { this.ui = ui; }

        // Cached styles and data: OnGUI runs several times per frame, so allocations here add up quickly.
        GUIStyle centerSmall, rightSmall, floatStyle, wrapSmall;
        System.Collections.Generic.List<ResourceDef> resCache = new System.Collections.Generic.List<ResourceDef>();
        float resTime = -1;
        string resLoc;

        void EnsureStyles()
        {
            if (centerSmall != null) return;
            centerSmall = new GUIStyle(Theme.Small) { alignment = TextAnchor.MiddleCenter };
            rightSmall = new GUIStyle(Theme.Small) { alignment = TextAnchor.MiddleRight };
            floatStyle = new GUIStyle(Theme.Label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            wrapSmall = new GUIStyle(Theme.Small) { wordWrap = true };
        }

        public void Draw(SurfaceWorld w)
        {
            var sim = ui.Sim;
            float VW = ui.VW, VH = ui.VH;
            EnsureStyles();
            DrawResources(w, sim);
            DrawMinimap(w, sim, new Rect(10, VH - 286, 220, 220));
            DrawFloatingTexts(w);

            // Interaction prompt
            if (!string.IsNullOrEmpty(w.Prompt) && w.Build.Selected == null)
            {
                var size = Theme.Label.CalcSize(new GUIContent(w.Prompt));
                var r = new Rect(VW / 2 - size.x / 2 - 18, VH - 168, size.x + 36, 40);
                GUI.Box(r, GUIContent.none, Theme.Box);
                GUI.Label(new Rect(r.x + 18, r.y + 9, size.x + 4, 24), w.Prompt, Theme.Label);
            }
            if (w.FieldUnderEngineer.HasValue && w.Build.Selected == null)
            {
                var f = w.FieldUnderEngineer.Value;
                GUI.Label(new Rect(VW / 2 - 200, VH - 196, 400, 22), "<color=#8498B0>Standing on a</color> " + sim.Db.ResourceName(f.ResourceId) + " <color=#8498B0>field · richness " + NumberFormat.Percent(f.Richness) + "</color>", centerSmall);
            }

            // Build mode feedback
            if (w.Build.Selected != null)
            {
                var def = w.Build.Selected;
                string txt = w.Build.HasPoint ? (w.Build.Problem == null ? Gx.Colored("Left click: build " + def.Name, Theme.Good) + "   R: rotate   Esc: cancel" : Gx.Colored(w.Build.Problem, Theme.Bad) + "   Esc: cancel")
                                               : "Point at the ground to place " + def.Name;
                var size = Theme.Label.CalcSize(new GUIContent(txt));
                var r = new Rect(VW / 2 - size.x / 2 - 18, VH - 168, size.x + 36, 40);
                GUI.Box(r, GUIContent.none, Theme.Box);
                GUI.Label(new Rect(r.x + 18, r.y + 9, size.x + 4, 24), txt, Theme.Label);
                GUI.Label(new Rect(VW / 2 - 300, VH - 196, 600, 22), "Cost: " + Fmt.Cost(sim, w.LocationId, sim.Construction.BuildCost(def, w.LocationId)), centerSmall);
            }
            if (w.Engineer.HasJetpack && !w.Engineer.InRover && w.Engineer.JetFuel < 0.999f)
            {
                GUI.Label(new Rect(VW / 2 - 120, VH - 232, 240, 18), "<size=11><color=#8498B0>JETPACK (hold Space in the air)</color></size>", centerSmall);
                Gx.Bar(new Rect(VW / 2 - 100, VH - 212, 200, 10), w.Engineer.JetFuel, new Color(0.45f, 0.85f, 1f, 0.9f));
            }
            if (w.Build.PaletteOpen) DrawPalette(w, sim);
            if (w.OpenPanelUid != 0) DrawBuildingPanel(w, sim);
            DrawControls(w);
        }

        void DrawControls(SurfaceWorld w)
        {
            string c = w.Engineer.InRover
                ? "W/S drive · A/D steer · B build (long range) · E leave rover · Right-drag camera · Wheel zoom · Tab command center · M orbit view"
                : "WASD move · Shift sprint · Space jump · B build · E interact · F drill · Right-drag camera · Tab command center · M orbit view";
            var r = new Rect(ui.VW / 2 - 430, ui.VH - 46, 860, 34);
            GUI.Box(r, GUIContent.none, Theme.Box);
            GUI.Label(r, c, centerSmall);
            var g = w.Def;
            GUI.Label(new Rect(16, ui.VH - 58, 420, 30), "<b>" + g.Name + "</b>  <color=#8498B0>" + g.Gravity.ToString("0.00") + " g · " + g.TemperatureC.ToString("0") + " °C · " + (g.Atmosphere ?? "") + "</color>", Theme.Label);
        }

        void DrawResources(SurfaceWorld w, Core.Simulation.GameSimulation sim)
        {
            var loc = sim.State.Location(w.LocationId);
            if (Time.unscaledTime - resTime > 0.25f || resLoc != w.LocationId)
            {
                resTime = Time.unscaledTime; resLoc = w.LocationId;
                resCache = sim.Db.Resources.Where(r => !r.IsGlobal && sim.Inventory.Get(w.LocationId, r.Id) >= 1).OrderBy(r => r.Category).Take(14).ToList();
            }
            var items = resCache;
            float h = 16 + items.Count * 22;
            var r = new Rect(10, 74, 230, h);
            GUI.Box(r, GUIContent.none, Theme.Box);
            float y = r.y + 8;
            foreach (var res in items)
            {
                var c = Fmt.ResColor(sim.Db, res.Id);
                if (Event.current.type == EventType.Repaint) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(r.x + 12, y + 5, 11, 11), Views.VisualFactory.CircleTexture); GUI.color = old; }
                loc.Rates.TryGetValue(res.Id, out var rate);
                double amt = sim.Inventory.Get(w.LocationId, res.Id), cap = sim.Inventory.Capacity(w.LocationId, res.Id);
                GUI.Label(new Rect(r.x + 30, y, 120, 22), res.Name, Theme.Small);
                GUI.Label(new Rect(r.x + 120, y, 100, 22), Gx.Colored(NumberFormat.Short(amt), amt >= cap ? Theme.Warn : Theme.Text) + (rate != 0 ? Gx.Colored(" " + NumberFormat.Rate(rate), rate > 0 ? Theme.Good : Theme.Bad) : ""), rightSmall);
                y += 22;
            }
        }

        void DrawMinimap(SurfaceWorld w, Core.Simulation.GameSimulation sim, Rect r)
        {
            GUI.Box(r, GUIContent.none, Theme.Box);
            float half = (float)w.Def.SurfaceHalfSizeM;
            var inner = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20);
            Vector2 Map(double x, double z) => new Vector2(inner.x + (float)((x + half) / (2 * half)) * inner.width, inner.y + (float)((half - z) / (2 * half)) * inner.height);
            if (Event.current.type != EventType.Repaint) return;
            Gx.RoundFill(inner, new Color(0, 0, 0, 0.35f));
            var tex = Views.VisualFactory.CircleTexture;
            var old = GUI.color;
            foreach (var p in sim.Placement.Patches(w.LocationId))
            {
                var c = Fmt.ResColor(sim.Db, p.ResourceId); c.a = 0.45f; GUI.color = c;
                var m = Map(p.X, p.Z); float rr = (float)p.Radius / (2 * half) * inner.width;
                GUI.DrawTexture(new Rect(m.x - rr, m.y - rr, rr * 2, rr * 2), tex);
            }
            foreach (var b in sim.State.Buildings.Where(b => b.LocationId == w.LocationId && b.Placed))
            {
                var m = Map(b.PosX, b.PosZ);
                GUI.color = b.Status == BuildingStatus.Working ? Theme.Good : b.Status == BuildingStatus.Paused ? Theme.Dim : Theme.Warn;
                GUI.DrawTexture(new Rect(m.x - 2.5f, m.y - 2.5f, 5, 5), Theme.White);
            }
            var rov = w.Rover.transform.position; var rm = Map(rov.x, rov.z);
            GUI.color = Theme.Orange; GUI.DrawTexture(new Rect(rm.x - 3, rm.y - 3, 6, 6), tex);
            var e = w.Engineer.InRover ? rov : w.Engineer.transform.position; var em = Map(e.x, e.z);
            GUI.color = Theme.Cyan; GUI.DrawTexture(new Rect(em.x - 4.5f, em.y - 4.5f, 9, 9), tex);
            GUI.color = old;
            GUI.Label(new Rect(r.x + 12, r.yMax - 26, r.width, 18), "<size=10><color=#8498B0>" + (half * 2).ToString("0") + " m survey area</color></size>", Theme.Small);
        }

        void DrawFloatingTexts(SurfaceWorld w)
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var (text, pos, time, color) in w.FloatingTexts)
            {
                float age = Time.time - time;
                var sp = cam.WorldToScreenPoint(pos + Vector3.up * age * 1.2f);
                if (sp.z <= 0) continue;
                var p = new Vector2(sp.x / ui.Scale, (Screen.height - sp.y) / ui.Scale);
                floatStyle.normal.textColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(1.6f - age));
                GUI.Label(new Rect(p.x - 100, p.y - 12, 200, 24), "<b>" + text + "</b>", floatStyle);
            }
        }

        void DrawPalette(SurfaceWorld w, Core.Simulation.GameSimulation sim)
        {
            var defs = sim.Db.Buildings.Where(d => sim.Construction.PlacementProblem(d, w.LocationId) == null).ToList();
            var r = new Rect(ui.VW / 2 - 560, ui.VH - 330, 1120, 150);
            GUI.Box(r, GUIContent.none, Theme.Box);
            GUI.Label(new Rect(r.x + 18, r.y + 8, 400, 22), "<b>BUILD</b>  <color=#8498B0>choose a structure, then place it in the world (B / Esc to close)</color>", Theme.Label);
            paletteScroll = GUI.BeginScrollView(new Rect(r.x + 12, r.y + 34, r.width - 24, r.height - 40), paletteScroll, new Rect(0, 0, defs.Count * 168, 96));
            float x = 0;
            foreach (var d in defs)
            {
                var cr = new Rect(x, 0, 160, 92);
                bool afford = sim.Inventory.Has(w.LocationId, sim.Construction.BuildCost(d, w.LocationId));
                string power = d.PowerOutput > 0 ? Gx.Colored("+" + NumberFormat.Short(d.PowerOutput * Core.Systems.ProductionSystem.SourceFactor(d, w.Def)) + " MW", Theme.Good) : d.PowerUse > 0 ? Gx.Colored("-" + d.PowerUse + " MW", Theme.Warn) : "";
                var tip = d.Description + "\nCost: " + Fmt.Amounts(sim.Db, sim.Construction.BuildCost(d, w.LocationId)) + "\nFootprint " + d.FootprintM + " m";
                if (GUI.Button(cr, new GUIContent("", tip), afford ? Theme.Button : Theme.Nav)) { w.Build.Select(d); Gx.Changed(); }
                GUI.Label(new Rect(cr.x + 10, cr.y + 8, 150, 22), "<b>" + d.Name + "</b>", Theme.Small);
                GUI.Label(new Rect(cr.x + 10, cr.y + 30, 150, 36), "<size=10>" + Fmt.Cost(sim, w.LocationId, sim.Construction.BuildCost(d, w.LocationId)) + "</size>", wrapSmall);
                GUI.Label(new Rect(cr.x + 10, cr.y + 66, 150, 20), "<size=11>" + power + "</size>", Theme.Small);
                x += 168;
            }
            GUI.EndScrollView();
        }

        void DrawBuildingPanel(SurfaceWorld w, Core.Simulation.GameSimulation sim)
        {
            var b = sim.Construction.Find(w.OpenPanelUid);
            if (b == null) { w.OpenPanelUid = 0; return; }
            var def = sim.Db.Building(b.DefId);
            var r = new Rect(ui.VW - 430, ui.VH - 420, 420, 230);
            GUI.Box(r, GUIContent.none, Theme.Box);
            GUILayout.BeginArea(new Rect(r.x + 18, r.y + 14, r.width - 36, r.height - 24));
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><size=18>" + def.Name + "</size></b>  <color=#8498B0>Lv " + b.Level + "</color>", Theme.Label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", Theme.Button, GUILayout.Width(60))) { w.OpenPanelUid = 0; Gx.Changed(); }
            GUILayout.EndHorizontal();
            GUILayout.Label(def.Description, Theme.DimLabel);
            GUILayout.BeginHorizontal();
            var (st, sc) = Fmt.StatusPill(b.Status);
            Gx.Pill(st, sc, 100);
            if (def.PowerOutput <= 0 && def.Recipes.Count > 0) Gx.LayoutBar((float)b.Progress, new Color(0.25f, 0.8f, 1f, 0.9f), NumberFormat.Percent(b.EffectiveSpeed) + " speed", 160, 16);
            GUILayout.EndHorizontal();
            if (def.Recipes.Count > 0 && def.PowerOutput <= 0)
            {
                var rec = sim.Db.Recipe(b.RecipeId);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", Theme.Button, GUILayout.Width(28))) sim.Construction.CycleRecipe(b.Uid, -1);
                GUILayout.Label(new GUIContent(rec?.Name ?? "-", Fmt.Recipe(sim.Db, rec)), Theme.Label, GUILayout.Width(220));
                if (GUILayout.Button(">", Theme.Button, GUILayout.Width(28))) sim.Construction.CycleRecipe(b.Uid, 1);
                GUILayout.EndHorizontal();
                GUILayout.Label(Fmt.Recipe(sim.Db, rec), Theme.DimLabel);
            }
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            bool canUp = sim.Construction.CanUpgrade(b, out var why);
            if (Gx.Button("Upgrade", canUp, "Level " + (b.Level + 1) + ": " + Fmt.Amounts(sim.Db, sim.Construction.UpgradeCost(b)) + (canUp ? "" : "\n" + why), Theme.Accent, GUILayout.Width(100))) sim.Construction.Upgrade(b.Uid);
            if (GUILayout.Button(b.Enabled ? "Pause" : "Run", Theme.Button, GUILayout.Width(70))) sim.Construction.SetEnabled(b.Uid, !b.Enabled);
            if (GUILayout.Button(new[] { "Low", "Norm", "High" }[b.Priority], Theme.Button, GUILayout.Width(60))) sim.Construction.SetPriority(b.Uid, (b.Priority + 1) % 3);
            if (GUILayout.Button(new GUIContent("Demolish", "Refunds 50% of the base cost"), Theme.Button, GUILayout.Width(90)))
                ui.Confirm("Demolish this " + def.Name + "?", () => { sim.Construction.Demolish(b.Uid); w.OpenPanelUid = 0; });
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
