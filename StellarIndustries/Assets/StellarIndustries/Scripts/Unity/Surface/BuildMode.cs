using StellarIndustries.Core.Data;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// Build mode: a translucent preview of the chosen building follows the mouse on the terrain (in range of the
    /// engineer or rover). Green = legal, red = not (reason shown in the HUD). R rotates, left click builds,
    /// Esc / right click cancels. All rules come from ConstructionSystem.BuildAtProblem.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        public const float EngineerRange = 45, RoverRange = 90;
        SurfaceWorld world;
        GameObject ghost;
        Material ok, bad;
        public BuildingDef Selected { get; private set; }
        public float Rotation;
        public Vector3 Point { get; private set; }
        public bool HasPoint { get; private set; }
        public string Problem { get; private set; }
        public bool PaletteOpen;

        public void Init(SurfaceWorld w)
        {
            world = w;
            ok = VisualFactory.Unlit(new Color(0.3f, 1f, 0.6f, 0.35f));
            bad = VisualFactory.Unlit(new Color(1f, 0.3f, 0.3f, 0.35f));
        }

        public void Select(BuildingDef def)
        {
            Cancel();
            Selected = def;
            ghost = StructureFactory.Create(def, Color.gray);
            ghost.transform.SetParent(transform, false);
            StructureFactory.MakeGhost(ghost, ok);
            PaletteOpen = false;
        }

        public void Cancel()
        {
            Selected = null;
            if (ghost != null) Destroy(ghost);
            ghost = null;
            HasPoint = false;
        }

        void Update()
        {
            if (world == null || !world.Active) return;
            if (SurfaceInput.Pressed(KeyCode.B)) { if (Selected != null) Cancel(); else PaletteOpen = !PaletteOpen; }
            if (SurfaceInput.Pressed(KeyCode.Escape)) { if (Selected != null) Cancel(); PaletteOpen = false; }
            if (Selected == null) return;
            if (SurfaceInput.Pressed(KeyCode.R)) Rotation = (Rotation + (SurfaceInput.Held(KeyCode.LeftShift) ? 15 : 45)) % 360;

            var cam = Camera.main;
            HasPoint = false;
            if (cam != null && Physics.Raycast(cam.ScreenPointToRay(SurfaceInput.MouseScreen), out var hit, 600f, Physics.DefaultRaycastLayers & ~(1 << 2)))
            {
                var origin = world.Engineer.InRover ? world.Rover.transform.position : world.Engineer.transform.position;
                float range = world.Engineer.InRover ? RoverRange : EngineerRange;
                var p = hit.point;
                var flat = new Vector2(p.x - origin.x, p.z - origin.z);
                if (flat.magnitude > range) { flat = flat.normalized * range; p = new Vector3(origin.x + flat.x, 0, origin.z + flat.y); }
                // snap to a 2 m grid for tidy layouts
                p.x = Mathf.Round(p.x / 2) * 2; p.z = Mathf.Round(p.z / 2) * 2;
                p.y = world.HeightAt(p.x, p.z);
                Point = p; HasPoint = true;
            }
            if (!HasPoint) { ghost.SetActive(false); return; }
            ghost.SetActive(true);
            ghost.transform.position = Point;
            ghost.transform.rotation = Quaternion.Euler(0, Rotation, 0);
            Problem = world.Sim.Construction.BuildAtProblem(Selected.Id, world.LocationId, Point.x, Point.z);
            StructureFactory.MakeGhost(ghost, Problem == null ? ok : bad);
            if (SurfaceInput.Click && Problem == null)
                world.Sim.Construction.BuildAt(Selected.Id, world.LocationId, Point.x, Point.z, Rotation);
        }
    }
}
