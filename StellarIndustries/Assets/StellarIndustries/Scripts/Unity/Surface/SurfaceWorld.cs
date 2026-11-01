using System.Collections.Generic;
using System.Linq;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Systems;
using StellarIndustries.Unity.Views;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// The walkable surface of one location: procedural terrain (relief, craters), sky and lighting per world,
    /// deposit fields, building models at their simulation positions, the engineer and the rover.
    /// Purely presentation: all rules (placement, costs, production) live in the core simulation.
    /// </summary>
    public class SurfaceWorld : MonoBehaviour
    {
        public bool Active { get; private set; }
        public string LocationId { get; private set; }
        public GameSimulation Sim { get; private set; }
        public LocationDef Def { get; private set; }
        public EngineerController Engineer { get; private set; }
        public RoverController Rover { get; private set; }
        public SurfaceCamera CameraRig { get; private set; }
        public BuildMode Build { get; private set; }
        public readonly Dictionary<int, GameObject> Structures = new Dictionary<int, GameObject>();
        public float GravityMs2 => Def == null ? 9.81f : Def.BodyType == BodyType.GasGiant ? 9.81f : (float)(Def.Gravity * 9.80665);
        /// <summary>No usable atmosphere: no rotors, no drag, black sky.</summary>
        public bool Airless => Def != null && VisualFactory.Hex(Def.SkyColorHex, Color.black).maxColorComponent < 0.08f;
        public DustSystem Dust { get; private set; }
        public Footprints Prints { get; private set; }
        public DroneSwarm Drones { get; private set; }
        Transform decor;
        LightShadows oldShadows;

        Transform root;
        bool dirty;
        float statusTimer;
        uint seed;
        readonly List<(Vector2 c, float r)> craters = new List<(Vector2, float)>();
        float relief;
        Light sun;
        Color oldAmbient; AmbientMode oldAmbientMode; bool oldFog; Color oldFogColor; float oldFogDensity; Quaternion oldSunRot; float oldSunIntensity; Color oldSunColor;
        Color oldBg; CameraClearFlags oldClear;
        readonly List<RocketLaunchFx> launches = new List<RocketLaunchFx>();

        // ------------------------------------------------------------------ terrain
        public float HeightAt(float x, float z)
        {
            float sx = seed % 1000, sz = (seed / 1000) % 1000;
            float h = 0, amp = 1, freq = 0.0045f;
            for (int i = 0; i < 4; i++) { h += (Mathf.PerlinNoise(x * freq + sx, z * freq + sz) - 0.5f) * 2 * amp; amp *= 0.45f; freq *= 2.2f; }
            h *= relief;
            foreach (var (c, r) in craters)
            {
                float d = Vector2.Distance(new Vector2(x, z), c) / r;
                if (d > 1.6f) continue;
                if (d < 1) h -= r * 0.16f * (1 - d * d);
                h += r * 0.06f * Mathf.Exp(-Mathf.Pow((d - 1.05f) / 0.17f, 2));
            }
            float spawn = Mathf.Sqrt(x * x + z * z);
            return Mathf.Lerp(0, h, Mathf.SmoothStep(0, 1, (spawn - 18) / 40f));
        }

        public Vector3 GroundPoint(double x, double z) => new Vector3((float)x, HeightAt((float)x, (float)z), (float)z);

        public Vector3 NormalAt(float x, float z)
        {
            float e = 1.5f;
            return new Vector3(HeightAt(x - e, z) - HeightAt(x + e, z), 2 * e, HeightAt(x, z - e) - HeightAt(x, z + e)).normalized;
        }

        Mesh TerrainMesh(float half, int n, float yOffset, float flattenBeyond)
        {
            var verts = new Vector3[(n + 1) * (n + 1)];
            var uv = new Vector2[verts.Length];
            for (int iz = 0, k = 0; iz <= n; iz++)
                for (int ix = 0; ix <= n; ix++, k++)
                {
                    float x = -half + 2 * half * ix / n, z = -half + 2 * half * iz / n;
                    float h = HeightAt(x, z);
                    float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (r > flattenBeyond) h *= Mathf.Clamp01(1 - (r - flattenBeyond) / 1200f);
                    verts[k] = new Vector3(x, h + yOffset, z);
                    uv[k] = new Vector2(x / 24f, z / 24f);
                }
            var tris = new int[n * n * 6];
            for (int iz = 0, t = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    int a = iz * (n + 1) + ix;
                    tris[t++] = a; tris[t++] = a + n + 1; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = a + n + 1; tris[t++] = a + n + 2;
                }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, vertices = verts, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        Texture2D GroundTexture(Color baseColor)
        {
            int s = 512;
            var tex = new Texture2D(s, s, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var px = new Color[s * s];
            var rnd = new System.Random((int)seed);
            var waves = Enumerable.Range(0, 14).Select(i => (kx: rnd.Next(1, 9 + i * 3), ky: rnd.Next(1, 9 + i * 3), ph: (float)rnd.NextDouble() * 6.28f, a: 1f / (1 + i * 0.6f))).ToArray();
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float v = 0, norm = 0;
                    foreach (var w in waves) { v += Mathf.Sin(2 * Mathf.PI * (w.kx * x + w.ky * y) / s + w.ph) * w.a; norm += w.a; }
                    v = v / norm * 0.5f + 0.5f;
                    float grain = (float)rnd.NextDouble();
                    float shade = 0.72f + v * 0.4f + (grain - 0.5f) * 0.12f + (grain > 0.985f ? 0.25f : 0);
                    px[y * s + x] = baseColor * shade;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // ------------------------------------------------------------------ lifecycle
        public void Enter(GameSimulation sim, string locationId, Vector3? spawn = null)
        {
            if (Active) Exit();
            Sim = sim; LocationId = locationId; Def = sim.Db.Location(locationId);
            seed = 2166136261; foreach (char c in locationId) { seed ^= c; seed *= 16777619; }
            relief = (float)Def.TerrainReliefM;
            craters.Clear();
            var rnd = new System.Random((int)(seed & 0x7fffffff));
            int count = (int)(Def.CraterDensity * 70);
            float half = (float)Def.SurfaceHalfSizeM;
            for (int i = 0; i < count; i++) craters.Add((new Vector2((float)(rnd.NextDouble() * 2 - 1) * (half + 300), (float)(rnd.NextDouble() * 2 - 1) * (half + 300)), 5 + (float)System.Math.Pow(rnd.NextDouble(), 2.2) * 45));

            root = new GameObject("Surface " + Def.Name).transform;
            decor = new GameObject("Static decor").transform;
            decor.SetParent(root, false);
            BuildTerrain(half);
            BuildSky();
            BuildDepositFields();
            BuildLandingPad();
            Engineer = new GameObject("Engineer").AddComponent<EngineerController>();
            Engineer.transform.SetParent(root, false);
            Engineer.Init(this, spawn ?? new Vector3(0, 0.2f, -8));
            Rover = new GameObject("Rover").AddComponent<RoverController>();
            Rover.transform.SetParent(root, false);
            Rover.Init(this, new Vector3(9, 0, -4));
            CameraRig = new GameObject("Surface camera").AddComponent<SurfaceCamera>();
            CameraRig.transform.SetParent(root, false);
            CameraRig.Init(this);
            Build = new GameObject("Build mode").AddComponent<BuildMode>();
            Build.transform.SetParent(root, false);
            Build.Init(this);
            Dust = new GameObject("Dust").AddComponent<DustSystem>();
            Dust.transform.SetParent(root, false);
            Dust.Init(this);
            Prints = new GameObject("Footprints").AddComponent<Footprints>();
            Prints.transform.SetParent(root, false);
            Prints.Init(this);
            Drones = new GameObject("Drones").AddComponent<DroneSwarm>();
            Drones.transform.SetParent(root, false);
            Drones.Init(this);
            CombineStatic(decor.gameObject);
            GetComponent<Performance.PerformanceMonitor>()?.Grace();
            sim.Events.BuildingsChanged += OnBuildingsChanged;
            sim.Events.RocketLaunched += OnRocketLaunched;
            Active = true;
            dirty = true;
            SurfaceInput.ClearAll();
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            if (Sim != null) { Sim.Events.BuildingsChanged -= OnBuildingsChanged; Sim.Events.RocketLaunched -= OnRocketLaunched; }
            RestoreRendering();
            Structures.Clear();
            launches.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
            SurfaceInput.ClearAll();
        }

        void OnBuildingsChanged(int uid) => dirty = true;

        void BuildTerrain(float half)
        {
            var ground = VisualFactory.Lit(Color.white, 0.08f, GroundTexture(VisualFactory.Hex(Def.GroundColorHex, Color.gray)));
            var inner = new GameObject("Terrain");
            inner.transform.SetParent(root, false);
            var mesh = TerrainMesh(half + 80, Performance.GraphicsQuality.TerrainGrid, 0, 1e9f);
            inner.AddComponent<MeshFilter>().sharedMesh = mesh;
            var terrainRenderer = inner.AddComponent<MeshRenderer>();
            terrainRenderer.sharedMaterial = ground;
            terrainRenderer.receiveShadows = Performance.GraphicsQuality.Shadows;
            terrainRenderer.shadowCastingMode = Performance.GraphicsQuality.SoftShadows ? ShadowCastingMode.On : ShadowCastingMode.Off; // crater rims self-shadow on High+
            inner.AddComponent<MeshCollider>().sharedMesh = mesh;
            // Horizon ring: coarse, slightly lower, reaches the real horizon distance (~2.4 km on the Moon, ~4.7 km on Earth for eye height).
            var outer = new GameObject("Horizon terrain");
            outer.transform.SetParent(root, false);
            outer.AddComponent<MeshFilter>().sharedMesh = TerrainMesh(5000, Performance.GraphicsQuality.HorizonGrid, -0.6f, half + 60);
            var outerR = outer.AddComponent<MeshRenderer>();
            outerR.sharedMaterial = ground;
            outerR.shadowCastingMode = ShadowCastingMode.Off;
            // Survey stakes along the buildable border.
            var stake = StructureFactory.Mat("stake", new Color(1f, 0.55f, 0.15f), 0.2f);
            for (float p = -half; p <= half; p += 50)
                foreach (var v in new[] { new Vector2(p, -half), new Vector2(p, half), new Vector2(-half, p), new Vector2(half, p) })
                    StructureFactory.P(PrimitiveType.Cube, decor, new Vector3(v.x, HeightAt(v.x, v.y) + 0.8f, v.y), new Vector3(0.25f, 1.6f, 0.25f), stake);
            // Scattered rocks (airless worlds get more).
            var rockMat = StructureFactory.Mat("rock_" + Def.Id, VisualFactory.Hex(Def.GroundColorHex, Color.gray) * 0.75f, 0.05f);
            var rnd = new System.Random((int)(seed >> 3));
            int rocks = Def.BodyType == BodyType.GasGiant ? 0 : (int)((120 + Def.CraterDensity * 200) * Performance.GraphicsQuality.DecorationDensity);
            for (int i = 0; i < rocks; i++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1) * (half + 60), z = (float)(rnd.NextDouble() * 2 - 1) * (half + 60);
                if (x * x + z * z < 40 * 40) continue;
                float s = 0.3f + (float)System.Math.Pow(rnd.NextDouble(), 3) * 2.2f;
                var rock = StructureFactory.P(PrimitiveType.Sphere, decor, new Vector3(x, HeightAt(x, z) + s * 0.15f, z), new Vector3(s * 1.3f, s * 0.7f, s), rockMat, new Vector3(0, (float)rnd.NextDouble() * 360, 0));
                if (Performance.GraphicsQuality.SoftShadows && s > 1) rock.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            }
        }

        void BuildSky()
        {
            var cam = Camera.main;
            oldBg = cam.backgroundColor; oldClear = cam.clearFlags;
            oldAmbient = RenderSettings.ambientLight; oldAmbientMode = RenderSettings.ambientMode;
            oldFog = RenderSettings.fog; oldFogColor = RenderSettings.fogColor; oldFogDensity = RenderSettings.fogDensity;
#if UNITY_2023_1_OR_NEWER
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) sun = l;
#else
            foreach (var l in Object.FindObjectsOfType<Light>()) if (l.type == LightType.Directional) sun = l;
#endif
            var sky = VisualFactory.Hex(Def.SkyColorHex, Color.black);
            bool airless = sky.maxColorComponent < 0.08f;
            var star = Sim.Db.StarSystem(Def.StarSystem);
            var starColor = Color.Lerp(VisualFactory.Hex(star?.ColorHex ?? "#FFF4E0", Color.white), Color.white, 0.55f);
            if (sun != null)
            {
                oldSunRot = sun.transform.rotation; oldSunIntensity = sun.intensity; oldSunColor = sun.color; oldShadows = sun.shadows;
                sun.shadows = Performance.GraphicsQuality.SoftShadows ? LightShadows.Soft : Performance.GraphicsQuality.Shadows ? LightShadows.Hard : LightShadows.None;
                sun.shadowStrength = Airless ? 0.95f : 0.75f; // no sky light fills shadows on airless worlds
                sun.transform.rotation = Quaternion.Euler(38, -35, 0);
                sun.intensity = Mathf.Clamp((float)SpacePhysics.SolarFactor(Def), 0.55f, 1.5f) * (airless ? 1.25f : 1.05f);
                sun.color = starColor;
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = airless ? new Color(0.05f, 0.05f, 0.06f) : Color.Lerp(sky, Color.white, 0.2f) * 0.42f;
            RenderSettings.fog = !airless;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(sky, Color.white, 0.15f);
            RenderSettings.fogDensity = Def.Id == "mars" ? 0.0011f : 0.0007f;

            var anchor = new GameObject("Sky anchor").transform;
            anchor.SetParent(root, false);
            anchor.gameObject.AddComponent<FollowCamera>();
            if (airless) VisualFactory.SkySphere(anchor, 4500);
            // The star's disc at its real angular size, exaggerated 3x so it reads on screen.
            double angular = Def.StarSystem == "sol" ? 1.0 / System.Math.Max(0.1, Def.DistanceAU) : (star?.RadiusSun ?? 1) / System.Math.Max(0.005, Def.DistanceAU);
            if (sun != null)
            {
                var dir = -sun.transform.forward;
                var disc = VisualFactory.Billboard("Star disc", anchor, VisualFactory.GlowTexture, new Color(starColor.r, starColor.g, starColor.b, 1), (float)(4000 * 0.0093 * angular * 3 * 2.2));
                disc.transform.localPosition = dir * 4000;
                var halo = VisualFactory.Billboard("Star halo", anchor, VisualFactory.GlowTexture, new Color(starColor.r, starColor.g, starColor.b, airless ? 0.25f : 0.45f), (float)(4000 * 0.0093 * angular * 18));
                halo.transform.localPosition = dir * 4010;
            }
            // From the Moon, Earth hangs in the sky (angular diameter ~1.9 degrees), lit by the sun - it shows phases.
            if (Def.Id == "moon")
            {
                var earthDef = Sim.Db.Location("earth");
                var earth = MeshFactory.Smooth(VisualFactory.Primitive(PrimitiveType.Sphere, "Earth in the sky", anchor), Performance.GraphicsQuality.SphereSegments);
                earth.transform.localPosition = Quaternion.Euler(-42, 120, 0) * Vector3.forward * 3500;
                earth.transform.localScale = Vector3.one * (3500 * Mathf.Tan(1.9f * Mathf.Deg2Rad));
                earth.GetComponent<Renderer>().sharedMaterial = VisualFactory.PlanetMaterial(earthDef);
                earth.transform.localRotation = Quaternion.Euler(0, 0, -23.44f);
            }
        }

        void RestoreRendering()
        {
            var cam = Camera.main;
            if (cam != null) { cam.backgroundColor = oldBg; cam.clearFlags = oldClear; }
            RenderSettings.ambientLight = oldAmbient; RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.fog = oldFog; RenderSettings.fogColor = oldFogColor; RenderSettings.fogDensity = oldFogDensity;
            if (sun != null) { sun.transform.rotation = oldSunRot; sun.intensity = oldSunIntensity; sun.color = oldSunColor; sun.shadows = oldShadows; }
        }

        void BuildDepositFields()
        {
            foreach (var p in Sim.Placement.Patches(LocationId))
            {
                var res = Sim.Db.Resource(p.ResourceId);
                var c = VisualFactory.Hex(res?.ColorHex, Color.gray);
                bool water = p.ResourceId == "water";
                var go = new GameObject("Field " + p.ResourceId);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = DiscMesh((float)p.X, (float)p.Z, (float)p.Radius, water ? 0.25f : 0.12f);
                var fill = water ? VisualFactory.Lit(new Color(0.08f, 0.25f, 0.45f), 0.95f) : VisualFactory.Unlit(new Color(c.r, c.g, c.b, 0.28f));
                go.AddComponent<MeshRenderer>().sharedMaterial = fill;
                var ring = new GameObject("Ring").AddComponent<LineRenderer>();
                ring.transform.SetParent(go.transform, false);
                ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 72; ring.widthMultiplier = 0.35f;
                ring.sharedMaterial = VisualFactory.Unlit(new Color(c.r, c.g, c.b, 0.75f));
                for (int i = 0; i < 72; i++)
                {
                    float a = i / 72f * Mathf.PI * 2, x = (float)p.X + Mathf.Cos(a) * (float)p.Radius, z = (float)p.Z + Mathf.Sin(a) * (float)p.Radius;
                    ring.SetPosition(i, new Vector3(x, HeightAt(x, z) + 0.2f, z));
                }
                if (!water)
                {
                    var ore = StructureFactory.Mat("orerock_" + p.ResourceId, c, 0.25f);
                    var rnd = new System.Random(p.ResourceId.GetHashCode() ^ (int)p.X);
                    int ores = Mathf.Max(4, (int)(14 * Performance.GraphicsQuality.DecorationDensity));
                    for (int i = 0; i < ores; i++)
                    {
                        float a = (float)rnd.NextDouble() * 6.28f, d = (float)System.Math.Sqrt(rnd.NextDouble()) * (float)p.Radius * 0.9f;
                        float x = (float)p.X + Mathf.Cos(a) * d, z = (float)p.Z + Mathf.Sin(a) * d, s = 0.5f + (float)rnd.NextDouble() * 1.2f;
                        StructureFactory.P(PrimitiveType.Sphere, decor, new Vector3(x, HeightAt(x, z) + s * 0.2f, z), new Vector3(s * 1.4f, s * 0.8f, s), ore);
                    }
                }
            }
        }

        Mesh DiscMesh(float cx, float cz, float r, float lift)
        {
            int rings = 8, seg = 48;
            var v = new List<Vector3> { new Vector3(cx, HeightAt(cx, cz) + lift, cz) };
            var t = new List<int>();
            for (int i = 1; i <= rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    float rr = r * i / rings, a = j / (float)seg * Mathf.PI * 2, x = cx + Mathf.Cos(a) * rr, z = cz + Mathf.Sin(a) * rr;
                    v.Add(new Vector3(x, HeightAt(x, z) + lift, z));
                }
            for (int j = 0; j < seg; j++) { t.Add(0); t.Add(1 + (j + 1) % seg); t.Add(1 + j); }
            for (int i = 1; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = 1 + (i - 1) * seg + j, b = 1 + (i - 1) * seg + (j + 1) % seg, c = a + seg, d = b + seg;
                    t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c);
                }
            var m = new Mesh { vertices = v.ToArray(), triangles = t.ToArray() };
            m.RecalculateNormals();
            return m;
        }

        void BuildLandingPad()
        {
            StructureFactory.P(PrimitiveType.Cylinder, root, new Vector3(0, -0.6f, 0), new Vector3(30, 0.8f, 30), StructureFactory.Mat("pad", new Color(0.32f, 0.33f, 0.35f), 0.2f));
            var mark = StructureFactory.Mat("padmark", new Color(1f, 0.6f, 0.15f), 0.2f);
            StructureFactory.P(PrimitiveType.Cube, root, new Vector3(0, -0.15f, 0), new Vector3(16, 0.05f, 1.2f), mark);
            StructureFactory.P(PrimitiveType.Cube, root, new Vector3(0, -0.15f, 0), new Vector3(1.2f, 0.05f, 16), mark);
            // The lander that brought the engineer.
            var white = StructureFactory.Mat("white", new Color(0.92f, 0.93f, 0.95f), 0.45f);
            StructureFactory.P(PrimitiveType.Cylinder, root, new Vector3(-7, 2.6f, 6), new Vector3(3.2f, 1.6f, 3.2f), white);
            StructureFactory.P(PrimitiveType.Sphere, root, new Vector3(-7, 4.4f, 6), new Vector3(3, 2.4f, 3), white);
            for (int i = 0; i < 4; i++) StructureFactory.P(PrimitiveType.Cylinder, root, new Vector3(-7 + (i % 2 - 0.5f) * 4, 0.9f, 6 + (i / 2 - 0.5f) * 4), new Vector3(0.2f, 1.2f, 0.2f), StructureFactory.Mat("dark", new Color(0.22f, 0.24f, 0.28f), 0.4f), new Vector3((i / 2 - 0.5f) * 30, 0, (i % 2 - 0.5f) * -30));
        }

        // ------------------------------------------------------------------ buildings
        void SyncStructures()
        {
            dirty = false;
            var here = Sim.State.Buildings.Where(b => b.LocationId == LocationId && b.Placed).ToList();
            var keep = new HashSet<int>(here.Select(b => b.Uid));
            foreach (var uid in Structures.Keys.ToList())
                if (!keep.Contains(uid)) { Destroy(Structures[uid]); Structures.Remove(uid); }
            foreach (var b in here)
            {
                if (Structures.ContainsKey(b.Uid)) continue;
                var def = Sim.Db.Building(b.DefId);
                var recipe = Sim.Db.Recipe(b.RecipeId);
                var oreColor = VisualFactory.Hex(Sim.Db.Resource(recipe?.RequiredDeposit)?.ColorHex, Color.gray);
                var go = StructureFactory.Create(def, oreColor);
                go.transform.SetParent(root, false);
                go.transform.localPosition = GroundPoint(b.PosX, b.PosZ);
                go.transform.localRotation = Quaternion.Euler(0, (float)b.RotationDeg, 0);
                // Solid for the engineer, invisible to placement raycasts (layer 2 = Ignore Raycast).
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = (float)def.FootprintM * 0.42f; col.height = 8; col.center = new Vector3(0, 4, 0);
                go.layer = 2;
                var light = VisualFactory.Billboard("Status", go.transform, VisualFactory.GlowTexture, Color.green, 2.4f);
                light.transform.localPosition = new Vector3(0, StatusHeight(def), 0);
                var tag = go.AddComponent<StructureTag>();
                tag.Uid = b.Uid;
                // Cache components once: the status refresh runs 4x per second for every building and must not allocate.
                tag.StatusRenderer = light.GetComponent<Renderer>();
                tag.Spins = go.GetComponentsInChildren<Views.Spin>();
                tag.Nods = go.GetComponentsInChildren<Nod>();
                tag.Rocket = go.transform.Find("StandingRocket");
                Structures[b.Uid] = go;
            }
            UpdateStatus();
        }

        static float StatusHeight(BuildingDef def) => def.Id == "wind_turbine" ? 27 : def.Id == "rocket_assembly" ? 34 : def.Id == "surface_mine" ? 12.5f : 9;

        void UpdateStatus()
        {
            bool blink = Mathf.Repeat(Time.time, 1) < 0.5f;
            bool rocketReady = HasRocketReady();
            foreach (var kv in Structures)
            {
                var b = Sim.Construction.Find(kv.Key);
                if (b == null) continue;
                var tag = kv.Value.GetComponent<StructureTag>();
                if (tag == null) continue;
                Color c = b.Status == BuildingStatus.Working ? new Color(0.3f, 1f, 0.55f) : b.Status == BuildingStatus.Paused || b.Status == BuildingStatus.Idle ? new Color(0.5f, 0.55f, 0.6f)
                        : b.Status == BuildingStatus.NoPower || b.Status == BuildingStatus.NoDeposit || b.Status == BuildingStatus.NoFuel ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.7f, 0.2f);
                if (b.Status != BuildingStatus.Working && b.Status != BuildingStatus.Paused && !blink) c *= 0.4f;
                if (tag.StatusRenderer != null) tag.StatusRenderer.sharedMaterial.color = c;
                bool running = b.Status == BuildingStatus.Working || b.Status == BuildingStatus.LowPower;
                if (running != tag.Running || tag.Status != b.Status)
                {
                    foreach (var s in tag.Spins) s.enabled = running;
                    foreach (var n in tag.Nods) n.Active = running;
                    tag.Running = running;
                }
                if (tag.Rocket != null && tag.Rocket.gameObject.activeSelf != rocketReady) tag.Rocket.gameObject.SetActive(rocketReady);
                tag.Status = b.Status;
            }
        }

        bool HasRocketReady() => Sim.Db.Rockets.Any(r => Sim.Inventory.Get(LocationId, r.VehicleResourceId) >= 1);

        void OnRocketLaunched(string missionId, string rocketId, string origin)
        {
            if (origin != LocationId) return;
            var pad = Sim.State.Buildings.FirstOrDefault(b => b.LocationId == LocationId && b.DefId == "rocket_assembly" && b.Placed);
            var pos = pad != null ? GroundPoint(pad.PosX, pad.PosZ) : new Vector3(0, 0, 40);
            launches.Add(RocketLaunchFx.Spawn(root, pos, GravityMs2));
            for (int i = 0; i < 6; i++) Dust?.Burst(pos + Random.insideUnitSphere * 6, new Vector3(Random.Range(-8f, 8f), 2, Random.Range(-8f, 8f)), 4, 40);
        }

        public BuildingInstance NearestBuilding(Vector3 p, float maxDist, out float dist)
        {
            BuildingInstance best = null; dist = maxDist;
            foreach (var b in Sim.State.Buildings)
            {
                if (b.LocationId != LocationId || !b.Placed) continue;
                float r = (float)Sim.Db.Building(b.DefId).FootprintM * 0.5f;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2((float)b.PosX, (float)b.PosZ)) - r;
                if (d < dist) { dist = d; best = b; }
            }
            return best;
        }

        // ------------------------------------------------------------------ interaction
        public string Prompt { get; private set; }
        public int InteractUid { get; private set; }
        /// <summary>Building whose panel is open (0 = none).</summary>
        public int OpenPanelUid;
        /// <summary>Raised when an interaction should open a command-center screen ("research", "missions").</summary>
        public event System.Action<string> OpenScreen;
        public readonly List<(string text, Vector3 pos, float time, Color color)> FloatingTexts = new List<(string, Vector3, float, Color)>();
        public DepositPatch? FieldUnderEngineer { get; private set; }
        float drillTimer;

        void HandleInteraction()
        {
            Prompt = null; InteractUid = 0;
            var eng = Engineer;
            var pos = eng.InRover ? Rover.transform.position : eng.transform.position;
            FieldUnderEngineer = null;
            foreach (var p in Sim.Placement.PatchesAt(LocationId, pos.x, pos.z)) { FieldUnderEngineer = p; break; }

            if (eng.InRover)
            {
                Prompt = "[E] Leave rover";
                if (SurfaceInput.Pressed(KeyCode.E)) { eng.InRover = false; Rover.Driven = false; eng.Teleport(Rover.SeatExit); eng.SetVisible(true); }
                return;
            }
            float roverDist = Vector3.Distance(pos, Rover.transform.position);
            var b = NearestBuilding(pos, 3.5f, out _);
            if (roverDist < 4.5f)
            {
                Prompt = "[E] Drive rover";
                if (SurfaceInput.Pressed(KeyCode.E)) { eng.InRover = true; Rover.Driven = true; eng.SetVisible(false); OpenPanelUid = 0; Build.Cancel(); }
            }
            else if (b != null)
            {
                var def = Sim.Db.Building(b.DefId);
                InteractUid = b.Uid;
                string action = def.Category == BuildingCategory.Research ? "open research terminal" : def.Id == "rocket_assembly" ? "open mission control" : "manage";
                Prompt = "[E] " + def.Name + " (Lv " + b.Level + ") - " + action;
                if (SurfaceInput.Pressed(KeyCode.E))
                {
                    if (def.Category == BuildingCategory.Research) OpenScreen?.Invoke("research");
                    else if (def.Id == "rocket_assembly") OpenScreen?.Invoke("missions");
                    else OpenPanelUid = OpenPanelUid == b.Uid ? 0 : b.Uid;
                }
            }
            if (OpenPanelUid != 0)
            {
                var open = Sim.Construction.Find(OpenPanelUid);
                if (open == null || Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2((float)open.PosX, (float)open.PosZ)) > Sim.Db.Building(open.DefId).FootprintM + 10) OpenPanelUid = 0;
            }

            // Hand drilling inside a deposit field.
            eng.Drilling = false;
            if (FieldUnderEngineer.HasValue && b == null)
            {
                var res = FieldUnderEngineer.Value.ResourceId;
                bool can = Sim.Construction.GatherableResources(LocationId).Exists(r => r.Id == res);
                string name = Sim.Db.ResourceName(res);
                if (Prompt == null) Prompt = can ? "[Hold F] Drill " + name + " by hand" : name + " field - research the extraction technology to mine it";
                if (can && SurfaceInput.Held(KeyCode.F))
                {
                    eng.Drilling = true;
                    drillTimer += Time.deltaTime;
                    if (drillTimer > 0.6f)
                    {
                        drillTimer = 0;
                        double got = Sim.Construction.Gather(LocationId, res);
                        FloatingTexts.Add((got > 0 ? "+" + got.ToString("0.#") + " " + name : "Storage full", eng.transform.position + Vector3.up * 2.2f, Time.time,
                            got > 0 ? VisualFactory.Hex(Sim.Db.Resource(res)?.ColorHex, Color.white) : new Color(1f, 0.7f, 0.2f)));
                    }
                }
            }
            FloatingTexts.RemoveAll(f => Time.time - f.time > 1.6f);
        }

        void Update()
        {
            if (!Active || Sim == null) return;
            HandleInteraction();
            if (dirty) SyncStructures();
            statusTimer += Time.deltaTime;
            if (statusTimer > 0.25f) { statusTimer = 0; UpdateStatus(); }
            for (int i = launches.Count - 1; i >= 0; i--) if (launches[i] == null) launches.RemoveAt(i);
        }

        /// <summary>
        /// Static batching for the many decoration objects (rocks, stakes, ore boulders): hundreds of renderers become a
        /// few draw calls. Only runs when every mesh is CPU-readable (Unity requires it), otherwise instancing still applies.
        /// </summary>
        static void CombineStatic(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;
            StaticBatchingUtility.Combine(go);
        }

        void LateUpdate() { if (Active) SurfaceInput.EndFrame(); }
        public void SurfaceInput_Feed(Event e, float scale) { if (Active) SurfaceInput.Feed(e, scale); }
        public void SurfaceInput_SetBlocked(bool blocked) => SurfaceInput.Blocked = blocked;
        public void SurfaceInput_Clear() => SurfaceInput.ClearAll();
        void OnApplicationFocus(bool focus) { if (!focus) SurfaceInput.ClearAll(); }
    }

    public class StructureTag : MonoBehaviour
    {
        public int Uid; public BuildingStatus Status; public bool Running = true;
        public Renderer StatusRenderer; public Views.Spin[] Spins; public Nod[] Nods; public Transform Rocket;
    }

    /// <summary>Launch: the rocket climbs from the pad with a flame and trail; acceleration is independent of local gravity, but liftoff is visibly easier in low g.</summary>
    public class RocketLaunchFx : MonoBehaviour
    {
        float t, accel;
        Vector3 start;
        public static RocketLaunchFx Spawn(Transform parent, Vector3 pos, float gravity)
        {
            var go = new GameObject("Launch");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var white = StructureFactory.Mat("white", new Color(0.92f, 0.93f, 0.95f), 0.45f);
            StructureFactory.P(PrimitiveType.Cylinder, go.transform, new Vector3(0, 13, 0), new Vector3(2.6f, 13, 2.6f), white);
            StructureFactory.P(PrimitiveType.Capsule, go.transform, new Vector3(0, 26.5f, 0), new Vector3(2.4f, 2.4f, 2.4f), white);
            var flame = VisualFactory.Billboard("Flame", go.transform, VisualFactory.GlowTexture, new Color(1f, 0.7f, 0.3f, 1), 12);
            flame.transform.localPosition = new Vector3(0, -2, 0);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 6; trail.startWidth = 4; trail.endWidth = 14; trail.sharedMaterial = VisualFactory.Unlit(new Color(0.85f, 0.85f, 0.9f, 0.35f));
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(go.transform, false); light.type = LightType.Point; light.range = 120; light.intensity = 4; light.color = new Color(1f, 0.6f, 0.3f);
            var fx = go.AddComponent<RocketLaunchFx>();
            fx.start = pos;
            fx.accel = Mathf.Clamp(30f - gravity, 6, 30); // thrust-to-weight: net acceleration grows in low gravity
            return fx;
        }
        void Update()
        {
            t += Time.deltaTime;
            transform.localPosition = start + new Vector3(0.6f * t * t, 0.5f * accel * t * t, 0);
            if (t > 14) Destroy(gameObject);
        }
    }
}
