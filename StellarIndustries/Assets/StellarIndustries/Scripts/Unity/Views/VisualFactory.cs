using StellarIndustries.Core.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarIndustries.Unity.Views
{
    /// <summary>
    /// Placeholder art generated at runtime (primitives, procedural textures). Works with the
    /// built-in render pipeline and URP. Replace with real models/materials later: all visuals
    /// are created through this class.
    /// </summary>
    public static class VisualFactory
    {
        static Shader lit, unlit;

        public static Shader LitShader
        {
            get
            {
                if (lit != null) return lit;
                if (GraphicsSettings.currentRenderPipeline != null) lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard");
                if (lit == null) lit = Shader.Find("Legacy Shaders/Diffuse");
                if (lit == null) lit = UnlitShader;
                return lit;
            }
        }

        /// <summary>Sprites/Default exists in every pipeline, supports vertex colors and is always included in builds.</summary>
        public static Shader UnlitShader => unlit != null ? unlit : (unlit = Shader.Find("Sprites/Default"));

        public static Material Lit(Color c, float smoothness = 0.15f, Texture tex = null)
        {
            var m = new Material(LitShader) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            return m;
        }

        public static Material Unlit(Color c) => new Material(UnlitShader) { color = c };

        // ------------------------------------------------------------------ HD assets
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> texCache = new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>Loads Resources/Textures/{name}; null if missing (callers fall back to procedural art).</summary>
        public static Texture2D LoadTexture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (texCache.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Textures/" + name);
            if (t != null) { t.anisoLevel = 8; t.wrapModeU = TextureWrapMode.Repeat; t.wrapModeV = TextureWrapMode.Clamp; }
            texCache[name] = t;
            return t;
        }

        /// <summary>Surface material from real textures (day map, optional city lights as emission), or procedural fallback.</summary>
        public static Material PlanetMaterial(LocationDef def)
        {
            var tex = LoadTexture(def.SurfaceTexture);
            if (tex == null) return Lit(Color.white, 0.1f, PlanetTexture(def, 1024, 512));
            var m = Lit(Hex(def.TextureTintHex, Color.white), 0.12f, tex);
            var night = LoadTexture(def.NightTexture);
            if (night != null) EnableEmission(m, night, new Color(0.85f, 0.7f, 0.45f) * 0.55f);
            return m;
        }

        public static void EnableEmission(Material m, Texture tex, Color color)
        {
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        /// <summary>Switches a Standard / URP Lit material to alpha blending (used for the cloud layer).</summary>
        public static void MakeTransparent(Material m)
        {
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2);          // Standard: Fade
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1);    // URP: Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Texture2D glowTex, haloTex, circleTex;

        /// <summary>Soft radial glow (stars, sun).</summary>
        public static Texture2D GlowTexture => glowTex != null ? glowTex : (glowTex = Radial(256, r => Mathf.Pow(Mathf.Clamp01(1 - r), 2.4f)));

        /// <summary>Atmospheric rim: transparent inside the planet disc (r &lt; 0.72), bright at the limb, fading outwards.</summary>
        public static Texture2D HaloTexture => haloTex != null ? haloTex : (haloTex = Radial(256, r =>
            r < 0.70f ? 0 : r < 0.74f ? Mathf.InverseLerp(0.70f, 0.74f, r) : Mathf.Pow(Mathf.Clamp01(1 - (r - 0.74f) / 0.26f), 2.2f)));

        public static Texture2D CircleTexture => circleTex != null ? circleTex : (circleTex = Radial(64, r => Mathf.Clamp01((1 - r) * 32)));

        static Texture2D Radial(int size, System.Func<float, float> alpha)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                    px[y * size + x] = new Color(1, 1, 1, alpha(Mathf.Sqrt(dx * dx + dy * dy)));
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Camera-facing quad with an alpha texture (glows, halos). Size is the full width in world units.</summary>
        public static GameObject Billboard(string name, Transform parent, Texture tex, Color color, float size)
        {
            var go = Primitive(PrimitiveType.Quad, name, parent);
            go.transform.localScale = Vector3.one * size;
            var m = new Material(UnlitShader) { color = color, mainTexture = tex };
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.AddComponent<FaceCamera>();
            return go;
        }

        /// <summary>Huge inward-facing sphere with the Milky Way panorama (Sprites/Default renders both faces).</summary>
        public static GameObject SkySphere(Transform parent, float radius)
        {
            var tex = LoadTexture("milky_way");
            if (tex == null) return null;
            var go = Primitive(PrimitiveType.Sphere, "Milky Way", parent);
            go.transform.localScale = new Vector3(-radius * 2, radius * 2, radius * 2); // mirrored: seen from inside
            var m = new Material(UnlitShader) { color = new Color(0.55f, 0.58f, 0.66f), mainTexture = tex, renderQueue = (int)RenderQueue.Background };
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.AddComponent<FollowCamera>();
            return go;
        }

        public static Color Hex(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>Simple shapes for building categories. Returns the root object.</summary>
        public static GameObject BuildingShape(string shape, Material mat, Transform parent)
        {
            var root = new GameObject("Building");
            root.transform.SetParent(parent, false);
            void Part(PrimitiveType t, Vector3 pos, Vector3 scale)
            {
                var p = Primitive(t, "part", root.transform);
                p.transform.localPosition = pos;
                p.transform.localScale = scale;
                p.GetComponent<Renderer>().sharedMaterial = mat;
            }
            switch (shape)
            {
                case "tower": Part(PrimitiveType.Cube, new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.3f, 0.3f)); Part(PrimitiveType.Cylinder, new Vector3(0, 0.55f, 0), new Vector3(0.08f, 0.3f, 0.08f)); break;
                case "cylinder": Part(PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.2f, 0.35f)); break;
                case "dome": Part(PrimitiveType.Sphere, new Vector3(0, 0, 0), new Vector3(0.5f, 0.45f, 0.5f)); break;
                case "panel": Part(PrimitiveType.Cube, new Vector3(0, 0.12f, 0), new Vector3(0.55f, 0.03f, 0.3f)); Part(PrimitiveType.Cube, new Vector3(0, 0.05f, 0), new Vector3(0.04f, 0.1f, 0.04f)); break;
                case "pad": Part(PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(0.6f, 0.02f, 0.6f)); Part(PrimitiveType.Cube, new Vector3(0.22f, 0.35f, 0), new Vector3(0.06f, 0.7f, 0.06f)); break;
                default: Part(PrimitiveType.Cube, new Vector3(0, 0.15f, 0), new Vector3(0.4f, 0.3f, 0.3f)); break;
            }
            return root;
        }

        /// <summary>Procedural equirectangular planet texture derived from the location data.</summary>
        public static Texture2D PlanetTexture(LocationDef def, int width = 256, int height = 128)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var baseColor = Hex(def.ColorHex, Color.gray);
            bool earthLike = def.Atmosphere != null && def.Atmosphere.Contains("Oxygen");
            bool cratered = def.Atmosphere != null && def.Atmosphere.StartsWith("None");
            bool gasGiant = def.BodyType == BodyType.GasGiant;
            float seed = (def.Id.GetHashCode() & 0xffff) * 0.013f;
            var px = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float lat = (y / (float)(height - 1)) * 2 - 1;
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width * Mathf.PI * 2;
                    // Sample noise on a cylinder so the texture wraps seamlessly.
                    float nx = Mathf.Cos(u) * 2 + seed, nz = Mathf.Sin(u) * 2 + seed;
                    float n = Fbm(nx, lat * 2.2f + nz);
                    Color c;
                    if (gasGiant)
                    {
                        float band = Mathf.Sin(lat * 18f + n * 3f) * 0.5f + 0.5f;
                        c = Color.Lerp(baseColor * 0.6f, Color.Lerp(baseColor, Color.white, 0.45f), band);
                    }
                    else if (earthLike)
                    {
                        if (Mathf.Abs(lat) > 0.86f + n * 0.08f) c = new Color(0.93f, 0.96f, 1f);
                        else if (n > 0.53f) c = Color.Lerp(new Color(0.20f, 0.45f, 0.18f), new Color(0.55f, 0.47f, 0.30f), (n - 0.53f) * 3.5f);
                        else c = Color.Lerp(new Color(0.03f, 0.12f, 0.35f), new Color(0.10f, 0.35f, 0.65f), n * 1.6f);
                    }
                    else
                    {
                        c = baseColor * (0.65f + n * 0.6f);
                        if (def.Id == "mars" && Mathf.Abs(lat) > 0.9f) c = new Color(0.95f, 0.92f, 0.9f);
                    }
                    px[y * width + x] = c;
                }
            }
            if (cratered)
            {
                var rnd = new System.Random(def.Id.GetHashCode());
                for (int i = 0; i < 70; i++)
                {
                    int cx = rnd.Next(width), cy = rnd.Next(8, height - 8), r = rnd.Next(2, 9);
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                            if (d > 1) continue;
                            int xx = (cx + dx + width) % width, yy = Mathf.Clamp(cy + dy, 0, height - 1);
                            float shade = d > 0.75f ? 1.12f : 0.82f;
                            px[yy * width + xx] *= shade;
                        }
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static float Fbm(float x, float y)
        {
            float sum = 0, amp = 0.5f, freq = 1;
            for (int i = 0; i < 4; i++) { sum += Mathf.PerlinNoise(x * freq, y * freq) * amp; amp *= 0.5f; freq *= 2.1f; }
            return sum / 0.9375f;
        }

        /// <summary>A sphere of points used as starfield (single draw call).</summary>
        public static GameObject Starfield(Transform parent, int count, float radius)
        {
            var go = new GameObject("Starfield");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Stars" };
            var verts = new Vector3[count];
            var colors = new Color[count];
            var idx = new int[count];
            var rnd = new System.Random(1337);
            for (int i = 0; i < count; i++)
            {
                var v = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1).normalized * radius;
                verts[i] = v;
                float b = 0.35f + (float)rnd.NextDouble() * 0.65f;
                float tint = (float)rnd.NextDouble();
                colors[i] = tint < 0.15f ? new Color(b, b * 0.85f, b * 0.7f) : tint > 0.85f ? new Color(b * 0.8f, b * 0.9f, b) : new Color(b, b, b);
                idx[i] = i;
            }
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.SetIndices(idx, MeshTopology.Points, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Unlit(Color.white);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static LineRenderer Circle(Transform parent, float radius, Color color, int segments = 96, float width = 0.06f)
        {
            var go = new GameObject("Orbit");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.widthMultiplier = width;
            lr.sharedMaterial = Unlit(color);
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }
            return lr;
        }
    }
}
