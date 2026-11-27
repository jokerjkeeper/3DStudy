using System.Collections.Generic;
using UnityEngine;

namespace InteractiveFog
{
    /// <summary>
    /// Anything that pushes the fog: characters, skills, explosions, the mouse.
    /// Velocity is derived from the transform's movement every simulation step.
    /// </summary>
    public class FogInteractor : MonoBehaviour
    {
        internal static readonly List<FogInteractor> All = new List<FogInteractor>();

        [Tooltip("Influence radius in world units")]
        [Min(0.1f)] public float radius = 1.15f;
        [Tooltip("Constant outward push (world units / s), so the fog parts even when standing still")]
        public float push = 0.35f;
        [Tooltip("Extra outward push per unit of movement speed")]
        public float pushBySpeed = 0.55f;
        [Tooltip("How fast the fog velocity under the interactor matches the target (1/s)")]
        public float follow = 9f;
        [Tooltip("How fast the fog density is carved away under the interactor (1/s). 0 = only pushes")]
        public float carve = 7f;
        [Tooltip("Movement velocity multiplier")]
        public float velocityGain = 1.15f;
        public float maxSpeed = 22f;

        Vector3 _prev;

        void OnEnable()
        {
            _prev = transform.position;
            All.Add(this);
        }

        void OnDisable() => All.Remove(this);

        internal Vector2 SampleVelocity(float dt)
        {
            Vector3 p = transform.position;
            Vector3 d = p - _prev;
            _prev = p;
            var v = new Vector2(d.x, d.z) / Mathf.Max(dt, 1e-4f);
            if (v.magnitude > maxSpeed) v = v.normalized * maxSpeed;
            return v * velocityGain;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
