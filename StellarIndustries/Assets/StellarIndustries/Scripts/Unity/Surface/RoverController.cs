using UnityEngine;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// Six-wheeled construction rover: W/S throttle, A/D steer. Follows the terrain (no physics engine needed),
    /// tilts with the slope, spins its wheels. Lets the engineer build farther away and cross the area quickly.
    /// </summary>
    public class RoverController : MonoBehaviour
    {
        public const float MaxSpeed = 14f;
        SurfaceWorld world;
        float speed, yaw;
        public bool Driven;
        readonly Transform[] wheels = new Transform[6];
        Light[] lights;
        float trackDist;

        public void Init(SurfaceWorld w, Vector3 pos)
        {
            world = w;
            gameObject.layer = 2;
            transform.position = new Vector3(pos.x, w.HeightAt(pos.x, pos.z), pos.z);
            yaw = -30;
            var body = StructureFactory.Mat("rover", new Color(0.93f, 0.93f, 0.95f), 0.4f);
            var orange = StructureFactory.Mat("suit_orange", new Color(1f, 0.5f, 0.12f), 0.35f);
            var dark = StructureFactory.Mat("dark", new Color(0.22f, 0.24f, 0.28f), 0.4f);
            var glass = StructureFactory.Mat("glass", new Color(0.35f, 0.8f, 1f), 0, true);
            StructureFactory.P(PrimitiveType.Cube, transform, new Vector3(0, 1.1f, 0), new Vector3(2.6f, 0.7f, 4.6f), body);
            StructureFactory.P(PrimitiveType.Cube, transform, new Vector3(0, 1.75f, 0.9f), new Vector3(2.2f, 0.8f, 1.8f), body);
            StructureFactory.P(PrimitiveType.Cube, transform, new Vector3(0, 1.8f, 1.81f), new Vector3(2f, 0.55f, 0.05f), glass);
            StructureFactory.P(PrimitiveType.Cube, transform, new Vector3(0, 1.5f, -1.4f), new Vector3(2.4f, 0.12f, 1.6f), orange);
            StructureFactory.P(PrimitiveType.Cylinder, transform, new Vector3(-0.8f, 2.6f, -1.6f), new Vector3(0.05f, 0.6f, 0.05f), dark);
            StructureFactory.P(PrimitiveType.Sphere, transform, new Vector3(-0.8f, 3.25f, -1.6f), new Vector3(0.7f, 0.15f, 0.7f), body, new Vector3(30, 0, 0));
            for (int i = 0; i < 6; i++)
            {
                var w2 = new GameObject("Wheel").transform;
                w2.SetParent(transform, false);
                w2.localPosition = new Vector3(i % 2 == 0 ? -1.45f : 1.45f, 0.55f, -1.6f + (i / 2) * 1.6f);
                StructureFactory.P(PrimitiveType.Cylinder, w2, Vector3.zero, new Vector3(1.1f, 0.22f, 1.1f), dark, new Vector3(0, 0, 90));
                wheels[i] = w2;
            }
            lights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                var l = new GameObject("Headlight").AddComponent<Light>();
                l.transform.SetParent(transform, false); l.transform.localPosition = new Vector3(i == 0 ? -0.8f : 0.8f, 1.3f, 2.35f);
                l.type = LightType.Spot; l.spotAngle = 60; l.range = 45; l.intensity = 0; l.color = new Color(1f, 0.95f, 0.85f);
                lights[i] = l;
            }
            Align(1);
        }

        void Update()
        {
            if (world == null || !world.Active) return;
            float dt = Time.deltaTime, g = world.GravityMs2;
            if (Driven)
            {
                float throttle = (SurfaceInput.Held(KeyCode.W) ? 1 : 0) - (SurfaceInput.Held(KeyCode.S) ? 1 : 0);
                float steer = (SurfaceInput.Held(KeyCode.D) ? 1 : 0) - (SurfaceInput.Held(KeyCode.A) ? 1 : 0);
                float grip = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(g / 9.81f)); // less grip in low gravity
                speed = Mathf.MoveTowards(speed, throttle * MaxSpeed * (throttle < 0 ? 0.4f : 1), (throttle == 0 ? 6 : 5) * grip * dt);
                yaw += steer * Mathf.Clamp01(Mathf.Abs(speed) / 3f) * Mathf.Sign(speed == 0 ? 1 : speed) * 55 * dt;
            }
            else speed = Mathf.MoveTowards(speed, 0, 8 * dt);
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var p = transform.position + fwd * speed * dt;
            float half = (float)world.Def.SurfaceHalfSizeM + 70;
            p.x = Mathf.Clamp(p.x, -half, half); p.z = Mathf.Clamp(p.z, -half, half);
            if (!Blocked(p)) transform.position = new Vector3(p.x, transform.position.y, p.z); else speed = 0;
            Align(dt);
            foreach (var w in wheels) w.Rotate(speed * dt / 0.55f * Mathf.Rad2Deg, 0, 0, Space.Self);
            float sp = Mathf.Abs(speed);
            if (sp > 1.5f)
            {
                // Rooster tails: on the Moon they arc in perfect parabolas and drop straight back (no air).
                var back = -transform.forward;
                foreach (var i in new[] { 0, 1 })
                {
                    var wheel = wheels[i].position;
                    world.Dust?.Burst(wheel, back * sp * 0.35f + Vector3.up * (1.2f + sp * 0.18f), 0.8f, Mathf.CeilToInt(sp * 0.25f));
                }
                trackDist += sp * dt;
                if (trackDist > 1.4f)
                {
                    trackDist = 0;
                    foreach (var i in new[] { 0, 1 }) world.Prints?.Stamp(wheels[i].position, transform.eulerAngles.y, new Vector2(0.5f, 1.5f));
                }
            }
            foreach (var l in lights) l.intensity = Driven ? 2.2f : 0;
        }

        bool Blocked(Vector3 p)
        {
            var b = world.NearestBuilding(p, 2.0f, out _);
            return b != null;
        }

        void Align(float dt)
        {
            var p = transform.position;
            float y = world.HeightAt(p.x, p.z);
            var n = world.NormalAt(p.x, p.z);
            var target = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, yaw, 0);
            transform.position = new Vector3(p.x, Mathf.Lerp(p.y, y, 1 - Mathf.Exp(-12 * dt)), p.z);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1 - Mathf.Exp(-8 * dt));
        }

        public Vector3 SeatExit => transform.position + transform.rotation * new Vector3(-2.6f, 0.3f, 0);
    }
}
