using System.Collections.Generic;
using System.Text;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI
{
    /// <summary>Sci-fi industrial look: dark panels, cyan accents, industrial orange highlights.</summary>
    public static class Theme
    {
        public static readonly Color Bg = new Color32(7, 11, 20, 255);
        public static readonly Color Panel = new Color32(12, 20, 34, 236);
        public static readonly Color PanelLight = new Color32(22, 34, 54, 240);
        public static readonly Color Border = new Color32(43, 217, 254, 90);
        public static readonly Color Cyan = new Color32(43, 217, 254, 255);
        public static readonly Color Orange = new Color32(255, 140, 42, 255);
        public static readonly Color Text = new Color32(216, 230, 243, 255);
        public static readonly Color Dim = new Color32(127, 147, 168, 255);
        public static readonly Color Good = new Color32(61, 220, 132, 255);
        public static readonly Color Warn = new Color32(255, 176, 32, 255);
        public static readonly Color Bad = new Color32(255, 90, 90, 255);

        public static GUIStyle Box, Title, Header, Label, Small, DimLabel, Wrap, Button, Accent, Nav, NavActive, Node, Tooltip, BigTitle, Value, Field;
        public static Texture2D White;
        static bool ready;

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static Texture2D Tex(Color fill, Color border)
        {
            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    t.SetPixel(x, y, x == 0 || y == 0 || x == 7 || y == 7 ? border : fill);
            t.Apply();
            return t;
        }

        static GUIStyle Btn(Color fill, Color border, Color hover, Color text)
        {
            var s = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true, padding = new RectOffset(8, 8, 4, 4), margin = new RectOffset(2, 2, 2, 2) };
            s.normal.background = Tex(fill, border); s.normal.textColor = text;
            s.hover.background = Tex(hover, border); s.hover.textColor = Color.white;
            s.active.background = Tex(border, border); s.active.textColor = Color.white;
            s.onNormal = s.active; s.focused = s.normal;
            s.border = new RectOffset(2, 2, 2, 2);
            return s;
        }

        public static void Ensure()
        {
            if (ready && White != null) return;
            White = Tex(Color.white, Color.white);
            Box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), border = new RectOffset(2, 2, 2, 2) };
            Box.normal.background = Tex(Panel, Border);
            Label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };
            Label.normal.textColor = Text;
            Wrap = new GUIStyle(Label) { wordWrap = true };
            Small = new GUIStyle(Label) { fontSize = 11 };
            DimLabel = new GUIStyle(Small) { wordWrap = true };
            DimLabel.normal.textColor = Dim;
            Value = new GUIStyle(Label) { alignment = TextAnchor.MiddleRight };
            Title = new GUIStyle(Label) { fontSize = 17, fontStyle = FontStyle.Bold };
            Title.normal.textColor = Cyan;
            Header = new GUIStyle(Label) { fontSize = 13, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 8, 2) };
            Header.normal.textColor = Orange;
            BigTitle = new GUIStyle(Label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            BigTitle.normal.textColor = Color.white;
            Button = Btn(PanelLight, Border, new Color32(30, 60, 90, 255), Text);
            Accent = Btn(new Color32(120, 60, 10, 255), Orange, new Color32(170, 85, 15, 255), Color.white);
            Nav = Btn(new Color32(10, 16, 28, 220), new Color32(43, 217, 254, 40), new Color32(25, 45, 70, 255), Text);
            Nav.alignment = TextAnchor.MiddleLeft; Nav.fontSize = 14; Nav.padding = new RectOffset(12, 6, 6, 6);
            NavActive = Btn(new Color32(18, 60, 85, 255), Cyan, new Color32(25, 75, 105, 255), Color.white);
            NavActive.alignment = TextAnchor.MiddleLeft; NavActive.fontSize = 14; NavActive.padding = Nav.padding;
            Node = new GUIStyle(Button) { alignment = TextAnchor.UpperLeft, fontSize = 12, wordWrap = true, padding = new RectOffset(8, 6, 5, 4) };
            Tooltip = new GUIStyle(Wrap) { fontSize = 12, padding = new RectOffset(8, 8, 6, 6) };
            Tooltip.normal.background = Tex(new Color32(5, 10, 18, 250), Cyan);
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 15, padding = new RectOffset(8, 8, 6, 6) };
            Field.normal.background = Tex(new Color32(5, 10, 18, 255), Border); Field.normal.textColor = Color.white;
            Field.focused.background = Tex(new Color32(5, 10, 18, 255), Cyan); Field.focused.textColor = Color.white;
            ready = true;
        }
    }

    /// <summary>Small IMGUI helpers.</summary>
    public static class Gx
    {
        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Theme.White); GUI.color = old;
        }

        public static void Bar(Rect r, float f, Color c, string text = null)
        {
            Fill(r, new Color(0, 0, 0, 0.45f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(f), r.height), c);
            if (!string.IsNullOrEmpty(text))
            {
                var s = new GUIStyle(Theme.Small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(r, text, s);
            }
        }

        public static void LayoutBar(float f, Color c, string text = null, float width = 100, float height = 14)
        {
            var r = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            r.y += 3;
            Bar(r, f, c, text);
        }

        public static bool Button(string text, bool enabled = true, string tooltip = null, GUIStyle style = null, params GUILayoutOption[] opts)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool r = GUILayout.Button(new GUIContent(text, tooltip), style ?? Theme.Button, opts);
            GUI.enabled = old;
            return r && enabled;
        }

        public static void Swatch(Color c, float size = 10)
        {
            var r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            r.y += 4;
            Fill(r, c);
        }

        /// <summary>
        /// Call after an action that changes the GUI structure (rows added/removed, screen switched).
        /// Aborts the current IMGUI pass so Layout and the following event stay consistent.
        /// </summary>
        public static void Changed() => GUIUtility.ExitGUI();

        public static void HLine() { var r = GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true), GUILayout.Height(1)); Fill(r, Theme.Border); }

        public static string Colored(string s, Color c) => "<color=" + Theme.Hex(c) + ">" + s + "</color>";

        public static Color LevelColor(Core.Systems.NotificationLevel l)
        {
            switch (l)
            {
                case Core.Systems.NotificationLevel.Success: return Theme.Good;
                case Core.Systems.NotificationLevel.Warning: return Theme.Warn;
                case Core.Systems.NotificationLevel.Error: return Theme.Bad;
                default: return Theme.Cyan;
            }
        }
    }

    /// <summary>Text formatting shared by panels.</summary>
    public static class Fmt
    {
        public static string Amounts(GameDatabase db, IEnumerable<ResourceAmount> list)
        {
            var sb = new StringBuilder();
            foreach (var a in list) { if (sb.Length > 0) sb.Append(", "); sb.Append(NumberFormat.Short(a.Amount)).Append(' ').Append(db.ResourceName(a.ResourceId)); }
            return sb.Length == 0 ? "nothing" : sb.ToString();
        }

        /// <summary>Cost with red highlighting for amounts the location cannot pay.</summary>
        public static string Cost(GameSimulation sim, string loc, IEnumerable<ResourceAmount> list)
        {
            var sb = new StringBuilder();
            foreach (var a in list)
            {
                if (sb.Length > 0) sb.Append("  ");
                bool ok = sim.Inventory.Get(loc, a.ResourceId) + 1e-9 >= a.Amount;
                sb.Append(Gx.Colored(NumberFormat.Short(a.Amount) + " " + sim.Db.ResourceName(a.ResourceId), ok ? Theme.Text : Theme.Bad));
            }
            return sb.Length == 0 ? "free" : sb.ToString();
        }

        public static string Recipe(GameDatabase db, RecipeDef r)
        {
            if (r == null) return "";
            string inp = r.Inputs.Count == 0 ? (string.IsNullOrEmpty(r.RequiredDeposit) ? "-" : "deposit") : Amounts(db, r.Inputs);
            string outp = r.Outputs.Count == 0 ? "energy" : Amounts(db, r.Outputs);
            return inp + "  ->  " + outp + "  (" + r.Duration.ToString("0.#") + "s)";
        }

        public static string Status(BuildingStatus s)
        {
            switch (s)
            {
                case BuildingStatus.Working: return Gx.Colored("Working", Theme.Good);
                case BuildingStatus.LowPower: return Gx.Colored("Low power", Theme.Warn);
                case BuildingStatus.NoPower: return Gx.Colored("No power", Theme.Bad);
                case BuildingStatus.MissingInput: return Gx.Colored("Missing input", Theme.Warn);
                case BuildingStatus.OutputFull: return Gx.Colored("Storage full", Theme.Warn);
                case BuildingStatus.Paused: return Gx.Colored("Paused", Theme.Dim);
                case BuildingStatus.NoDeposit: return Gx.Colored("No deposit", Theme.Bad);
                case BuildingStatus.NoFuel: return Gx.Colored("No fuel", Theme.Bad);
                default: return Gx.Colored("Idle", Theme.Dim);
            }
        }

        public static Color ResColor(GameDatabase db, string id) => Views.VisualFactory.Hex(db.Resource(id)?.ColorHex, Color.gray);
    }
}
