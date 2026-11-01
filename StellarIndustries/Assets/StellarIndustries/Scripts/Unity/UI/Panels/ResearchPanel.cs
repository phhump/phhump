using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Systems;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    /// <summary>Interactive technology tree: columns = tiers, lines = prerequisites, colors = status.</summary>
    public class ResearchPanel : Panel
    {
        const float NodeW = 184, NodeH = 60, ColW = 220, RowH = 70;
        readonly Dictionary<string, Rect> layout = new Dictionary<string, Rect>();
        Vector2 contentSize;
        string selected;

        public ResearchPanel(GameUI ui) : base(ui) { }
        public override string Title => "Research";
        public override float PreferredWidth => 1100;

        void BuildLayout()
        {
            layout.Clear();
            var rows = new Dictionary<int, int>();
            foreach (var t in Sim.Db.Techs.OrderBy(t => t.Tier))
            {
                rows.TryGetValue(t.Tier, out int row);
                rows[t.Tier] = row + 1;
                layout[t.Id] = new Rect(10 + (t.Tier - 1) * ColW, 10 + row * RowH, NodeW, NodeH);
            }
            contentSize = new Vector2(layout.Values.Max(r => r.xMax) + 20, layout.Values.Max(r => r.yMax) + 20);
        }

        static Color StatusColor(TechStatus s)
        {
            switch (s)
            {
                case TechStatus.Completed: return new Color(0.16f, 0.55f, 0.32f);
                case TechStatus.Researching: return new Color(0.10f, 0.55f, 0.75f);
                case TechStatus.Queued: return new Color(0.35f, 0.35f, 0.65f);
                case TechStatus.Available: return new Color(0.55f, 0.32f, 0.08f);
                default: return new Color(0.15f, 0.17f, 0.21f);
            }
        }

        public override void Draw(Rect r)
        {
            if (layout.Count != Sim.Db.Techs.Count) BuildLayout();
            float detailH = 170;
            GUI.Label(new Rect(r.x, r.y, r.width, 24), "Technology Tree", Theme.Title);
            float lx = r.x + 230;
            foreach (var (name, st) in new[] { ("Completed", TechStatus.Completed), ("Researching", TechStatus.Researching), ("Queued", TechStatus.Queued), ("Available", TechStatus.Available), ("Locked", TechStatus.Locked) })
            {
                Gx.RoundFill(new Rect(lx, r.y + 9, 12, 12), StatusColor(st) * 1.5f);
                GUI.Label(new Rect(lx + 16, r.y + 4, 100, 20), name, Theme.Small);
                lx += 104;
            }
            var view = new Rect(r.x, r.y + 28, r.width, r.height - detailH - 34);
            Scroll = GUI.BeginScrollView(view, Scroll, new Rect(0, 0, contentSize.x, contentSize.y));

            // Prerequisite connectors (elbow lines)
            foreach (var t in Sim.Db.Techs)
                foreach (var p in t.Prerequisites)
                {
                    if (!layout.TryGetValue(p, out var a) || !layout.TryGetValue(t.Id, out var b)) continue;
                    bool done = Sim.Research.Status(p) == TechStatus.Completed;
                    var c = done ? new Color(0.17f, 0.85f, 1f, 0.55f) : new Color(0.5f, 0.55f, 0.6f, 0.3f);
                    float x1 = a.xMax, y1 = a.center.y, x2 = b.x, y2 = b.center.y, mx = x2 - 14;
                    Gx.Fill(new Rect(x1, y1 - 1, mx - x1, 2), c);
                    Gx.Fill(new Rect(mx - 1, Mathf.Min(y1, y2) - 1, 2, Mathf.Abs(y2 - y1) + 2), c);
                    Gx.Fill(new Rect(mx, y2 - 1, x2 - mx, 2), c);
                }

            foreach (var t in Sim.Db.Techs)
            {
                var rect = layout[t.Id];
                var status = Sim.Research.Status(t.Id);
                var old = GUI.backgroundColor;
                GUI.backgroundColor = StatusColor(status) * 2.2f;
                string label = "<b>" + Gx.Colored(t.Name, status == TechStatus.Locked ? Theme.Dim : Color.white) + "</b>\n<size=11>" +
                               Gx.Colored(t.Category + " · " + NumberFormat.Short(t.ScienceCost) + " sci", status == TechStatus.Locked ? Theme.Dim : new Color(0.85f, 0.92f, 1f, 0.8f)) + "</size>";
                if (GUI.Button(rect, new GUIContent(label, t.Description), Theme.Node)) { selected = t.Id; Gx.Changed(); }
                GUI.backgroundColor = old;
                if (selected == t.Id) { Gx.Fill(new Rect(rect.x, rect.y, rect.width, 2), Theme.Orange); Gx.Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Theme.Orange); }
                if (status == TechStatus.Researching) Gx.Bar(new Rect(rect.x + 6, rect.yMax - 8, rect.width - 12, 4), (float)Sim.Research.Fraction, Theme.Cyan);
            }
            GUI.EndScrollView();

            var d = new Rect(r.x, r.yMax - detailH, r.width, detailH);
            Gx.Fill(new Rect(d.x, d.y - 4, d.width, 1), Theme.Border);
            GUILayout.BeginArea(d);
            DrawDetails();
            GUILayout.EndArea();
        }

        void DrawDetails()
        {
            var t = Sim.Db.Tech(selected);
            if (t == null)
            {
                GUILayout.Label("Select a technology to see details.", Theme.DimLabel);
                DrawAuto();
                return;
            }
            var status = Sim.Research.Status(t.Id);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(420));
            GUILayout.Label(Gx.Colored(t.Name, Theme.Cyan) + "  " + Gx.Colored(status.ToString(), Theme.Dim), Theme.Label);
            GUILayout.Label(t.Description, Theme.Wrap);
            GUILayout.Label("Cost: " + Gx.Colored(NumberFormat.Short(t.ScienceCost) + " science", Theme.Cyan) +
                            (t.MaterialCost.Count > 0 ? "  +  " + Fmt.Cost(Sim, Sim.Db.Start.StartLocation, t.MaterialCost) + " (paid on Earth at start)" : ""), Theme.Small);
            if (t.Prerequisites.Count > 0)
                GUILayout.Label("Requires: " + string.Join(", ", t.Prerequisites.Select(p => Gx.Colored(Sim.Db.Tech(p)?.Name ?? p, Sim.Research.Status(p) == TechStatus.Completed ? Theme.Good : Theme.Bad))), Theme.Small);
            if (status != TechStatus.Completed && t.MaterialCost.Count > 0 && !Sim.Inventory.Has(Sim.Db.Start.StartLocation, t.MaterialCost))
                GUILayout.Label(Gx.Colored("Materials must be on Earth before the project can start. " + Sim.Inventory.MissingText(Sim.Db.Start.StartLocation, t.MaterialCost), Theme.Warn), Theme.Small);
            GUILayout.BeginHorizontal();
            if (status == TechStatus.Available)
            {
                bool busy = !string.IsNullOrEmpty(Sim.State.Research.ActiveTech);
                if (Gx.Button(busy ? "Add to queue" : "Start research", true, null, Theme.Accent, GUILayout.Width(150), GUILayout.Height(30))) { Sim.Research.Start(t.Id); Gx.Changed(); }
            }
            if (status == TechStatus.Researching || status == TechStatus.Queued)
                if (Gx.Button("Cancel", true, "Invested science and materials are refunded", Theme.Button, GUILayout.Width(90), GUILayout.Height(30))) { Sim.Research.Cancel(t.Id); Gx.Changed(); }
            GUILayout.EndHorizontal();
            DrawAuto();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Unlocks", Theme.Header);
            foreach (var u in Sim.Db.DescribeUnlocks(t)) GUILayout.Label("• " + u, Theme.Small);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void DrawAuto()
        {
            if (Sim.State.Research.Queue.Count > 0)
                GUILayout.Label("Queue: " + string.Join(" > ", Sim.State.Research.Queue.Select(q => Sim.Db.Tech(q)?.Name)), Theme.DimLabel);
            if (Sim.State.HasFeature(Features.AutoResearch))
                Sim.State.Research.AutoResearch = GUILayout.Toggle(Sim.State.Research.AutoResearch, " Automated research (picks the cheapest available project)", Theme.Label);
        }
    }
}
