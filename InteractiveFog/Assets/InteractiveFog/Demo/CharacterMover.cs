using UnityEngine;

namespace InteractiveFog.Demo
{
    /// <summary>WASD movement relative to the camera, terrain following and a simple walk swing.</summary>
    public class CharacterMover : MonoBehaviour
    {
        public float speed = 5.5f;
        public Terrain terrain;
        public Transform legL, legR, armL, armR;

        Vector3 _vel;
        float _phase;

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = FogDemoInput.Move();
            var cam = Camera.main;
            Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = cam ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 target = fwd * input.y + right * input.x;
            if (target.sqrMagnitude > 1f) target.Normalize();
            target *= speed;
            _vel = Vector3.Lerp(_vel, target, 1f - Mathf.Exp(-dt * 10f));

            Vector3 p = transform.position + _vel * dt;
            var sim = FogSimulation.Active;
            if (sim != null)
            {
                float lim = sim.worldSize * 0.45f;
                Vector3 c = sim.transform.position;
                p.x = Mathf.Clamp(p.x, c.x - lim, c.x + lim);
                p.z = Mathf.Clamp(p.z, c.z - lim, c.z + lim);
            }
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            transform.position = p;

            float sp = new Vector2(_vel.x, _vel.z).magnitude;
            if (sp > 0.2f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(new Vector3(_vel.x, 0, _vel.z)), 1f - Mathf.Exp(-dt * 12f));

            _phase += dt * sp * 2.3f;
            float sw = Mathf.Sin(_phase) * 37f * Mathf.Min(sp / speed, 1f);
            if (legL) legL.localRotation = Quaternion.Euler(sw, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-sw, 0, 0);
            if (armL) armL.localRotation = Quaternion.Euler(-sw, 0, 0);
            if (armR) armR.localRotation = Quaternion.Euler(sw, 0, 0);
        }
    }
}
