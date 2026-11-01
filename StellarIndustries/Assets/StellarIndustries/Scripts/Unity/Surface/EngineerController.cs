using StellarIndustries.Unity.Views;
using UnityEngine;

namespace StellarIndustries.Unity.Surface
{
    /// <summary>
    /// The player's engineer. Walks with WASD (camera-relative), Shift sprints, Space jumps.
    /// Uses the location's real surface gravity: the same jump reaches ~0.5 m on Earth, ~1.2 m on Mars, ~2.8 m on the Moon,
    /// and low gravity also means less traction (slower acceleration and braking).
    /// </summary>
    public class EngineerController : MonoBehaviour
    {
        public const float WalkSpeed = 4.2f, SprintSpeed = 7.5f, JumpSpeed = 3.0f;
        SurfaceWorld world;
        CharacterController cc;
        Vector3 velocity;
        Transform model, legL, legR, armL, armR;
        float phase;
        public bool InRover;
        public bool Drilling { get; set; }
        /// <summary>Cold-gas jetpack, only where gravity is low and there is no air to make it pointless (g &lt; 4 m/s²).</summary>
        public bool HasJetpack => world != null && world.Airless && world.GravityMs2 < 4;
        public float JetFuel { get; private set; } = 1;
        Transform jetFlame;
        bool wasGrounded = true, leftFoot;
        float lastStepPhase;
        public float Speed => new Vector2(velocity.x, velocity.z).magnitude;

        public void Init(SurfaceWorld w, Vector3 spawn)
        {
            world = w;
            gameObject.layer = 2; // ignored by placement raycasts
            cc = gameObject.AddComponent<CharacterController>();
            cc.height = 1.85f; cc.radius = 0.38f; cc.center = new Vector3(0, 0.93f, 0); cc.stepOffset = 0.45f; cc.slopeLimit = 50;
            transform.position = new Vector3(spawn.x, w.HeightAt(spawn.x, spawn.z) + 0.2f, spawn.z);
            BuildModel();
        }

        void BuildModel()
        {
            model = new GameObject("Suit").transform;
            model.SetParent(transform, false);
            var suit = StructureFactory.Mat("suit", new Color(0.93f, 0.93f, 0.95f), 0.35f);
            var orange = StructureFactory.Mat("suit_orange", new Color(1f, 0.5f, 0.12f), 0.35f);
            var visor = StructureFactory.Mat("visor", new Color(0.85f, 0.62f, 0.2f), 0.95f);
            var dark = StructureFactory.Mat("dark", new Color(0.22f, 0.24f, 0.28f), 0.4f);
            StructureFactory.P(PrimitiveType.Capsule, model, new Vector3(0, 1.1f, 0), new Vector3(0.62f, 0.45f, 0.42f), suit);
            StructureFactory.P(PrimitiveType.Cube, model, new Vector3(0, 1.2f, -0.3f), new Vector3(0.5f, 0.62f, 0.26f), suit);
            StructureFactory.P(PrimitiveType.Cube, model, new Vector3(0, 1.22f, 0.22f), new Vector3(0.28f, 0.2f, 0.05f), orange);
            StructureFactory.P(PrimitiveType.Sphere, model, new Vector3(0, 1.72f, 0), Vector3.one * 0.46f, suit);
            StructureFactory.P(PrimitiveType.Sphere, model, new Vector3(0, 1.73f, 0.1f), new Vector3(0.36f, 0.27f, 0.3f), visor);
            legL = Limb(model, new Vector3(-0.15f, 0.78f, 0), suit, dark, 0.78f);
            legR = Limb(model, new Vector3(0.15f, 0.78f, 0), suit, dark, 0.78f);
            armL = Limb(model, new Vector3(-0.38f, 1.42f, 0), suit, orange, 0.6f);
            armR = Limb(model, new Vector3(0.38f, 1.42f, 0), suit, orange, 0.6f);
            jetFlame = VisualFactory.Billboard("Jet", model, VisualFactory.GlowTexture, new Color(0.6f, 0.85f, 1f, 1f), 0.9f).transform;
            jetFlame.localPosition = new Vector3(0, 0.75f, -0.42f);
            jetFlame.localScale = Vector3.zero;
            var lamp = new GameObject("Helmet lamp").AddComponent<Light>();
            lamp.transform.SetParent(model, false); lamp.transform.localPosition = new Vector3(0, 1.85f, 0.2f); lamp.transform.localRotation = Quaternion.Euler(12, 0, 0);
            lamp.type = LightType.Spot; lamp.spotAngle = 55; lamp.range = 28; lamp.intensity = 1.2f; lamp.color = new Color(1f, 0.96f, 0.88f);
        }

        static Transform Limb(Transform parent, Vector3 pivot, Material m, Material end, float length)
        {
            var p = new GameObject("Limb").transform;
            p.SetParent(parent, false); p.localPosition = pivot;
            StructureFactory.P(PrimitiveType.Capsule, p, new Vector3(0, -length * 0.45f, 0), new Vector3(0.17f, length * 0.48f, 0.17f), m);
            StructureFactory.P(PrimitiveType.Sphere, p, new Vector3(0, -length * 0.92f, 0.03f), new Vector3(0.19f, 0.14f, 0.24f), end);
            return p;
        }

        public void SetVisible(bool v) { model.gameObject.SetActive(v); cc.enabled = v; }

        public void Teleport(Vector3 p) { cc.enabled = false; transform.position = p; cc.enabled = true; velocity = Vector3.zero; }

        void Update()
        {
            if (InRover || world == null || !world.Active) return;
            float dt = Time.deltaTime, g = world.GravityMs2;
            var cam = world.CameraRig;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 input = Vector3.zero;
            if (SurfaceInput.Held(KeyCode.W)) input += fwd;
            if (SurfaceInput.Held(KeyCode.S)) input -= fwd;
            if (SurfaceInput.Held(KeyCode.D)) input += right;
            if (SurfaceInput.Held(KeyCode.A)) input -= right;
            if (input.sqrMagnitude > 1) input.Normalize();
            if (Drilling) input = Vector3.zero;
            float speed = SurfaceInput.Held(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed;
            // Traction scales with gravity: on the Moon you start and stop sluggishly (but never fully lose control).
            float traction = Mathf.Lerp(2.5f, 12f, Mathf.Clamp01(g / 9.81f)) * (cc.isGrounded ? 1 : 0.25f);
            var target = input * speed;
            var horiz = Vector3.Lerp(new Vector3(velocity.x, 0, velocity.z), target, 1 - Mathf.Exp(-traction * dt));
            velocity.x = horiz.x; velocity.z = horiz.z;
            if (cc.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -1f;
                if (SurfaceInput.Pressed(KeyCode.Space)) velocity.y = JumpSpeed;
            }
            // Jetpack: hold Space in the air. Thrust 1.5 g (net upward 0.5 g), ~3 s of fuel, refills on the ground.
            bool jetting = false;
            if (HasJetpack)
            {
                if (!cc.isGrounded && SurfaceInput.Held(KeyCode.Space) && JetFuel > 0 && velocity.y < JumpSpeed * 1.4f)
                {
                    velocity.y += 1.5f * g * dt; JetFuel = Mathf.Max(0, JetFuel - dt / 3f); jetting = true;
                    float above = transform.position.y - world.HeightAt(transform.position.x, transform.position.z);
                    if (above < 4) world.Dust?.Burst(new Vector3(transform.position.x, world.HeightAt(transform.position.x, transform.position.z), transform.position.z), Vector3.zero, 2.5f, 3);
                }
                else if (cc.isGrounded) JetFuel = Mathf.Min(1, JetFuel + dt * 0.25f);
            }
            jetFlame.localScale = Vector3.one * (jetting ? 0.9f + Random.value * 0.4f : 0);
            velocity.y -= g * dt;
            cc.Move(velocity * dt);
            // Landing puff and footsteps: dust flies ballistically under the local gravity; prints stay on airless worlds.
            if (cc.isGrounded && !wasGrounded) world.Dust?.Burst(transform.position, Vector3.zero, 1.6f, 22);
            wasGrounded = cc.isGrounded;
            if (transform.position.y < world.HeightAt(transform.position.x, transform.position.z) - 2) Teleport(new Vector3(transform.position.x, world.HeightAt(transform.position.x, transform.position.z) + 1, transform.position.z));

            if (horiz.sqrMagnitude > 0.05f) model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(horiz), 1 - Mathf.Exp(-10 * dt));
            // Walk cycle: longer, floatier strides in low gravity.
            float s = horiz.magnitude;
            phase += dt * s * Mathf.Lerp(1.2f, 2.2f, Mathf.Clamp01(g / 9.81f));
            if (cc.isGrounded && s > 0.8f && Mathf.Floor(phase / Mathf.PI) != Mathf.Floor(lastStepPhase / Mathf.PI))
            {
                leftFoot = !leftFoot;
                var side = model.right * (leftFoot ? -0.16f : 0.16f);
                world.Prints?.Stamp(transform.position + side, model.eulerAngles.y, new Vector2(0.16f, 0.32f));
                world.Dust?.Burst(transform.position + side, horiz * 0.25f + Vector3.up * 0.6f, 0.5f, s > WalkSpeed ? 8 : 5);
            }
            lastStepPhase = phase;
            float swing = cc.isGrounded ? Mathf.Sin(phase) * Mathf.Clamp01(s / WalkSpeed) * 35 : 18;
            legL.localRotation = Quaternion.Euler(swing, 0, 0); legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            armL.localRotation = Quaternion.Euler(Drilling ? -70 : -swing * 0.8f, 0, 0); armR.localRotation = Quaternion.Euler(Drilling ? -70 : swing * 0.8f, 0, 0);
        }
    }
}
