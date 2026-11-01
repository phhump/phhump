using System.Collections.Generic;
using UnityEngine;

namespace StellarIndustries.Unity.Views
{
    /// <summary>Generated meshes: a smooth UV sphere for planets (Unity's built-in sphere has visible facets up close).</summary>
    public static class MeshFactory
    {
        static readonly Dictionary<int, Mesh> spheres = new Dictionary<int, Mesh>();

        /// <summary>Unit-diameter UV sphere with equirectangular UVs (east to the right seen from outside, north up).</summary>
        public static Mesh Sphere(int segments)
        {
            segments = Mathf.Clamp(segments, 16, 256);
            if (spheres.TryGetValue(segments, out var m) && m != null) return m;
            int rings = segments / 2;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.PI * (r / (float)rings - 0.5f);
                for (int s = 0; s <= segments; s++)
                {
                    float lon = 2 * Mathf.PI * s / segments - Mathf.PI;
                    var p = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), -Mathf.Cos(lat) * Mathf.Cos(lon));
                    v.Add(p * 0.5f); n.Add(p); uv.Add(new Vector2(s / (float)segments, r / (float)rings));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + 1, c = a + segments + 1, d = c + 1;
                    t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d);
                }
            // Make sure triangles face outwards (Unity: front face normal = cross(b - a, c - a)).
            int tri = ((rings / 2) * segments + 1) * 6;
            var A = v[t[tri]]; var B = v[t[tri + 1]]; var C = v[t[tri + 2]];
            if (Vector3.Dot(Vector3.Cross(B - A, C - A), A) < 0)
                for (int k = 0; k < t.Count; k += 3) { int tmp = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = tmp; }
            m = new Mesh { name = "Sphere" + segments, indexFormat = segments > 180 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            spheres[segments] = m;
            return m;
        }

        /// <summary>Replaces the mesh of a primitive sphere with the smooth one (keeps its collider).</summary>
        public static GameObject Smooth(GameObject sphere, int segments)
        {
            var mf = sphere.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = Sphere(segments);
            return sphere;
        }
    }
}
