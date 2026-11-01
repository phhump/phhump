using System;
using StellarIndustries.Core.Save;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI
{
    /// <summary>Title screen: glass side panel with save slots; the 3D Earth rotates on the right.</summary>
    public class MainMenu
    {
        readonly GameUI ui;
        string newName = "Stellar Industries";
        string error;
        SlotInfo[] slots;
        float refresh;
        GUIStyle titleOrange, tagline;

        public MainMenu(GameUI ui) { this.ui = ui; }

        public void Draw(Rect screen)
        {
            if (slots == null || Time.unscaledTime - refresh > 2f)
            {
                refresh = Time.unscaledTime;
                slots = new SlotInfo[SaveManager.SlotCount];
                for (int i = 0; i < slots.Length; i++) slots[i] = ui.GC.Saves.Info(i + 1);
            }
            if (titleOrange == null)
            {
                titleOrange = new GUIStyle(Theme.BigTitle); titleOrange.normal.textColor = Theme.Orange;
                tagline = new GUIStyle(Theme.Label) { fontSize = 17, wordWrap = true }; tagline.normal.textColor = new Color(0.8f, 0.87f, 0.95f);
            }
            float w = Mathf.Min(560, screen.width * 0.4f);
            var panel = new Rect(24, 24, w, screen.height - 48);
            GUI.Box(panel, GUIContent.none, Theme.Box);
            float x = panel.x + 36, iw = w - 72;
            GUI.Label(new Rect(x, panel.y + 40, iw, 70), "STELLAR", Theme.BigTitle);
            GUI.Label(new Rect(x, panel.y + 100, iw, 70), "INDUSTRIES", titleOrange);
            Gx.Fill(new Rect(x + 2, panel.y + 176, 90, 3), Theme.Cyan);
            GUI.Label(new Rect(x, panel.y + 190, iw, 50), "From one rocket factory on Earth to industry around other stars.", tagline);

            float y = panel.y + 260;
            GUI.Label(new Rect(x, y, iw, 18), "EMPIRE NAME", Theme.DimLabel);
            newName = GUI.TextField(new Rect(x, y + 20, iw, 38), newName ?? "", 40, Theme.Field);
            y += 76;

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                int slot = i + 1;
                var r = new Rect(x, y, iw, 104);
                GUI.Box(r, GUIContent.none, Theme.Card);
                GUI.Label(new Rect(r.x + 16, r.y + 10, 120, 18), "SLOT " + slot, Theme.DimLabel);
                if (!s.Exists) GUI.Label(new Rect(r.x + 16, r.y + 30, iw - 32, 24), Gx.Colored("Empty slot", Theme.Dim), Theme.Label);
                else if (s.Corrupt) GUI.Label(new Rect(r.x + 16, r.y + 30, iw - 32, 24), Gx.Colored("Damaged save - a backup is restored on load if possible", Theme.Warn), Theme.Label);
                else
                {
                    var h = s.Header;
                    string when = h.SavedUtcTicks > 0 ? new DateTime(h.SavedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "?";
                    GUI.Label(new Rect(r.x + 16, r.y + 28, iw - 32, 26), "<size=17><b>" + h.Name + "</b></size>", Theme.Label);
                    GUI.Label(new Rect(r.x + 16, r.y + 52, iw - 32, 18), $"{NumberFormat.Duration(h.PlayTime)} played  ·  {h.Buildings} facilities  ·  {h.Locations} worlds  ·  {when}", Theme.DimLabel);
                }
                float bx = r.x + 16, by = r.y + 72;
                if (s.Exists)
                {
                    if (GUI.Button(new Rect(bx, by, 120, 26), "Continue", Theme.Accent))
                    {
                        if (!ui.GC.LoadGame(slot, out error)) ui.Notify(error, Core.Systems.NotificationLevel.Error);
                        Gx.Changed();
                    }
                    bx += 128;
                    if (GUI.Button(new Rect(bx, by, 110, 26), "New Game", Theme.Button))
                        ui.Confirm("Overwrite slot " + slot + " with a new game?", () => ui.GC.NewGame(slot, newName));
                    bx += 118;
                    if (GUI.Button(new Rect(bx, by, 84, 26), "Delete", Theme.Button))
                        ui.Confirm("Delete the save in slot " + slot + "? This cannot be undone.", () => { ui.GC.Saves.Delete(slot); slots = null; });
                }
                else if (GUI.Button(new Rect(bx, by, 160, 26), "Start New Game", Theme.Accent)) { ui.GC.NewGame(slot, newName); Gx.Changed(); }
                y += 114;
            }
            if (!string.IsNullOrEmpty(error)) GUI.Label(new Rect(x, y, iw, 40), Gx.Colored(error, Theme.Bad), Theme.Wrap);
            if (GUI.Button(new Rect(x, panel.yMax - 62, 110, 34), "Quit", Theme.Button)) Application.Quit();
            GUI.Label(new Rect(x + 124, panel.yMax - 66, iw - 124, 40), "Planet textures: Solar System Scope (CC BY 4.0), based on NASA imagery.\nSaves: " + ui.GC.SaveDirectory, Theme.DimLabel);
        }
    }
}
