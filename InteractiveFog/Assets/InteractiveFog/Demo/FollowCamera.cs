using UnityEngine;

namespace InteractiveFog.Demo
{
    /// <summary>Fixed-angle follow camera (like the 45° battle view), scroll to zoom.</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 10.5f, -9f);
        public float lookHeight = 0.6f;
        [Range(0.45f, 2.2f)] public float zoom = 1f;

        void LateUpdate()
        {
            if (target == null) return;
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-FogDemoInput.Scroll() * 0.12f), 0.45f, 2.2f);
            Vector3 look = target.position + Vector3.up * lookHeight;
            transform.position = look + offset * zoom;
            transform.LookAt(look);
        }
    }
}
