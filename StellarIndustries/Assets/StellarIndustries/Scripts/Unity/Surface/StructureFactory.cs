using System.Collections.Generic;
using StellarIndustries.Core.Data;
using StellarIndustries.Unity.Views;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// Builds recognisable industrial models from primitives (metres). One place to swap in real art later:
    /// return a prefab instance here instead.
    /// </summary>
    public static class StructureFactory
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static Material Mat(string key, Color c, float smooth = 0.3f, bool emissive = false)
        {
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            m = emissive ? VisualFactory.Unlit(c) : VisualFactory.Lit(c, smooth);
            mats[key] = m;
            return m;
        }

        static Material Metal => Mat("metal", new Color(0.72f, 0.74f, 0.78f), 0.55f);
        static Material Dark => Mat("dark", new Color(0.22f, 0.24f, 0.28f), 0.4f);
        static Material Concrete => Mat("concrete", new Color(0.55f, 0.55f, 0.53f), 0.1f);
        static Material Panel => Mat("panel", new Color(0.08f, 0.16f, 0.35f), 0.9f);
        static Material White => Mat("white", new Color(0.92f, 0.93f, 0.95f), 0.45f);
        static Material Glow => Mat("glow", new Color(1f, 0.55f, 0.15f), 0, true);
        static Material Glass => Mat("glass", new Color(0.35f, 0.8f, 1f), 0, true);

        public static GameObject P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var go = VisualFactory.Primitive(t, t.ToString(), parent);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Creates the model for a building. Origin = ground centre; +Z = front.</summary>
        public static GameObject Create(BuildingDef def, Color oreColor)
        {
            var root = new GameObject(def.Name);
            var t = root.transform;
            float f = (float)def.FootprintM;
            var accent = Mat("accent_" + def.Id, VisualFactory.Hex(def.ColorHex, Color.gray), 0.35f);
            // foundation slab (extends below ground to hide slopes)
            P(PrimitiveType.Cylinder, t, new Vector3(0, -1.2f, 0), new Vector3(f, 1.5f, f), Concrete);
            var foundation = t.GetChild(0).GetComponent<Renderer>();
            switch (def.Id)
            {
                case "wind_turbine":
                    P(PrimitiveType.Cylinder, t, new Vector3(0, 12, 0), new Vector3(1.1f, 12, 1.1f), White);
                    P(PrimitiveType.Cube, t, new Vector3(0, 24.2f, -0.6f), new Vector3(1.4f, 1.4f, 3.4f), White);
                    var hub = new GameObject("Rotor").transform; hub.SetParent(t, false); hub.localPosition = new Vector3(0, 24.2f, 1.3f); hub.localRotation = Quaternion.Euler(90, 0, 0);
                    P(PrimitiveType.Sphere, hub, Vector3.zero, Vector3.one * 1.2f, White);
                    for (int i = 0; i < 3; i++) P(PrimitiveType.Cube, hub, Quaternion.Euler(0, i * 120, 0) * new Vector3(0, 0, 5.5f), new Vector3(0.5f, 0.15f, 10.5f), White, new Vector3(0, i * 120, 0));
                    hub.gameObject.AddComponent<Views.Spin>().DegreesPerSecond = 70;
                    break;
                case "solar_array":
                    for (int r = -1; r <= 1; r++)
                        for (int c = -1; c <= 1; c++)
                        {
                            P(PrimitiveType.Cylinder, t, new Vector3(c * 3.4f, 0.6f, r * 3.4f), new Vector3(0.15f, 0.6f, 0.15f), Dark);
                            P(PrimitiveType.Cube, t, new Vector3(c * 3.4f, 1.3f, r * 3.4f), new Vector3(3.1f, 0.08f, 2.4f), Panel, new Vector3(-28, 0, 0));
                        }
                    break;
                case "surface_mine":
                    for (int i = 0; i < 4; i++) P(PrimitiveType.Cube, t, new Vector3((i % 2 - 0.5f) * 3, 5, (i / 2 - 0.5f) * 3), new Vector3(0.35f, 10, 0.35f), Dark);
                    P(PrimitiveType.Cube, t, new Vector3(0, 10.2f, 0), new Vector3(3.8f, 0.8f, 3.8f), accent);
                    P(PrimitiveType.Cylinder, t, new Vector3(0, 11.2f, 0), new Vector3(2.4f, 0.15f, 2.4f), Metal, new Vector3(90, 0, 0));
                    P(PrimitiveType.Cube, t, new Vector3(3.5f, 2.5f, 0), new Vector3(6, 0.5f, 1.2f), Metal, new Vector3(0, 0, 30));
                    P(PrimitiveType.Sphere, t, new Vector3(6.2f, 0.2f, 0), new Vector3(4, 2.2f, 4), Mat("ore_" + ColorUtility.ToHtmlStringRGB(oreColor), oreColor, 0.1f));
                    break;
                case "smelter":
                    P(PrimitiveType.Cube, t, new Vector3(0, 2.8f, 0), new Vector3(8, 5.6f, 6.5f), accent);
                    P(PrimitiveType.Cylinder, t, new Vector3(2.5f, 7, -1.5f), new Vector3(1.3f, 7, 1.3f), Dark);
                    P(PrimitiveType.Cylinder, t, new Vector3(2.5f, 14.05f, -1.5f), new Vector3(1.35f, 0.1f, 1.35f), Glow);
                    P(PrimitiveType.Cube, t, new Vector3(-2.5f, 1.5f, 3.6f), new Vector3(2.6f, 3, 1), Glow);
                    break;
                case "refinery":
                    for (int i = 0; i < 3; i++) P(PrimitiveType.Cylinder, t, new Vector3(-4 + i * 4, 3, -3), new Vector3(3, 3, 3), White);
                    P(PrimitiveType.Cylinder, t, new Vector3(4, 7, 3), new Vector3(1.6f, 7, 1.6f), accent);
                    P(PrimitiveType.Cube, t, new Vector3(-1, 1.5f, 3), new Vector3(7, 3, 4), Metal);
                    P(PrimitiveType.Cylinder, t, new Vector3(0, 6.3f, -3), new Vector3(0.4f, 6, 0.4f), Dark, new Vector3(0, 0, 90));
                    break;
                case "chemical_plant":
                    P(PrimitiveType.Sphere, t, new Vector3(-3.5f, 4.5f, -2), Vector3.one * 5, White);
                    P(PrimitiveType.Sphere, t, new Vector3(3.5f, 4.5f, -2), Vector3.one * 5, White);
                    for (int i = 0; i < 4; i++) P(PrimitiveType.Cylinder, t, new Vector3(i < 2 ? -3.5f : 3.5f, 1.2f, -2 + (i % 2 - 0.5f) * 2.4f), new Vector3(0.3f, 1.2f, 0.3f), Dark);
                    P(PrimitiveType.Cube, t, new Vector3(0, 2, 4), new Vector3(12, 4, 4.5f), accent);
                    break;
                case "component_factory":
                    P(PrimitiveType.Cube, t, new Vector3(0, 3, 0), new Vector3(12, 6, 9), accent);
                    for (int i = 0; i < 4; i++) P(PrimitiveType.Cube, t, new Vector3(-4.5f + i * 3, 6.8f, 0), new Vector3(0.2f, 2.2f, 9), Panel, new Vector3(0, 0, 35));
                    P(PrimitiveType.Cube, t, new Vector3(0, 2, 4.55f), new Vector3(4, 4, 0.2f), Dark);
                    break;
                case "rocket_assembly":
                    P(PrimitiveType.Cylinder, t, new Vector3(0, 0.15f, 0), new Vector3(f * 0.85f, 0.3f, f * 0.85f), Concrete);
                    P(PrimitiveType.Cube, t, new Vector3(6, 16, 0), new Vector3(2.2f, 32, 2.2f), accent);
                    for (int i = 1; i < 6; i++) P(PrimitiveType.Cube, t, new Vector3(3.8f, i * 5.5f, 0), new Vector3(3.2f, 0.3f, 0.6f), Metal);
                    P(PrimitiveType.Cube, t, new Vector3(-8, 5, -6), new Vector3(9, 10, 8), Metal);
                    var rocket = new GameObject("StandingRocket").transform; rocket.SetParent(t, false);
                    P(PrimitiveType.Cylinder, rocket, new Vector3(0, 13, 0), new Vector3(2.6f, 13, 2.6f), White);
                    P(PrimitiveType.Capsule, rocket, new Vector3(0, 26.5f, 0), new Vector3(2.4f, 2.4f, 2.4f), White);
                    P(PrimitiveType.Cylinder, rocket, new Vector3(0, 9, 0), new Vector3(2.65f, 0.6f, 2.65f), Dark);
                    rocket.gameObject.SetActive(false);
                    break;
                case "research_lab":
                    P(PrimitiveType.Sphere, t, new Vector3(0, 0, 0), new Vector3(10, 8, 10), White);
                    P(PrimitiveType.Cylinder, t, new Vector3(3, 4.5f, 2), new Vector3(0.2f, 2, 0.2f), Dark);
                    P(PrimitiveType.Sphere, t, new Vector3(3, 6.6f, 2), new Vector3(3, 0.6f, 3), Metal, new Vector3(35, 0, 0));
                    P(PrimitiveType.Cube, t, new Vector3(0, 1.2f, 5), new Vector3(2.4f, 2.4f, 1), Glass);
                    break;
                case "warehouse":
                    P(PrimitiveType.Cube, t, new Vector3(0, 3.5f, 0), new Vector3(12, 7, 10), accent);
                    for (int i = 0; i < 5; i++) P(PrimitiveType.Cube, t, new Vector3(-4.8f + i * 2.4f, 3.5f, 5.05f), new Vector3(0.25f, 7, 0.1f), Dark);
                    break;
                case "water_pump":
                    P(PrimitiveType.Cube, t, new Vector3(0, 1.5f, 0), new Vector3(4, 3, 4), accent);
                    P(PrimitiveType.Cylinder, t, new Vector3(0, 2, 3), new Vector3(0.5f, 2, 0.5f), Metal, new Vector3(90, 0, 0));
                    break;
                case "oil_derrick":
                    P(PrimitiveType.Cube, t, new Vector3(0, 1, 0), new Vector3(1, 2, 1), Dark);
                    var beam = P(PrimitiveType.Cube, t, new Vector3(0, 3.2f, 0), new Vector3(7, 0.6f, 0.6f), accent);
                    beam.AddComponent<Nod>();
                    P(PrimitiveType.Cube, t, new Vector3(3.2f, 1.6f, 0), new Vector3(0.3f, 3.2f, 0.3f), Metal);
                    break;
                case "coal_plant":
                    P(PrimitiveType.Cube, t, new Vector3(-3, 3.5f, 0), new Vector3(8, 7, 9), accent);
                    P(PrimitiveType.Cylinder, t, new Vector3(4.5f, 6, -3), new Vector3(5.5f, 6, 5.5f), Concrete);
                    P(PrimitiveType.Cylinder, t, new Vector3(-5, 9, 3), new Vector3(1, 9, 1), Dark);
                    break;
                case "ice_extractor":
                case "atmo_processor":
                    P(PrimitiveType.Cylinder, t, new Vector3(0, 5, 0), new Vector3(2.4f, 5, 2.4f), accent);
                    for (int i = 0; i < 4; i++) P(PrimitiveType.Cube, t, Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 6, 1.6f), new Vector3(0.2f, 6, 1.4f), Metal, new Vector3(0, i * 90, 0));
                    P(PrimitiveType.Sphere, t, new Vector3(0, 1.4f, 3.6f), new Vector3(3.5f, 2.6f, 3.5f), White);
                    break;
                case "he3_harvester":
                    P(PrimitiveType.Cube, t, new Vector3(0, 2.2f, 0), new Vector3(9, 3.4f, 6), accent);
                    for (int i = 0; i < 6; i++) P(PrimitiveType.Cylinder, t, new Vector3(-3.2f + (i % 3) * 3.2f, 0.9f, i < 3 ? -3.3f : 3.3f), new Vector3(1.8f, 0.4f, 1.8f), Dark, new Vector3(90, 0, 0));
                    P(PrimitiveType.Cube, t, new Vector3(6.5f, 2.2f, 0), new Vector3(5, 0.6f, 1.6f), Metal, new Vector3(0, 0, -25));
                    P(PrimitiveType.Sphere, t, new Vector3(-2, 4.5f, 0), new Vector3(3, 2, 3), Glass);
                    break;
                case "fusion_reactor":
                    P(PrimitiveType.Sphere, t, new Vector3(0, 0, 0), new Vector3(18, 12, 18), White);
                    P(PrimitiveType.Cylinder, t, new Vector3(0, 6.2f, 0), new Vector3(6, 0.4f, 6), Glass);
                    break;
                case "laser_array":
                    for (int r = -2; r <= 2; r++)
                        for (int c = -2; c <= 2; c++)
                        {
                            P(PrimitiveType.Cylinder, t, new Vector3(c * 5, 1, r * 5), new Vector3(0.4f, 1, 0.4f), Dark);
                            P(PrimitiveType.Sphere, t, new Vector3(c * 5, 2.2f, r * 5), new Vector3(3.6f, 0.7f, 3.6f), Metal);
                            P(PrimitiveType.Sphere, t, new Vector3(c * 5, 2.6f, r * 5), Vector3.one * 0.5f, Mat("laser", new Color(1f, 0.4f, 0.8f), 0, true));
                        }
                    break;
                default:
                    P(PrimitiveType.Cube, t, new Vector3(0, 2.5f, 0), new Vector3(f * 0.7f, 5, f * 0.6f), accent);
                    break;
            }
            AddLod(root, foundation, accent, f);
            VisualFactory.Shadows(root);
            return root;
        }

        /// <summary>
        /// Two detail levels: the full model up close, one simple shape (same silhouette size and colour) far away,
        /// culled when tiny. Cuts draw calls for big bases dramatically; thresholds follow the quality preset.
        /// </summary>
        static void AddLod(GameObject root, Renderer foundation, Material accent, float f)
        {
            var detailed = root.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(root.transform.position, Vector3.zero);
            foreach (var r in detailed) if (r != foundation) b.Encapsulate(r.bounds);
            float h = Mathf.Max(1, b.max.y - root.transform.position.y);
            bool tall = h > 2.5f * Mathf.Max(b.size.x, b.size.z);
            var simple = tall
                ? P(PrimitiveType.Cylinder, root.transform, new Vector3(0, h / 2, 0), new Vector3(Mathf.Max(1, b.size.x * 0.5f), h / 2, Mathf.Max(1, b.size.z * 0.5f)), accent)
                : P(PrimitiveType.Cube, root.transform, new Vector3(0, h / 2, 0), new Vector3(Mathf.Max(1, b.size.x * 0.9f), h, Mathf.Max(1, b.size.z * 0.9f)), accent);
            simple.name = "LOD1";
            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[]
            {
                new LOD(Performance.GraphicsQuality.LodDetailThreshold, detailed),
                new LOD(Performance.GraphicsQuality.LodCullThreshold, new[] { simple.GetComponent<Renderer>(), foundation }),
            });
            lod.RecalculateBounds();
        }

        /// <summary>Switches every renderer of a model to one (transparent) preview material.</summary>
        public static void MakeGhost(GameObject go, Material ghost)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = ghost; r.shadowCastingMode = ShadowCastingMode.Off; }
            foreach (var s in go.GetComponentsInChildren<Views.Spin>(true)) s.enabled = false;
        }
    }

    /// <summary>Pump-jack style nodding motion.</summary>
    public class Nod : MonoBehaviour
    {
        public bool Active = true;
        void Update() { if (Active) transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.6f) * 14f); }
    }
}
