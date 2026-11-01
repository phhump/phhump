using UnityEngine;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>Third-person camera: right mouse drag orbits, wheel zooms, never dips below the terrain.</summary>
    public class SurfaceCamera : MonoBehaviour
    {
        SurfaceWorld world;
        Camera cam;
        float yaw = 20, pitch = 18, distance = 9, targetDistance = 9;
        Vector3 focus;

        public void Init(SurfaceWorld w)
        {
            world = w;
            cam = Camera.main;
            focus = w.Engineer.transform.position;
        }

        void LateUpdate()
        {
            if (world == null || !world.Active || cam == null) return;
            float dt = Time.deltaTime;
            bool rover = world.Engineer.InRover;
            if (SurfaceInput.RightHeld && !SurfaceInput.Blocked) { yaw += SurfaceInput.LookDelta.x * 0.25f; pitch = Mathf.Clamp(pitch + SurfaceInput.LookDelta.y * 0.2f, -10, 80); }
            if (!SurfaceInput.Blocked && SurfaceInput.Scroll != 0) targetDistance = Mathf.Clamp(targetDistance * (1 + SurfaceInput.Scroll * 0.06f), rover ? 8 : 3, rover ? 60 : 45);
            var target = rover ? world.Rover.transform.position + Vector3.up * 2.2f : world.Engineer.transform.position + Vector3.up * 1.6f;
            if (rover && !SurfaceInput.RightHeld) yaw = Mathf.LerpAngle(yaw, world.Rover.transform.eulerAngles.y, 1 - Mathf.Exp(-2.5f * dt));
            focus = Vector3.Lerp(focus, target, 1 - Mathf.Exp(-12 * dt));
            distance = Mathf.Lerp(distance, targetDistance, 1 - Mathf.Exp(-8 * dt));
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var pos = focus + rot * new Vector3(0, 0, -distance);
            float ground = world.HeightAt(pos.x, pos.z) + 0.8f;
            if (pos.y < ground) pos.y = ground;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(focus - pos);
            cam.rect = new Rect(0, 0, 1, 1);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 9000; cam.fieldOfView = 55;
        }

        public float Yaw => yaw;
    }
}
