using System;
using StellarIndustries.Core.Save;
using StellarIndustries.Core.Util;
using UnityEngine;

namespace StellarIndustries.Unity.UI
{
    public class MainMenu
    {
        readonly GameUI ui;
        string newName = "Stellar Industries";
        string error;
        SlotInfo[] slots;
        float refresh;

        public MainMenu(GameUI ui) { this.ui = ui; }

        public void Draw(Rect screen)
        {
            if (slots == null || Time.unscaledTime - refresh > 2f)
            {
                refresh = Time.unscaledTime;
                slots = new SlotInfo[SaveManager.SlotCount];
                for (int i = 0; i < slots.Length; i++) slots[i] = ui.GC.Saves.Info(i + 1);
            }
            Gx.Fill(new Rect(0, 0, screen.width * 0.42f, screen.height), new Color(0.01f, 0.02f, 0.05f, 0.82f));
            float x = 50, w = Mathf.Min(460, screen.width * 0.42f - 80);
            GUI.Label(new Rect(x, 70, w, 60), "STELLAR", new GUIStyle(Theme.BigTitle) { alignment = TextAnchor.MiddleLeft });
            GUI.Label(new Rect(x, 120, w, 60), "INDUSTRIES", new GUIStyle(Theme.BigTitle) { alignment = TextAnchor.MiddleLeft, normal = { textColor = Theme.Orange } });
            GUI.Label(new Rect(x, 182, w, 22), "From one rocket factory to a galactic industrial empire.", Theme.DimLabel);

            GUI.Label(new Rect(x, 225, w, 20), "EMPIRE NAME", Theme.DimLabel);
            newName = GUI.TextField(new Rect(x, 245, w, 32), newName ?? "", 40, Theme.Field);

            float y = 300;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                int slot = i + 1;
                var r = new Rect(x, y, w, 92);
                GUI.Box(r, GUIContent.none, Theme.Box);
                GUI.Label(new Rect(r.x + 12, r.y + 8, w - 24, 20), "SLOT " + slot, Theme.DimLabel);
                if (!s.Exists) GUI.Label(new Rect(r.x + 12, r.y + 26, w - 24, 22), Gx.Colored("Empty", Theme.Dim), Theme.Label);
                else if (s.Corrupt) GUI.Label(new Rect(r.x + 12, r.y + 26, w - 24, 22), Gx.Colored("Damaged save (a backup may be restored on load)", Theme.Warn), Theme.Label);
                else
                {
                    var h = s.Header;
                    string when = h.SavedUtcTicks > 0 ? new DateTime(h.SavedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "?";
                    GUI.Label(new Rect(r.x + 12, r.y + 26, w - 24, 22), Gx.Colored(h.Name, Theme.Cyan), Theme.Label);
                    GUI.Label(new Rect(r.x + 12, r.y + 44, w - 24, 18), $"{NumberFormat.Duration(h.PlayTime)} played · {h.Buildings} buildings · {h.Locations} worlds · saved {when}", Theme.DimLabel);
                }
                float bx = r.x + 12, by = r.y + 62;
                if (s.Exists)
                {
                    if (GUI.Button(new Rect(bx, by, 110, 24), "Continue", Theme.Accent))
                    {
                        if (!ui.GC.LoadGame(slot, out error)) ui.Notify(error, Core.Systems.NotificationLevel.Error);
                        Gx.Changed();
                    }
                    bx += 116;
                    if (GUI.Button(new Rect(bx, by, 110, 24), "New Game", Theme.Button))
                        ui.Confirm("Overwrite slot " + slot + " with a new game?", () => ui.GC.NewGame(slot, newName));
                    bx += 116;
                    if (GUI.Button(new Rect(bx, by, 80, 24), "Delete", Theme.Button))
                        ui.Confirm("Delete the save in slot " + slot + "? This cannot be undone.", () => { ui.GC.Saves.Delete(slot); slots = null; });
                }
                else if (GUI.Button(new Rect(bx, by, 140, 24), "Start New Game", Theme.Accent)) { ui.GC.NewGame(slot, newName); Gx.Changed(); }
                y += 100;
            }
            if (!string.IsNullOrEmpty(error)) GUI.Label(new Rect(x, y, w, 40), Gx.Colored(error, Theme.Bad), Theme.Wrap);
            if (GUI.Button(new Rect(x, screen.height - 60, 120, 30), "Quit", Theme.Button)) Application.Quit();
            GUI.Label(new Rect(x + 130, screen.height - 54, w, 20), "Saves: " + ui.GC.SaveDirectory, Theme.DimLabel);
        }
    }
}
