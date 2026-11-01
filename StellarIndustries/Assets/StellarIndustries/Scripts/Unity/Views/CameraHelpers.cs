using UnityEngine;

namespace StellarIndustries.Unity.Views
{
    /// <summary>Keeps a quad facing the camera (glows, atmospheric halos).</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    /// <summary>Keeps the sky sphere centred on the camera so it never gets closer.</summary>
    public class FollowCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.position = cam.transform.position;
        }
    }

    /// <summary>Spins an object around its local up axis (planet rotation, clouds).</summary>
    public class Spin : MonoBehaviour
    {
        public float DegreesPerSecond = 5;
        void Update() => transform.Rotate(0, DegreesPerSecond * Time.deltaTime, 0, Space.Self);
    }
}
