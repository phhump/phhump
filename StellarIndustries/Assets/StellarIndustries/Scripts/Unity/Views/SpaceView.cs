using System.Collections.Generic;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.Simulation;
using StellarIndustries.Core.State;
using UnityEngine;

namespace StellarIndustries.Unity.Views
{
    /// <summary>Marks a clickable celestial body on the map.</summary>
    public class BodyTag : MonoBehaviour { public string LocationId; }

    /// <summary>
    /// 3D presentation. Reads simulation state and reacts to events, never mutates gameplay.
    /// Two modes: Location (selected body with its installations and launches) and Map
    /// (solar system with orbits, bodies and missions in flight). Visual detail is capped and
    /// pooled so thousands of buildings do not create thousands of GameObjects.
    /// </summary>
    public class SpaceView : MonoBehaviour
    {
        public enum ViewMode { Location, Map }

        public ViewMode Mode = ViewMode.Location;
        /// <summary>Left edge of the 3D viewport (0..1); the UI panel covers the area to the left.</summary>
        public float ViewportLeft;
        public const int MaxBuildingVisuals = 160;

        GameController gc;
        GameSimulation sim;
        Camera cam;
        Transform root, locationRoot, mapRoot;
        GameObject planet;
        string shownLocation;
        bool buildingsDirty = true, mapDirty = true;
        float statusTimer;

        float yaw = 20, pitch = 15, distance = 6, targetDistance = 6;
        Vector3 camTarget;

        class BuildingVisual { public GameObject Go; public Renderer[] Renderers; public string DefId; public int Level; public Material Normal; }
        readonly Dictionary<int, BuildingVisual> visuals = new Dictionary<int, BuildingVisual>();
        readonly Dictionary<string, Material> defMaterials = new Dictionary<string, Material>();
        Material blockedMat, pausedMat;

        class RocketFx { public GameObject Go; public TrailRenderer Trail; public Vector3 Start, Dir, Side; public float T; }
        readonly List<RocketFx> activeRockets = new List<RocketFx>();
        readonly Stack<RocketFx> rocketPool = new Stack<RocketFx>();

        class MapBody { public LocationDef Def; public Transform T, Pivot; public Renderer R; public float Phase; public Material Mat; }
        readonly Dictionary<string, MapBody> mapBodies = new Dictionary<string, MapBody>();
        readonly List<GameObject> missionDots = new List<GameObject>();
        Material lockedBodyMat, dotMat;
        readonly Dictionary<string, Texture2D> planetTextures = new Dictionary<string, Texture2D>();

        public string FocusBody;

        public void Init(GameController controller)
        {
            gc = controller;
            root = new GameObject("World").transform;
            locationRoot = new GameObject("LocationView").transform; locationRoot.SetParent(root, false);
            mapRoot = new GameObject("MapView").transform; mapRoot.SetParent(root, false);
            SetupCamera();
            SetupLighting();
            VisualFactory.Starfield(root, 2500, 900);
            blockedMat = VisualFactory.Lit(new Color(1f, 0.55f, 0.1f));
            pausedMat = VisualFactory.Lit(new Color(0.25f, 0.25f, 0.28f));
            lockedBodyMat = VisualFactory.Lit(new Color(0.3f, 0.32f, 0.36f));
            dotMat = VisualFactory.Unlit(new Color(0.3f, 0.95f, 1f));
            gc.SimulationStarted += OnSimulationStarted;
            gc.SimulationStopped += OnSimulationStopped;
            ShowLocation(gc.Db.Start.StartLocation);
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.02f, 0.045f);
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 3000;
            cam.fieldOfView = 45;
        }

        void SetupLighting()
        {
#if UNITY_2023_1_OR_NEWER
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = Object.FindObjectsOfType<Light>();
#endif
            Light sun = null;
            foreach (var l in lights) if (l.type == LightType.Directional) sun = l;
            if (sun == null)
            {
                sun = new GameObject("Sun Light").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(25, -55, 0);
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.18f);
            RenderSettings.skybox = null;
        }

        void OnSimulationStarted(GameSimulation s)
        {
            sim = s;
            sim.Events.BuildingsChanged += OnBuildingsChanged;
            sim.Events.RocketLaunched += OnRocketLaunched;
            sim.Events.LocationUnlocked += OnLocationUnlocked;
            shownLocation = null;
            mapDirty = true;
            Mode = ViewMode.Location;
        }

        void OnSimulationStopped()
        {
            if (sim != null)
            {
                sim.Events.BuildingsChanged -= OnBuildingsChanged;
                sim.Events.RocketLaunched -= OnRocketLaunched;
                sim.Events.LocationUnlocked -= OnLocationUnlocked;
            }
            sim = null;
            ClearBuildings();
            Mode = ViewMode.Location;
            ShowLocation(gc.Db.Start.StartLocation);
        }

        void OnBuildingsChanged(int uid) => buildingsDirty = true;
        void OnLocationUnlocked(string id) => mapDirty = true;

        // ------------------------------------------------------------------ location view
        Texture2D TextureFor(LocationDef def)
        {
            if (!planetTextures.TryGetValue(def.Id, out var t)) planetTextures[def.Id] = t = VisualFactory.PlanetTexture(def);
            return t;
        }

        void ShowLocation(string id)
        {
            var def = gc.Db.Location(id);
            if (def == null) return;
            shownLocation = id;
            ClearBuildings();
            if (planet != null) Destroy(planet);
            planet = VisualFactory.Primitive(PrimitiveType.Sphere, "Planet " + def.Name, locationRoot);
            planet.transform.localScale = Vector3.one * 3f;
            planet.GetComponent<Renderer>().sharedMaterial = VisualFactory.Lit(Color.white, 0.1f, TextureFor(def));
            buildingsDirty = true;
            targetDistance = 6.5f;
        }

        void ClearBuildings()
        {
            foreach (var v in visuals.Values) if (v.Go != null) Destroy(v.Go);
            visuals.Clear();
        }

        Material MaterialFor(BuildingDef def)
        {
            if (!defMaterials.TryGetValue(def.Id, out var m)) defMaterials[def.Id] = m = VisualFactory.Lit(VisualFactory.Hex(def.ColorHex, Color.gray), 0.3f);
            return m;
        }

        static Vector3 StableNormal(int uid)
        {
            // Golden-ratio spiral by uid: stable positions that never shuffle when buildings are added.
            float f = (uid * 0.6180339f) % 1f;
            float y = Mathf.Lerp(-0.8f, 0.8f, f);
            float theta = uid * 2.399963f;
            float r = Mathf.Sqrt(1 - y * y);
            return new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
        }

        void SyncBuildings()
        {
            buildingsDirty = false;
            if (sim == null || planet == null) return;
            var list = sim.Production.BuildingsAt(shownLocation);
            var keep = new HashSet<int>();
            int shown = 0;
            foreach (var b in list)
            {
                if (shown++ >= MaxBuildingVisuals) break; // aggregated beyond this (see UI counts)
                keep.Add(b.Uid);
                var def = sim.Db.Building(b.DefId);
                if (def == null) continue;
                if (visuals.TryGetValue(b.Uid, out var v) && v.Level == b.Level) continue;
                if (v != null && v.Go != null) Destroy(v.Go);
                var mat = MaterialFor(def);
                var go = VisualFactory.BuildingShape(def.Shape, mat, planet.transform);
                var n = StableNormal(b.Uid);
                go.transform.localPosition = n * 0.5f;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, n);
                go.transform.localScale = Vector3.one * (0.075f * (1 + 0.12f * (b.Level - 1)));
                visuals[b.Uid] = new BuildingVisual { Go = go, Renderers = go.GetComponentsInChildren<Renderer>(), DefId = def.Id, Level = b.Level, Normal = mat };
            }
            var remove = new List<int>();
            foreach (var kv in visuals) if (!keep.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (var uid in remove) { if (visuals[uid].Go != null) Destroy(visuals[uid].Go); visuals.Remove(uid); }
            UpdateBuildingStatus();
        }

        void UpdateBuildingStatus()
        {
            if (sim == null) return;
            bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
            foreach (var b in sim.Production.BuildingsAt(shownLocation))
            {
                if (!visuals.TryGetValue(b.Uid, out var v)) continue;
                Material m = v.Normal;
                if (b.Status == BuildingStatus.Paused || b.Status == BuildingStatus.NoPower || b.Status == BuildingStatus.NoFuel) m = pausedMat;
                else if ((b.Status == BuildingStatus.MissingInput || b.Status == BuildingStatus.OutputFull || b.Status == BuildingStatus.NoDeposit) && blink) m = blockedMat;
                foreach (var r in v.Renderers) if (r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }

        // ------------------------------------------------------------------ rockets
        void OnRocketLaunched(string missionId, string rocketId, string origin)
        {
            if (origin != shownLocation || planet == null || Mode != ViewMode.Location) return;
            Vector3 start;
            BuildingVisual pad = null;
            foreach (var v in visuals.Values) if (v.DefId == "rocket_assembly") { pad = v; break; }
            if (pad != null) start = pad.Go.transform.position;
            else start = planet.transform.TransformPoint(StableNormal(Random.Range(1, 999)) * 0.5f);
            var fx = rocketPool.Count > 0 ? rocketPool.Pop() : CreateRocket();
            fx.Go.SetActive(true);
            fx.Start = start;
            fx.Dir = (start - planet.transform.position).normalized;
            fx.Side = Vector3.Cross(fx.Dir, Vector3.up).normalized;
            if (fx.Side.sqrMagnitude < 0.01f) fx.Side = Vector3.right;
            fx.T = 0;
            fx.Go.transform.position = start;
            fx.Trail.Clear();
            activeRockets.Add(fx);
        }

        RocketFx CreateRocket()
        {
            var go = new GameObject("Rocket");
            go.transform.SetParent(locationRoot, false);
            var body = VisualFactory.Primitive(PrimitiveType.Capsule, "Body", go.transform);
            body.transform.localScale = new Vector3(0.03f, 0.06f, 0.03f);
            body.GetComponent<Renderer>().sharedMaterial = VisualFactory.Lit(Color.white, 0.6f);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 1.6f;
            trail.startWidth = 0.05f;
            trail.endWidth = 0f;
            trail.sharedMaterial = VisualFactory.Unlit(Color.white);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 0.4f), new GradientColorKey(new Color(0.6f, 0.6f, 0.7f), 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = g;
            return new RocketFx { Go = go, Trail = trail };
        }

        void UpdateRockets(float dt)
        {
            for (int i = activeRockets.Count - 1; i >= 0; i--)
            {
                var fx = activeRockets[i];
                fx.T += dt;
                float t = fx.T;
                // Gravity turn: climb along the surface normal, then pitch over sideways.
                Vector3 pos = fx.Start + fx.Dir * (0.25f * t * t) + fx.Side * (0.05f * t * t * t);
                var vel = fx.Dir * (0.5f * t) + fx.Side * (0.15f * t * t);
                fx.Go.transform.position = pos;
                if (vel.sqrMagnitude > 1e-6f) fx.Go.transform.rotation = Quaternion.FromToRotation(Vector3.up, vel.normalized);
                if (fx.T > 7f)
                {
                    fx.Go.SetActive(false);
                    activeRockets.RemoveAt(i);
                    rocketPool.Push(fx);
                }
            }
        }

        // ------------------------------------------------------------------ map view
        void BuildMap()
        {
            mapDirty = false;
            foreach (Transform c in mapRoot) Destroy(c.gameObject);
            mapBodies.Clear();
            var sunGo = VisualFactory.Primitive(PrimitiveType.Sphere, "Sun", mapRoot);
            sunGo.transform.localScale = Vector3.one * 7f;
            sunGo.GetComponent<Renderer>().sharedMaterial = VisualFactory.Unlit(new Color(1f, 0.85f, 0.45f));
            foreach (var def in gc.Db.Locations)
            {
                var pivot = new GameObject("Pivot " + def.Name).transform;
                pivot.SetParent(mapRoot, false);
                var orbitColor = sim != null && sim.State.IsUnlocked(def.Id) ? new Color(0.17f, 0.85f, 1f, 0.35f) : new Color(0.5f, 0.55f, 0.6f, 0.18f);
                VisualFactory.Circle(pivot, (float)def.OrbitRadius, orbitColor, 128, string.IsNullOrEmpty(def.ParentId) ? 0.12f : 0.04f);
                var body = VisualFactory.Primitive(PrimitiveType.Sphere, def.Name, mapRoot, true);
                body.transform.localScale = Vector3.one * (float)(def.VisualRadius * 2);
                body.AddComponent<BodyTag>().LocationId = def.Id;
                var mat = VisualFactory.Lit(Color.white, 0.1f, TextureFor(def));
                var r = body.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                mapBodies[def.Id] = new MapBody { Def = def, T = body.transform, Pivot = pivot, R = r, Mat = mat, Phase = (def.Id.GetHashCode() & 1023) / 1023f * Mathf.PI * 2 };
            }
        }

        Vector3 BodyPosition(string id, float time, int depth = 0)
        {
            if (!mapBodies.TryGetValue(id, out var b) || depth > 8) return Vector3.zero;
            var parent = string.IsNullOrEmpty(b.Def.ParentId) ? Vector3.zero : BodyPosition(b.Def.ParentId, time, depth + 1);
            float a = b.Phase + time / (float)System.Math.Max(1, b.Def.OrbitPeriod) * Mathf.PI * 2;
            return parent + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (float)b.Def.OrbitRadius;
        }

        void UpdateMap()
        {
            if (mapDirty) BuildMap();
            float time = Time.time;
            foreach (var kv in mapBodies)
            {
                var b = kv.Value;
                b.T.localPosition = BodyPosition(kv.Key, time);
                b.T.Rotate(0, 20 * Time.deltaTime, 0, Space.Self);
                b.Pivot.localPosition = string.IsNullOrEmpty(b.Def.ParentId) ? Vector3.zero : BodyPosition(b.Def.ParentId, time);
                bool unlocked = sim != null && sim.State.IsUnlocked(kv.Key);
                var m = unlocked ? b.Mat : lockedBodyMat;
                if (b.R.sharedMaterial != m) b.R.sharedMaterial = m;
            }
            // Missions in flight
            int count = sim?.State.ActiveMissions.Count ?? 0;
            while (missionDots.Count < count)
            {
                var d = VisualFactory.Primitive(PrimitiveType.Sphere, "Mission", mapRoot);
                d.transform.localScale = Vector3.one * 0.45f;
                d.GetComponent<Renderer>().sharedMaterial = dotMat;
                missionDots.Add(d);
            }
            for (int i = 0; i < missionDots.Count; i++)
            {
                bool on = i < count;
                if (missionDots[i].activeSelf != on) missionDots[i].SetActive(on);
                if (!on) continue;
                var mi = sim.State.ActiveMissions[i];
                var def = sim.Db.Mission(mi.MissionId);
                if (def == null) continue;
                float p = Mathf.Clamp01((float)(mi.Elapsed / System.Math.Max(0.001, mi.Duration)));
                Vector3 a = BodyPosition(def.Origin, time), c = BodyPosition(def.Destination, time);
                Vector3 pos;
                if (def.Origin == def.Destination)
                {
                    float ang = p * Mathf.PI * 4;
                    pos = a + new Vector3(Mathf.Cos(ang), 0.2f, Mathf.Sin(ang)) * 2.2f;
                }
                else pos = Vector3.Lerp(a, c, p) + Vector3.up * Mathf.Sin(p * Mathf.PI) * Vector3.Distance(a, c) * 0.15f;
                missionDots[i].transform.localPosition = pos;
            }
        }

        public string Pick(Vector2 screenPos)
        {
            if (Mode != ViewMode.Map || cam == null) return null;
            if (!cam.pixelRect.Contains(screenPos)) return null;
            if (Physics.Raycast(cam.ScreenPointToRay(screenPos), out var hit, 5000f))
            {
                var tag = hit.collider.GetComponent<BodyTag>();
                if (tag != null) return tag.LocationId;
            }
            return null;
        }

        public struct Label { public string Id, Name; public Vector2 Screen; public bool Unlocked; }

        public void CollectLabels(List<Label> into)
        {
            into.Clear();
            if (Mode != ViewMode.Map || cam == null) return;
            foreach (var kv in mapBodies)
            {
                var sp = cam.WorldToScreenPoint(kv.Value.T.position + Vector3.up * kv.Value.T.localScale.y * 0.7f);
                if (sp.z <= 0) continue;
                into.Add(new Label { Id = kv.Key, Name = kv.Value.Def.Name, Screen = sp, Unlocked = sim != null && sim.State.IsUnlocked(kv.Key) });
            }
        }

        // ------------------------------------------------------------------ camera & loop
        public void Orbit(Vector2 delta) { yaw += delta.x * 0.3f; pitch = Mathf.Clamp(pitch + delta.y * 0.3f, -80, 85); }

        public void Zoom(float amount)
        {
            float min = Mode == ViewMode.Map ? 4 : 2.2f, max = Mode == ViewMode.Map ? 220 : 20;
            targetDistance = Mathf.Clamp(targetDistance * (1 + amount * 0.06f), min, max);
        }

        public void SetMode(ViewMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            targetDistance = mode == ViewMode.Map ? 95 : 6.5f;
            pitch = mode == ViewMode.Map ? 50 : 15;
            FocusBody = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (sim != null && sim.State.CurrentLocation != shownLocation) ShowLocation(sim.State.CurrentLocation);
            if (planet != null) planet.transform.Rotate(0, 5f * dt, 0, Space.World);
            if (buildingsDirty) SyncBuildings();
            statusTimer += dt;
            if (statusTimer > 0.25f) { statusTimer = 0; UpdateBuildingStatus(); }
            UpdateRockets(dt);

            bool map = Mode == ViewMode.Map && sim != null;
            if (locationRoot.gameObject.activeSelf == map) locationRoot.gameObject.SetActive(!map);
            if (mapRoot.gameObject.activeSelf != map) mapRoot.gameObject.SetActive(map);
            if (map) UpdateMap();

            camTarget = map ? (FocusBody != null && mapBodies.ContainsKey(FocusBody) ? mapBodies[FocusBody].T.position : Vector3.zero) : Vector3.zero;
            distance = Mathf.Lerp(distance, targetDistance, 1 - Mathf.Exp(-6 * dt));
            var rot = Quaternion.Euler(pitch, yaw, 0);
            cam.transform.position = camTarget + rot * new Vector3(0, 0, -distance);
            cam.transform.rotation = rot;
            float left = Mathf.Clamp(ViewportLeft, 0, 0.8f);
            var rect = new Rect(left, 0, 1 - left, 1);
            if (cam.rect != rect) cam.rect = rect;
            if (sim == null) yaw += 3 * dt; // idle spin on the main menu
        }
    }
}
