using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    public class ResourcesPanel : Panel
    {
        bool showAll;
        public ResourcesPanel(GameUI ui) : base(ui) { }
        public override string Title => "Resources";

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            Heading("Resources - " + Sim.Db.Location(Loc).Name, "Storage capacity grows with warehouses and Storage Logistics research.");
            bool all = GUILayout.Toggle(showAll, " Show resources not present here", Theme.Label);
            if (all != showAll) { showAll = all; Gx.Changed(); }
            Scroll = GUILayout.BeginScrollView(Scroll);
            var loc = Sim.State.Location(Loc);
            foreach (var cat in Sim.Db.Resources.GroupBy(x => x.Category))
            {
                var items = cat.Where(res => showAll || Sim.Inventory.Get(Loc, res.Id) > 0 || loc.Rates.ContainsKey(res.Id)).ToList();
                if (items.Count == 0) continue;
                GUILayout.Label(cat.Key.ToString(), Theme.Header);
                foreach (var res in items)
                {
                    double amt = Sim.Inventory.Get(Loc, res.Id), cap = Sim.Inventory.Capacity(Loc, res.Id);
                    loc.Rates.TryGetValue(res.Id, out var rate);
                    GUILayout.BeginHorizontal();
                    Gx.Swatch(Fmt.ResColor(Sim.Db, res.Id));
                    GUILayout.Label(new GUIContent(res.Name + (res.IsGlobal ? " (global)" : ""), res.Description), Theme.Label, GUILayout.Width(190));
                    if (res.IsGlobal) GUILayout.Label(NumberFormat.Short(amt), Theme.Label, GUILayout.Width(220));
                    else Gx.LayoutBar((float)(amt / cap), amt >= cap - 1e-6 ? Theme.Warn : Fmt.ResColor(Sim.Db, res.Id) * 0.8f, NumberFormat.Short(amt) + " / " + NumberFormat.Short(cap), 220, 16);
                    GUILayout.Label(rate == 0 ? "" : Gx.Colored(NumberFormat.Rate(rate), rate > 0 ? Theme.Good : Theme.Bad), Theme.Label, GUILayout.Width(90));
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
