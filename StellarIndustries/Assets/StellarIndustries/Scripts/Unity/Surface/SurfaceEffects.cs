using StellarIndustries.Unity.Performance;
using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// Dust that obeys the local gravity: each particle's lifetime is the exact ballistic flight time back to the
    /// ground (t = 2·vy/g). Without air (Moon) dust flies in clean parabolas and never hangs in the air; with an
    /// atmosphere drag slows it and the puff expands and drifts.
    /// </summary>
    public class DustSystem : MonoBehaviour
    {
        ParticleSystem ps;
        float g, budget, accum;
        bool airless;

        public void Init(SurfaceWorld w)
        {
            g = w.GravityMs2;
            airless = w.Airless;
            budget = GraphicsQuality.ParticleBudget;
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.maxParticles = (int)(4000 * budget);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = g / 9.81f;
            var ground = VisualFactory.Hex(w.Def.GroundColorHex, Color.gray);
            main.startColor = new Color(Mathf.Min(1, ground.r * 1.35f), Mathf.Min(1, ground.g * 1.35f), Mathf.Min(1, ground.b * 1.35f), airless ? 0.85f : 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(airless ? 0.06f : 0.25f, airless ? 0.18f : 0.7f);
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            if (!airless)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.limit = 100;
                lim.drag = w.Def.Id == "mars" ? 0.9f : 2.2f; // thin Martian air brakes dust far less than Earth's
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(airless ? 0.9f : 0.5f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var r = GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = new Material(VisualFactory.UnlitShader) { mainTexture = VisualFactory.GlowTexture, enableInstancing = true };
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void Burst(Vector3 pos, Vector3 baseVelocity, float spread, int count)
        {
            if (ps == null) return;
            accum += count * budget;
            int n = (int)accum;
            accum -= n;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                var v = baseVelocity + Random.insideUnitSphere * spread;
                if (v.y < 0.3f) v.y = 0.3f + Random.value * 0.5f;
                ep.position = pos + Random.insideUnitSphere * 0.2f;
                ep.velocity = v;
                ep.startLifetime = airless ? Mathf.Clamp(2 * v.y / g, 0.15f, 14f) : Random.Range(1.4f, 3.2f);
                ps.Emit(ep, 1);
            }
        }
    }

    /// <summary>
    /// Footprints and wheel tracks as pooled decals. On airless worlds they stay (no wind, like the Apollo prints);
    /// with an atmosphere they fade after half a minute.
    /// </summary>
    public class Footprints : MonoBehaviour
    {
        GameObject[] pool;
        float[] born;
        int next;
        bool permanent;
        Material mat;
        SurfaceWorld world;

        public void Init(SurfaceWorld w)
        {
            world = w;
            permanent = w.Airless;
            int n = GraphicsQuality.MaxFootprints;
            pool = new GameObject[n]; born = new float[n];
            mat = new Material(VisualFactory.UnlitShader) { mainTexture = VisualFactory.CircleTexture, color = new Color(0, 0, 0, w.Airless ? 0.42f : 0.28f), enableInstancing = true };
        }

        public void Stamp(Vector3 pos, float yaw, Vector2 size)
        {
            if (pool == null || pool.Length == 0) return;
            var go = pool[next];
            if (go == null)
            {
                go = VisualFactory.Primitive(PrimitiveType.Quad, "Print", transform);
                go.GetComponent<Renderer>().sharedMaterial = mat;
                pool[next] = go;
            }
            var n = world.NormalAt(pos.x, pos.z);
            go.transform.position = new Vector3(pos.x, world.HeightAt(pos.x, pos.z) + 0.03f, pos.z);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(90, yaw, 0);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            go.SetActive(true);
            born[next] = Time.time;
            next = (next + 1) % pool.Length;
        }

        void Update()
        {
            if (permanent || pool == null) return;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null && pool[i].activeSelf && Time.time - born[i] > 30) pool[i].SetActive(false);
        }
    }
}
