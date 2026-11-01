using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Unity.Performance;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// Delivery drones that fly the base's real supply links (a building that produces X -> the nearest building that
    /// consumes X), carrying a crate in the resource's colour. How they fly depends on physics:
    ///  - with an atmosphere: quadcopters cruising at constant altitude. Thin Martian air (~1% of Earth's density)
    ///    needs larger, much faster rotors - like the Ingenuity helicopter.
    ///  - without air (Moon): rotors cannot work, so "hopper" drones fly ballistic arcs under the real gravity, firing
    ///    thrusters only for take-off and landing. Lunar hops are slow and floaty, exactly as the physics says.
    /// </summary>
    public class DroneSwarm : MonoBehaviour
    {
        class Link { public Vector3 From, To; public Color Color; }
        class Drone
        {
            public Transform T, Crate, Flame; public Renderer CrateR; public Transform[] Rotors;
            public Link Link; public bool Loaded; public float Phase, Duration, Wait, Vy, Apex;
            public Vector3 A, B;
        }

        SurfaceWorld world;
        readonly List<Drone> drones = new List<Drone>();
        readonly List<Link> links = new List<Link>();
        float relink, g;
        bool hopper, mars;
        Material body, dark, accent, flameMat;

        public void Init(SurfaceWorld w)
        {
            world = w;
            g = w.GravityMs2;
            hopper = w.Airless;
            mars = w.Def.Id == "mars";
            body = StructureFactory.Mat("drone", new Color(0.9f, 0.91f, 0.93f), 0.5f);
            dark = StructureFactory.Mat("dark", new Color(0.22f, 0.24f, 0.28f), 0.4f);
            accent = StructureFactory.Mat("suit_orange", new Color(1f, 0.5f, 0.12f), 0.35f);
            flameMat = new Material(VisualFactory.UnlitShader) { mainTexture = VisualFactory.GlowTexture, color = new Color(0.6f, 0.85f, 1f, 1f) };
            relink = 0;
        }

        void RebuildLinks()
        {
            links.Clear();
            var sim = world.Sim;
            var here = sim.State.Buildings.Where(b => b.LocationId == world.LocationId && b.Placed && b.Enabled).ToList();
            var producers = new Dictionary<string, List<Core.State.BuildingInstance>>();
            foreach (var b in here)
            {
                var r = sim.Db.Recipe(b.RecipeId);
                if (r == null) continue;
                foreach (var o in r.Outputs)
                {
                    if (!producers.TryGetValue(o.ResourceId, out var l)) producers[o.ResourceId] = l = new List<Core.State.BuildingInstance>();
                    l.Add(b);
                }
            }
            foreach (var b in here)
            {
                var r = sim.Db.Recipe(b.RecipeId);
                if (r == null) continue;
                foreach (var i in r.Inputs)
                {
                    if (!producers.TryGetValue(i.ResourceId, out var list) || list.Count == 0) continue;
                    var src = list.OrderBy(p => (p.PosX - b.PosX) * (p.PosX - b.PosX) + (p.PosZ - b.PosZ) * (p.PosZ - b.PosZ)).First();
                    if (src.Uid == b.Uid) continue;
                    links.Add(new Link { From = world.GroundPoint(src.PosX, src.PosZ), To = world.GroundPoint(b.PosX, b.PosZ), Color = VisualFactory.Hex(sim.Db.Resource(i.ResourceId)?.ColorHex, Color.gray) });
                    if (links.Count >= GraphicsQuality.MaxDrones) return;
                }
            }
        }

        Drone CreateDrone()
        {
            var d = new Drone { T = new GameObject("Drone").transform };
            d.T.SetParent(transform, false);
            if (hopper)
            {
                StructureFactory.P(PrimitiveType.Sphere, d.T, Vector3.zero, new Vector3(1.4f, 0.8f, 1.4f), body);
                for (int i = 0; i < 3; i++) StructureFactory.P(PrimitiveType.Cylinder, d.T, Quaternion.Euler(0, i * 120, 0) * new Vector3(0, -0.5f, 0.6f), new Vector3(0.08f, 0.45f, 0.08f), dark, new Vector3(25, i * 120, 0));
                StructureFactory.P(PrimitiveType.Cylinder, d.T, new Vector3(0, -0.45f, 0), new Vector3(0.35f, 0.15f, 0.35f), dark);
                d.Flame = VisualFactory.Billboard("Thruster", d.T, VisualFactory.GlowTexture, new Color(0.6f, 0.85f, 1f, 1f), 1.6f).transform;
                d.Flame.localPosition = new Vector3(0, -0.9f, 0);
            }
            else
            {
                StructureFactory.P(PrimitiveType.Cube, d.T, Vector3.zero, new Vector3(0.9f, 0.3f, 0.9f), body);
                StructureFactory.P(PrimitiveType.Cube, d.T, new Vector3(0, 0.05f, 0.5f), new Vector3(0.3f, 0.12f, 0.1f), accent);
                float arm = mars ? 1.25f : 0.8f, blade = mars ? 1.7f : 1.0f; // thin air: bigger rotors
                d.Rotors = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    var dir = Quaternion.Euler(0, 45 + i * 90, 0) * Vector3.forward;
                    StructureFactory.P(PrimitiveType.Cube, d.T, dir * arm * 0.5f, new Vector3(0.1f, 0.08f, arm), dark, new Vector3(0, 45 + i * 90, 0));
                    var rotor = new GameObject("Rotor").transform; rotor.SetParent(d.T, false); rotor.localPosition = dir * arm + Vector3.up * 0.18f;
                    StructureFactory.P(PrimitiveType.Cube, rotor, Vector3.zero, new Vector3(blade, 0.02f, 0.12f), dark);
                    d.Rotors[i] = rotor;
                }
            }
            var crate = StructureFactory.P(PrimitiveType.Cube, d.T, new Vector3(0, hopper ? -0.2f : -0.45f, 0), Vector3.one * 0.55f, body);
            d.Crate = crate.transform; d.CrateR = crate.GetComponent<Renderer>();
            d.CrateR.sharedMaterial = new Material(body);
            VisualFactory.Shadows(d.T.gameObject);
            return d;
        }

        void StartLeg(Drone d)
        {
            d.Loaded = !d.Loaded;
            d.A = d.Loaded ? d.Link.From : d.Link.To;
            d.B = d.Loaded ? d.Link.To : d.Link.From;
            d.A.y += 1.2f; d.B.y += 1.2f;
            float dist = Vector2.Distance(new Vector2(d.A.x, d.A.z), new Vector2(d.B.x, d.B.z));
            d.Phase = 0;
            if (hopper)
            {
                // Ballistic hop: choose the apex height, launch speed from v = sqrt(2 g h), flight time from the landing height.
                d.Apex = Mathf.Clamp(dist * 0.3f, 15, 60);
                var hop = Core.Systems.SpacePhysics.BallisticHop(d.Apex, d.B.y - d.A.y, g);
                d.Vy = (float)hop.vy;
                d.Duration = (float)hop.time;
            }
            else d.Duration = dist / (mars ? 9f : 13f) + 5f;
            d.CrateR.sharedMaterial.color = d.Loaded ? d.Link.Color : new Color(0.5f, 0.52f, 0.55f);
            d.Crate.gameObject.SetActive(d.Loaded);
        }

        void Update()
        {
            if (world == null || !world.Active) return;
            float dt = Time.deltaTime;
            relink -= dt;
            if (relink <= 0)
            {
                relink = 6;
                RebuildLinks();
                while (drones.Count < links.Count) { var d = CreateDrone(); d.Link = links[drones.Count]; d.Loaded = Random.value > 0.5f; d.Wait = Random.value * 4; d.T.position = d.Link.From + Vector3.up; drones.Add(d); StartLeg(d); }
                while (drones.Count > links.Count) { Destroy(drones[drones.Count - 1].T.gameObject); drones.RemoveAt(drones.Count - 1); }
                for (int i = 0; i < drones.Count; i++) drones[i].Link = links[i];
            }
            foreach (var d in drones)
            {
                if (d.Wait > 0) { d.Wait -= dt; if (hopper && d.Flame != null) d.Flame.localScale = Vector3.zero; SpinRotors(d, dt, d.Wait > 0.5f ? 0.2f : 1); continue; }
                d.Phase += dt;
                float t = d.Phase;
                Vector3 p;
                if (hopper)
                {
                    float k = Mathf.Clamp01(t / d.Duration);
                    p = Vector3.Lerp(d.A, d.B, k);
                    p.y = d.A.y + d.Vy * t - 0.5f * g * t * t;
                    bool burning = t < 0.35f || d.Duration - t < 0.6f; // thrust only at take-off and landing
                    if (d.Flame != null) d.Flame.localScale = Vector3.one * (burning ? 1.6f + Random.value * 0.6f : 0);
                    if (t <= dt) world.Dust?.Burst(new Vector3(p.x, world.HeightAt(p.x, p.z), p.z), Vector3.zero, 3.5f, 30);
                }
                else
                {
                    float rise = 2.5f, cruise = Mathf.Max(d.A.y, d.B.y) + 14;
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - rise) / Mathf.Max(0.1f, d.Duration - 2 * rise)));
                    p = Vector3.Lerp(d.A, d.B, k);
                    float up = Mathf.Clamp01(t / rise) * Mathf.Clamp01((d.Duration - t) / rise);
                    p.y = Mathf.Lerp(Mathf.Lerp(d.A.y, d.B.y, k), cruise, Mathf.SmoothStep(0, 1, up)) + Mathf.Sin(Time.time * 2.3f + d.A.x) * 0.12f;
                    var vel = (d.B - d.A).normalized;
                    float tilt = (k > 0.02f && k < 0.98f) ? 12 : 0;
                    d.T.rotation = Quaternion.Slerp(d.T.rotation, Quaternion.LookRotation(new Vector3(vel.x, 0, vel.z) + Vector3.forward * 1e-4f) * Quaternion.Euler(tilt, 0, 0), 1 - Mathf.Exp(-4 * dt));
                    SpinRotors(d, dt, 1);
                }
                d.T.position = p;
                if (t >= d.Duration)
                {
                    if (hopper) world.Dust?.Burst(new Vector3(p.x, world.HeightAt(p.x, p.z), p.z), Vector3.zero, 3.5f, 30);
                    d.Wait = 1.5f; StartLeg(d);
                }
            }
        }

        void SpinRotors(Drone d, float dt, float power)
        {
            if (d.Rotors == null) return;
            float speed = (mars ? 3600 : 2000) * power;
            for (int i = 0; i < d.Rotors.Length; i++) d.Rotors[i].Rotate(0, (i % 2 == 0 ? 1 : -1) * speed * dt, 0, Space.Self);
        }
    }
}
