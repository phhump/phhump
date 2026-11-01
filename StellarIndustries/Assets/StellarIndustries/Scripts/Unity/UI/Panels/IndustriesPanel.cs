using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Systems;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    /// <summary>Planet management: facilities, construction and manual operations at the selected location.</summary>
    public class IndustriesPanel : Panel
    {
        int tab;
        int armedDemolish; float armedTime;
        static readonly string[] Tabs = { "Facilities", "Construct", "Manual Work" };
        static readonly string[] Prio = { "Low", "Norm", "High" };

        public IndustriesPanel(GameUI ui) : base(ui) { }
        public override string Title => "Industries";
        public override float PreferredWidth => 780;

        public override void Draw(Rect r)
        {
            GUILayout.BeginArea(r);
            var def = Sim.Db.Location(Loc);
            Heading(def.Name + " Industries");
            GUILayout.Label(new GUIContent($"Gravity {def.Gravity:0.00} g  ·  {def.Atmosphere}  ·  {def.TemperatureC:0} °C  ·  Solar {NumberFormat.Percent(Core.Systems.SpacePhysics.SolarFactor(def))}  ·  Wind {(def.HasWind ? "yes" : "no")}",
                "Solar output = irradiance x atmospheric transmission, relative to Earth's surface. Irradiance falls with 1/distance² (inverse-square law)."), Theme.Small);
            if (def.RadiusKm > 0)
                GUILayout.Label($"Radius {def.RadiusKm:0} km  ·  Escape velocity {def.EscapeVelocityKms:0.00} km/s  ·  Day {def.DayLengthHours:0.#} h  ·  Sunlight {def.IrradianceWm2:0} W/m² at {def.DistanceAU:0.###} AU", Theme.DimLabel);
            GUILayout.Label("Deposits: " + string.Join(", ", def.Deposits.Select(d => Sim.Db.ResourceName(d.ResourceId) + " " + NumberFormat.Percent(d.Richness))), Theme.DimLabel);
            if (def.Hazards.Count > 0) GUILayout.Label("Hazards: " + string.Join(", ", def.Hazards), Theme.DimLabel);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++) if (GUILayout.Button(Tabs[i], i == tab ? Theme.NavActive : Theme.Nav, GUILayout.Width(130))) { tab = i; Gx.Changed(); }
            GUILayout.EndHorizontal();
            Scroll = GUILayout.BeginScrollView(Scroll);
            if (tab == 0) DrawFacilities();
            else if (tab == 1) DrawConstruct();
            else DrawManual();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawFacilities()
        {
            var list = Sim.Production.BuildingsAt(Loc);
            if (list.Count == 0) { GUILayout.Label("No facilities here yet. Use the Construct tab.", Theme.DimLabel); return; }
            foreach (var group in list.GroupBy(b => b.DefId).OrderBy(g => Sim.Db.Buildings.FindIndex(d => d.Id == g.Key)))
            {
                var def = Sim.Db.Building(group.Key);
                GUILayout.Label(def.Name + "  x" + group.Count(), Theme.Header);
                foreach (var b in group.OrderBy(x => x.Uid))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Lv " + b.Level, Theme.Label, GUILayout.Width(38));
                    if (def.PowerOutput > 0)
                    {
                        double mw = def.PowerOutput * Core.Systems.ProductionSystem.LevelSpeed(def, b.Level) * Sim.Modifiers.Multiplier(ModifierType.EnergyOutput) * b.EffectiveSpeed;
                        var fuel = Sim.Db.Recipe(b.RecipeId);
                        GUILayout.Label(new GUIContent(Gx.Colored("+" + NumberFormat.Short(mw) + " MW", Theme.Good) + (fuel != null ? "  burns " + Fmt.Amounts(Sim.Db, fuel.Inputs) + "/" + fuel.Duration + "s" : ""),
                            def.Description), Theme.Label, GUILayout.Width(250));
                    }
                    else if (def.Recipes.Count > 0)
                    {
                        var rec = Sim.Db.Recipe(b.RecipeId);
                        if (GUILayout.Button("<", Theme.Button, GUILayout.Width(22))) Sim.Construction.CycleRecipe(b.Uid, -1);
                        GUILayout.Label(new GUIContent(rec?.Name ?? "-", Fmt.Recipe(Sim.Db, rec)), Theme.Label, GUILayout.Width(170));
                        if (GUILayout.Button(">", Theme.Button, GUILayout.Width(22))) Sim.Construction.CycleRecipe(b.Uid, 1);
                    }
                    else GUILayout.Label(new GUIContent("+" + NumberFormat.Percent(def.StorageBonus * b.Level) + " storage", def.Description), Theme.Label, GUILayout.Width(250));

                    GUILayout.Label(Fmt.Status(b.Status), Theme.Small, GUILayout.Width(90));
                    if (def.PowerOutput <= 0 && def.Recipes.Count > 0) Gx.LayoutBar((float)b.Progress, Theme.Cyan, NumberFormat.Percent(b.EffectiveSpeed), 70, 14);
                    else GUILayout.Space(74);
                    var upCost = Sim.Construction.UpgradeCost(b);
                    bool canUp = Sim.Construction.CanUpgrade(b, out var why);
                    if (Gx.Button("Upgrade", canUp, "Level " + (b.Level + 1) + ": " + Fmt.Amounts(Sim.Db, upCost) + (canUp ? "" : "\n" + why), Theme.Button, GUILayout.Width(70)))
                        Sim.Construction.Upgrade(b.Uid);
                    if (GUILayout.Button(new GUIContent(b.Enabled ? "Pause" : "Run", b.Enabled ? "Pause this facility" : "Resume production"), Theme.Button, GUILayout.Width(50)))
                        Sim.Construction.SetEnabled(b.Uid, !b.Enabled);
                    if (GUILayout.Button(new GUIContent(Prio[b.Priority], "Priority: high-priority facilities receive inputs first"), Theme.Button, GUILayout.Width(46)))
                        Sim.Construction.SetPriority(b.Uid, (b.Priority + 1) % 3);
                    bool armed = armedDemolish == b.Uid && Time.unscaledTime - armedTime < 3;
                    if (GUILayout.Button(new GUIContent(armed ? "Sure?" : "X", "Demolish (refunds 50% of the base cost). Click twice."), armed ? Theme.Accent : Theme.Button, GUILayout.Width(armed ? 50 : 24)))
                    {
                        if (armed) { Sim.Construction.Demolish(b.Uid); armedDemolish = 0; Gx.Changed(); }
                        else { armedDemolish = b.Uid; armedTime = Time.unscaledTime; }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        void DrawConstruct()
        {
            int locked = 0;
            BuildingCategory? last = null;
            foreach (var def in Sim.Db.Buildings.OrderBy(d => d.Category))
            {
                var problem = Sim.Construction.PlacementProblem(def, Loc);
                if (problem != null && problem.StartsWith("Requires research")) { locked++; continue; }
                if (last != def.Category) { GUILayout.Label(def.Category.ToString(), Theme.Header); last = def.Category; }
                GUILayout.BeginHorizontal(Theme.Box);
                GUILayout.BeginVertical();
                int owned = Sim.Construction.CountAt(def.Id, Loc);
                GUILayout.Label(Gx.Colored(def.Name, Theme.Cyan) + (owned > 0 ? Gx.Colored("  (" + owned + " built)", Theme.Dim) : ""), Theme.Label);
                GUILayout.Label(def.Description, Theme.DimLabel);
                string power = def.PowerOutput > 0 ? Gx.Colored("+" + NumberFormat.Short(def.PowerOutput * Core.Systems.ProductionSystem.SourceFactor(def, Sim.Db.Location(Loc))) + " MW", Theme.Good)
                             : def.PowerUse > 0 ? Gx.Colored("-" + def.PowerUse + " MW", Theme.Warn) : "";
                var cost = Sim.Construction.BuildCost(def, Loc);
                GUILayout.Label("Cost: " + Fmt.Cost(Sim, Loc, cost) + "   " + power, Theme.Small);
                var recipes = Sim.Construction.ValidRecipes(def, Loc);
                if (recipes.Count > 0 && def.PowerOutput <= 0) GUILayout.Label("Can run: " + string.Join(", ", recipes.Select(x => x.Name)), Theme.DimLabel);
                if (problem != null) GUILayout.Label(Gx.Colored(problem, Theme.Bad), Theme.Small);
                GUILayout.EndVertical();
                bool can = Sim.Construction.CanBuild(def, Loc, out var why);
                if (Gx.Button("Build", can, can ? "Construct " + def.Name : why, Theme.Accent, GUILayout.Width(80), GUILayout.Height(40)))
                { Sim.Construction.Build(def.Id, Loc); Gx.Changed(); }
                GUILayout.EndHorizontal();
            }
            if (locked > 0) GUILayout.Label(locked + " more building types can be unlocked through research.", Theme.DimLabel);
        }

        void DrawManual()
        {
            GUILayout.Label("Gather by hand", Theme.Header);
            GUILayout.Label("Early on, every click counts. Automate as soon as possible!", Theme.DimLabel);
            GUILayout.BeginHorizontal();
            int n = 0;
            foreach (var res in Sim.Construction.GatherableResources(Loc))
            {
                if (n++ % 4 == 0 && n > 1) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Button("+" + NumberFormat.Short(Sim.Construction.ManualGatherAmount) + " " + res.Name, Theme.Button, GUILayout.Width(160), GUILayout.Height(34)))
                    if (Sim.Construction.Gather(Loc, res.Id) <= 0) UI.Notify("Storage full: " + res.Name, NotificationLevel.Warning);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Craft by hand", Theme.Header);
            foreach (var rec in Sim.Db.Recipes.Where(x => x.ManualCraftable && Sim.Construction.IsUnlocked(x)))
            {
                GUILayout.BeginHorizontal();
                bool can = Sim.Inventory.Has(Loc, rec.Inputs);
                if (Gx.Button(rec.Name, can, Fmt.Recipe(Sim.Db, rec), Theme.Button, GUILayout.Width(200), GUILayout.Height(30))) Sim.Construction.ManualCraft(Loc, rec.Id);
                GUILayout.Label(Fmt.Cost(Sim, Loc, rec.Inputs) + "  ->  " + Fmt.Amounts(Sim.Db, rec.Outputs), Theme.Small);
                GUILayout.EndHorizontal();
            }
        }
    }
}
