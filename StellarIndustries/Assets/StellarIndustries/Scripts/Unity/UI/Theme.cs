using System.Collections.Generic;
using System.Text;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI
{
    /// <summary>
    /// Modern sci-fi look: rounded glass panels with soft shadows, gradient buttons, glossy progress bars,
    /// status pills and real typefaces (Rajdhani for headings, Titillium Web for text; SIL OFL).
    /// All textures are generated at runtime, so the theme works without any imported UI assets.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Bg = new Color32(4, 7, 14, 255);
        public static readonly Color Panel = new Color32(10, 17, 31, 225);
        public static readonly Color PanelLight = new Color32(22, 34, 56, 235);
        public static readonly Color Border = new Color32(90, 200, 255, 70);
        public static readonly Color Cyan = new Color32(64, 224, 255, 255);
        public static readonly Color Orange = new Color32(255, 150, 60, 255);
        public static readonly Color Text = new Color32(222, 233, 245, 255);
        public static readonly Color Dim = new Color32(132, 152, 176, 255);
        public static readonly Color Good = new Color32(70, 226, 150, 255);
        public static readonly Color Warn = new Color32(255, 186, 60, 255);
        public static readonly Color Bad = new Color32(255, 96, 96, 255);

        public static GUIStyle Box, Card, Title, Header, Label, Small, DimLabel, Wrap, Button, Accent, Nav, NavActive, Node, Tooltip, BigTitle, Value, Field, Pill, Glass, Rounded;
        public static Texture2D White, Gloss;
        public static Font Body, BodyBold, Display, DisplayBold;
        static bool ready;

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Anti-aliased rounded rectangle for 9-slicing: optional vertical gradient, 1px border and soft outer shadow.</summary>
        static Texture2D RoundTex(int radius, Color top, Color bottom, Color border, int shadow = 0)
        {
            int size = (radius + shadow) * 2 + 8;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float inner = size - shadow * 2;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // signed distance to the rounded rect (inset by the shadow margin)
                    float cx = Mathf.Clamp(x + 0.5f, shadow + radius, size - shadow - radius), cy = Mathf.Clamp(y + 0.5f, shadow + radius, size - shadow - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - radius;
                    float fillA = Mathf.Clamp01(0.5f - d);
                    float k = Mathf.Clamp01((y - shadow) / Mathf.Max(1f, inner - 1));
                    Color fill = Color.Lerp(bottom, top, k);           // texture y goes up
                    float borderA = Mathf.Clamp01(1f - Mathf.Abs(d + 0.5f)) * border.a;
                    Color c = fill; c.a *= fillA;
                    c = Color.Lerp(c, new Color(border.r, border.g, border.b, Mathf.Max(c.a, border.a)), borderA);
                    if (shadow > 0 && d > 0)
                    {
                        float sa = Mathf.Pow(Mathf.Clamp01(1 - d / shadow), 2) * 0.45f;
                        c = new Color(0, 0, 0, Mathf.Max(c.a, sa));
                    }
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static GUIStyle Btn(Color top, Color bottom, Color border, Color hoverTop, Color hoverBottom, Color hoverBorder, Color text, int radius = 7)
        {
            var s = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter, richText = true, padding = new RectOffset(10, 10, 4, 5), margin = new RectOffset(2, 2, 2, 2) };
            if (BodyBold != null) s.font = BodyBold;
            s.normal.background = RoundTex(radius, top, bottom, border); s.normal.textColor = text;
            s.hover.background = RoundTex(radius, hoverTop, hoverBottom, hoverBorder); s.hover.textColor = Color.white;
            s.active.background = RoundTex(radius, hoverBottom, hoverTop, hoverBorder); s.active.textColor = Color.white;
            s.onNormal = s.active; s.focused = s.normal;
            s.border = new RectOffset(radius + 4, radius + 4, radius + 4, radius + 4);
            return s;
        }

        static Font LoadFont(string name) => Resources.Load<Font>("Fonts/" + name);

        public static void Ensure()
        {
            if (ready && White != null) return;
            Body = LoadFont("TitilliumWeb-Regular"); BodyBold = LoadFont("TitilliumWeb-SemiBold");
            Display = LoadFont("Rajdhani-SemiBold"); DisplayBold = LoadFont("Rajdhani-Bold");
            White = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            White.SetPixel(0, 0, Color.white); White.Apply();
            Gloss = new Texture2D(1, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 16; y++) Gloss.SetPixel(0, y, new Color(1, 1, 1, y > 8 ? 0.22f * (y - 8) / 7f : 0));
            Gloss.Apply();

            Box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12), border = new RectOffset(26, 26, 26, 26), overflow = new RectOffset(10, 10, 10, 10) };
            Box.normal.background = RoundTex(12, new Color32(14, 23, 41, 232), new Color32(8, 13, 25, 238), new Color(0.35f, 0.78f, 1f, 0.28f), 10);
            Card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 9, 9), margin = new RectOffset(0, 0, 3, 5), border = new RectOffset(13, 13, 13, 13) };
            Card.normal.background = RoundTex(9, new Color32(26, 38, 62, 210), new Color32(17, 26, 44, 215), new Color(1f, 1f, 1f, 0.07f));
            Glass = new GUIStyle(GUI.skin.box) { border = new RectOffset(4, 4, 4, 4) };
            Glass.normal.background = RoundTex(0, new Color32(14, 22, 40, 238), new Color32(6, 10, 20, 242), new Color(0.35f, 0.78f, 1f, 0.0f));
            Rounded = new GUIStyle { border = new RectOffset(9, 9, 9, 9) };
            Rounded.normal.background = RoundTex(5, Color.white, Color.white, new Color(1, 1, 1, 0));

            Label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = false, padding = new RectOffset(2, 2, 1, 1) };
            if (Body != null) Label.font = Body;
            Label.normal.textColor = Text;
            Wrap = new GUIStyle(Label) { wordWrap = true };
            Small = new GUIStyle(Label) { fontSize = 12 };
            DimLabel = new GUIStyle(Small) { wordWrap = true };
            DimLabel.normal.textColor = Dim;
            Value = new GUIStyle(Label) { alignment = TextAnchor.MiddleRight };
            Title = new GUIStyle(Label) { fontSize = 24, fontStyle = DisplayBold != null ? FontStyle.Normal : FontStyle.Bold };
            if (DisplayBold != null) Title.font = DisplayBold;
            Title.normal.textColor = Color.white;
            Header = new GUIStyle(Label) { fontSize = 16, margin = new RectOffset(0, 0, 10, 3) };
            if (Display != null) Header.font = Display;
            Header.normal.textColor = Orange;
            BigTitle = new GUIStyle(Label) { fontSize = 64, alignment = TextAnchor.MiddleLeft };
            if (DisplayBold != null) BigTitle.font = DisplayBold;
            BigTitle.normal.textColor = Color.white;

            Button = Btn(new Color32(36, 54, 86, 245), new Color32(22, 34, 58, 245), new Color(0.45f, 0.8f, 1f, 0.35f),
                         new Color32(46, 82, 124, 255), new Color32(28, 52, 86, 255), new Color(0.45f, 0.9f, 1f, 0.9f), Text);
            Accent = Btn(new Color32(255, 168, 82, 255), new Color32(226, 112, 28, 255), new Color(1f, 0.85f, 0.6f, 0.6f),
                         new Color32(255, 190, 110, 255), new Color32(240, 132, 44, 255), new Color(1f, 0.95f, 0.8f, 1f), new Color32(30, 14, 0, 255));
            Nav = Btn(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), new Color32(30, 48, 78, 200), new Color32(22, 36, 60, 200), new Color(0.45f, 0.85f, 1f, 0.35f), Text, 8);
            Nav.alignment = TextAnchor.MiddleLeft; Nav.fontSize = 15; Nav.padding = new RectOffset(16, 8, 6, 7);
            if (Display != null) Nav.font = Display;
            NavActive = Btn(new Color32(28, 86, 122, 240), new Color32(18, 56, 86, 240), new Color(0.45f, 0.9f, 1f, 0.85f), new Color32(34, 100, 140, 255), new Color32(22, 66, 100, 255), new Color(0.6f, 0.95f, 1f, 1f), Color.white, 8);
            NavActive.alignment = TextAnchor.MiddleLeft; NavActive.fontSize = 15; NavActive.padding = Nav.padding;
            if (Display != null) NavActive.font = Display;
            Node = new GUIStyle(Button) { alignment = TextAnchor.UpperLeft, fontSize = 13, wordWrap = true, padding = new RectOffset(10, 8, 6, 5) };
            if (Body != null) Node.font = Body;
            Tooltip = new GUIStyle(Wrap) { fontSize = 13, padding = new RectOffset(12, 12, 8, 9), border = new RectOffset(13, 13, 13, 13) };
            Tooltip.normal.background = RoundTex(8, new Color32(10, 18, 34, 250), new Color32(6, 11, 22, 250), new Color(0.4f, 0.9f, 1f, 0.8f));
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 17, padding = new RectOffset(12, 12, 7, 7), border = new RectOffset(11, 11, 11, 11) };
            if (Body != null) Field.font = Body;
            Field.normal.background = RoundTex(7, new Color32(6, 11, 22, 255), new Color32(10, 16, 30, 255), new Color(0.45f, 0.8f, 1f, 0.35f)); Field.normal.textColor = Color.white;
            Field.focused.background = RoundTex(7, new Color32(6, 11, 22, 255), new Color32(10, 16, 30, 255), Cyan); Field.focused.textColor = Color.white;
            Field.hover = Field.normal;
            Pill = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 1, 2), border = new RectOffset(9, 9, 9, 9), fontSize = 11 };
            if (BodyBold != null) Pill.font = BodyBold;
            Pill.normal.background = Rounded.normal.background;
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

        /// <summary>Tinted rounded rectangle (9-sliced).</summary>
        public static void RoundFill(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color; GUI.color = c;
            Theme.Rounded.Draw(r, false, false, false, false);
            GUI.color = old;
        }

        static GUIStyle barText;

        /// <summary>Glossy rounded progress bar.</summary>
        public static void Bar(Rect r, float f, Color c, string text = null)
        {
            RoundFill(r, new Color(0, 0, 0, 0.5f));
            float w = Mathf.Max(f > 0 ? Mathf.Min(r.height, r.width) : 0, r.width * Mathf.Clamp01(f));
            if (w > 0)
            {
                var fill = new Rect(r.x, r.y, w, r.height);
                RoundFill(fill, c);
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(fill.x + 2, fill.y, fill.width - 4, fill.height), Theme.Gloss);
            }
            if (!string.IsNullOrEmpty(text))
            {
                if (barText == null) { barText = new GUIStyle(Theme.Small) { alignment = TextAnchor.MiddleCenter, fontSize = 11 }; barText.normal.textColor = Color.white; }
                GUI.Label(r, text, barText);
            }
        }

        /// <summary>Status pill: tinted rounded badge with coloured text.</summary>
        public static void Pill(string text, Color c, float width = 96)
        {
            var r = GUILayoutUtility.GetRect(width, 18, GUILayout.Width(width), GUILayout.Height(18));
            r.y += 3;
            RoundFill(r, new Color(c.r, c.g, c.b, 0.18f));
            var old = Theme.Pill.normal.textColor; Theme.Pill.normal.textColor = c;
            GUI.Label(r, text, Theme.Pill);
            Theme.Pill.normal.textColor = old;
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

        public static void Swatch(Color c, float size = 12)
        {
            var r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            r.y += 5;
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color; GUI.color = c;
                GUI.DrawTexture(r, Views.VisualFactory.CircleTexture);
                GUI.color = old;
            }
        }

        /// <summary>
        /// Call after an action that changes the GUI structure (rows added/removed, screen switched).
        /// Aborts the current IMGUI pass so Layout and the following event stay consistent.
        /// </summary>
        public static void Changed() => GUIUtility.ExitGUI();

        public static void HLine()
        {
            var r = GUILayoutUtility.GetRect(10, 2, GUILayout.ExpandWidth(true), GUILayout.Height(2));
            Fill(new Rect(r.x, r.y, r.width * 0.25f, 2), Theme.Cyan * new Color(1, 1, 1, 0.8f));
            Fill(new Rect(r.x + r.width * 0.25f, r.y + 0.5f, r.width * 0.75f, 1), Theme.Border);
        }

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

        public static (string text, Color color) StatusPill(BuildingStatus s)
        {
            switch (s)
            {
                case BuildingStatus.Working: return ("Working", Theme.Good);
                case BuildingStatus.LowPower: return ("Low power", Theme.Warn);
                case BuildingStatus.NoPower: return ("No power", Theme.Bad);
                case BuildingStatus.MissingInput: return ("Missing input", Theme.Warn);
                case BuildingStatus.OutputFull: return ("Storage full", Theme.Warn);
                case BuildingStatus.Paused: return ("Paused", Theme.Dim);
                case BuildingStatus.NoDeposit: return ("No deposit", Theme.Bad);
                case BuildingStatus.NoFuel: return ("No fuel", Theme.Bad);
                default: return ("Standby", Theme.Dim);
            }
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
