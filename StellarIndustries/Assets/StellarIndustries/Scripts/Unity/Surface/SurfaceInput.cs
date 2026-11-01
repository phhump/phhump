using System.Collections.Generic;
using UnityEngine;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// Keyboard/mouse state collected from IMGUI events (fed by GameUI at the end of OnGUI, after UI
    /// controls consumed their events). Works with the old Input Manager and the new Input System alike,
    /// because it never touches UnityEngine.Input. Read in Update, cleared in LateUpdate.
    /// </summary>
    public static class SurfaceInput
    {
        static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> pressed = new HashSet<KeyCode>();
        public static Vector2 LookDelta;
        public static float Scroll;
        public static bool LeftClick, RightHeld;
        /// <summary>Mouse position in screen pixels (origin bottom-left, like Camera.ScreenPointToRay expects).</summary>
        public static Vector2 MouseScreen;
        /// <summary>When true (command center or a modal is open) the world ignores input.</summary>
        public static bool Blocked;

        public static void Feed(Event e, float guiScale)
        {
            MouseScreen = new Vector2(e.mousePosition.x * guiScale, Screen.height - e.mousePosition.y * guiScale);
            switch (e.type)
            {
                case EventType.KeyDown:
                    if (e.keyCode != KeyCode.None) { if (held.Add(e.keyCode)) pressed.Add(e.keyCode); }
                    break;
                case EventType.KeyUp: held.Remove(e.keyCode); break;
                case EventType.MouseDown:
                    if (e.button == 0) LeftClick = true;
                    if (e.button == 1) RightHeld = true;
                    break;
                case EventType.MouseUp: if (e.button == 1) RightHeld = false; break;
                case EventType.MouseDrag: if (e.button == 1) LookDelta += e.delta; break;
                case EventType.ScrollWheel: Scroll += e.delta.y; break;
            }
        }

        public static bool Held(KeyCode k) => !Blocked && held.Contains(k);
        public static bool Pressed(KeyCode k) => !Blocked && pressed.Contains(k);
        public static bool Click => !Blocked && LeftClick;

        public static void EndFrame() { pressed.Clear(); LookDelta = Vector2.zero; Scroll = 0; LeftClick = false; }
        public static void ClearAll() { held.Clear(); EndFrame(); RightHeld = false; }
    }
}
