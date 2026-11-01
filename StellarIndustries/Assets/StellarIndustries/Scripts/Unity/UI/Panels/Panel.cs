using StellarIndustries.Core.Simulation;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.UI.Panels
{
    /// <summary>Base class for main navigation screens.</summary>
    public abstract class Panel
    {
        protected readonly GameUI UI;
        protected Vector2 Scroll;
        protected Panel(GameUI ui) { UI = ui; }
        protected GameSimulation Sim => UI.Sim;
        protected string Loc => Sim.State.CurrentLocation;

        public abstract string Title { get; }
        /// <summary>Null when unlocked; otherwise the hint shown on the locked nav button.</summary>
        public virtual string LockedHint => null;
        public bool IsUnlocked => LockedHint == null;
        public virtual float PreferredWidth => 700;
        public virtual SpaceView.ViewMode ViewMode => SpaceView.ViewMode.Location;
        public abstract void Draw(Rect r);

        protected void Heading(string title, string subtitle = null)
        {
            GUILayout.Label(title, Theme.Title);
            if (!string.IsNullOrEmpty(subtitle)) GUILayout.Label(subtitle, Theme.DimLabel);
            Gx.HLine();
            GUILayout.Space(4);
        }
    }
}
