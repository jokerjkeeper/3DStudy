using UnityEngine;
using UnityEngine.Rendering;

namespace StormLab
{
    sealed class DischargeLayers
    {
        enum Shape { Glow, Streak, Ring }
        const int MaxQuads = 56;
        const float GoldenAngle = 2.399963f;
        static readonly Color Warm = new(1f, 0.48f, 0.15f);
        static readonly Color Hot = new(1f, 0.94f, 0.77f);

        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Material material;
        readonly Camera camera;
        readonly Vector3[] vertices = new Vector3[MaxQuads * 4];
        readonly Vector2[] uv = new Vector2[MaxQuads * 4];
        readonly Vector2[] shapes = new Vector2[MaxQuads * 4];
        readonly Color[] colors = new Color[MaxQuads * 4];
        readonly int[] indices = new int[MaxQuads * 6];
        Vector3 right, up, towardCamera;
        int count;

        public DischargeLayers(Transform parent, Shader shader, Camera view)
        {
            camera = view;
            var holder = new GameObject("Discharge accents");
            holder.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Discharge accent quads" };
            mesh.MarkDynamic();
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = holder.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            material = new Material(shader) { name = "Discharge accent material" };
            renderer.sharedMaterial = material;
            renderer.enabled = false;

            for (int i = 0; i < MaxQuads; i++)
            {
                int vertex = i * 4;
                int start = i * 6;
                uv[vertex] = Vector2.zero;
                uv[vertex + 1] = Vector2.right;
                uv[vertex + 2] = Vector2.one;
                uv[vertex + 3] = Vector2.up;
                indices[start] = vertex;
                indices[start + 1] = vertex + 1;
                indices[start + 2] = vertex + 2;
                indices[start + 3] = vertex;
                indices[start + 4] = vertex + 2;
                indices[start + 5] = vertex + 3;
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
        }

        public void Charge(Vector3 center, float progress, Color tint, bool halo, bool sparks)
        {
            Begin(center);
            float build = Mathf.SmoothStep(0, 1, progress);
            float pulse = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 1f, progress));
            float time = Time.time;
            if (halo)
            {
                Add(center, right, up, 0.68f + pulse * 0.23f, 0.68f + pulse * 0.23f, Shape.Glow, tint, 0.52f + build * 1.65f);
                Add(center, right, up, 0.23f, 0.23f, Shape.Glow, Hot, 0.85f + pulse * 3.2f);
                Add(center, right, up, 0.29f, 0.035f, Shape.Streak, Hot, pulse * 1.6f);
                float haloSize = Mathf.Lerp(0.76f, 0.38f, build);
                Add(center, right, up, haloSize, haloSize, Shape.Ring, tint, build * 0.65f, time * 5);
            }
            if (sparks)
            {
                for (int i = 0; i < 22; i++)
                {
                    float seed = Seed(i, 0.619f);
                    float life = Mathf.Repeat(time * Mathf.Lerp(0.78f, 1.34f, seed) + seed, 1);
                    float angle = i * GoldenAngle + time * (3f + Seed(i, 0.414f));
                    float radius = Mathf.Lerp(0.96f, 0.12f, life * life) * (0.72f + seed * 0.45f);
                    Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle) * 0.73f) * radius;
                    offset += towardCamera * Mathf.Sin(angle * 0.61f) * radius * 0.43f;
                    float size = Mathf.Lerp(0.025f, 0.085f, seed * seed);
                    float opacity = Mathf.Sin(life * Mathf.PI) * (0.6f + build * 1.6f);
                    Add(center + offset, right, up, size, size, Shape.Glow, i % 5 == 0 ? Warm : tint, opacity);
                }
            }
            Submit();
        }

        public void Travel(Vector3 center, Vector3 path, float progress, Color tint)
        {
            Begin(center);
            float pathLength = path.magnitude;
            Vector3 direction = path.normalized;
            Vector3 longAxis = Vector3.ProjectOnPlane(direction, towardCamera).normalized;
            if (longAxis.sqrMagnitude < 0.01f) longAxis = right;
            Vector3 shortAxis = Vector3.Cross(towardCamera, longAxis).normalized;
            float fadeIn = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.12f, progress));
            Add(center, longAxis, shortAxis, 0.62f, 0.34f, Shape.Glow, tint, 2.8f * fadeIn);
            Add(center, longAxis, shortAxis, 0.36f, 0.056f, Shape.Streak, Hot, 4.2f * fadeIn);
            for (int i = 0; i < 8; i++)
            {
                float lag = (i + 1) * 0.115f;
                if (lag > progress * pathLength) break;
                float sway = Mathf.Sin(i * 2.17f + progress * 38f) * lag * 0.24f;
                Vector3 position = center - direction * lag + shortAxis * sway;
                Add(position, longAxis, shortAxis, 0.18f - i * 0.011f, 0.029f, Shape.Streak,
                    i % 3 == 0 ? Hot : tint, fadeIn * (1.5f - i * 0.14f));
            }
            Submit();
        }

        public void Impact(Vector3 center, Vector3 normal, float age, Color tint)
        {
            Begin(center);
            float flash = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.17f, age));
            float shock = Mathf.InverseLerp(0, 0.46f, age);
            float secondary = Mathf.InverseLerp(0.065f, 0.49f, age);
            float reach = 1f - (1f - shock) * (1f - shock);
            Vector3 burstAxis = (right + up * 0.23f).normalized;
            Vector3 across = Vector3.Cross(towardCamera, burstAxis).normalized;
            Vector3 ringRight = Vector3.Cross(normal, Vector3.up).normalized;
            if (ringRight.sqrMagnitude < 0.01f) ringRight = Vector3.right;
            Vector3 ringUp = Vector3.Cross(ringRight, normal).normalized;
            Add(center, right, up, 1.3f, 1.3f, Shape.Glow, tint, 5f * flash);
            Add(center, right, up, 0.53f, 0.53f, Shape.Glow, Hot, 9f * flash);
            Add(center, burstAxis, across, 1.63f, 0.075f, Shape.Streak, Hot, 4.3f * flash);
            Add(center, across, burstAxis, 0.74f, 0.055f, Shape.Streak, tint, 2.9f * flash);
            Add(center, right, up, 0.38f + reach * 1.46f, 0.32f + reach * 1.26f,
                Shape.Ring, tint, (1f - shock) * (1f - shock) * 1.8f, age * 7);
            if (age > 0.065f)
            {
                float spread = Mathf.Lerp(0.50f, 2.1f, Mathf.Sqrt(secondary));
                Add(center + normal * 0.16f, ringRight, ringUp, spread, spread, Shape.Ring, Warm,
                    (1f - secondary) * 0.82f, 2.8f - age * 4);
            }

            for (int i = 0; i < 42; i++)
            {
                float seed = Seed(i, 0.618f);
                float start = i < 22 ? 0 : 0.055f + seed * 0.10f;
                float life = Mathf.Lerp(0.24f, 0.67f, Seed(i, 0.391f));
                if (age < start || age > start + life) continue;
                float t = (age - start) / life;
                float azimuth = i * GoldenAngle + 0.4f;
                Vector3 direction = ringRight * Mathf.Cos(azimuth) + ringUp * Mathf.Sin(azimuth) * 0.85f;
                Vector3 velocity = direction * Mathf.Lerp(1.4f, 8f, seed * seed) + normal * (0.55f + seed);
                float travel = (1f - (1f - t) * (1f - t)) * life * 0.56f;
                Vector3 point = center + velocity * travel - Vector3.up * (travel * travel * 1.4f);
                float opacity = (1f - t) * (1f - t) * (1.5f + seed * 3f);
                Color color = i % 4 == 0 ? Hot : i % 3 == 0 ? Warm : tint;
                if (i < 22)
                {
                    Vector3 axis = Vector3.ProjectOnPlane(velocity, towardCamera).normalized;
                    Add(point, axis, Vector3.Cross(towardCamera, axis).normalized,
                        (0.05f + seed * 0.21f) * (1f - t * 0.55f), 0.016f + seed * 0.019f, Shape.Streak, color, opacity);
                }
                else
                {
                    float size = (0.028f + seed * seed * 0.076f) * (1f - t * 0.5f);
                    Add(point, right, up, size, size, Shape.Glow, color, opacity);
                }
            }
            Submit();
        }

        public void Hide() => renderer.enabled = false;

        public void Dispose()
        {
            Object.Destroy(renderer.gameObject);
            Object.Destroy(mesh);
            Object.Destroy(material);
        }

        void Begin(Vector3 origin)
        {
            count = 0;
            renderer.transform.position = origin;
            towardCamera = camera.orthographic ? -camera.transform.forward : (camera.transform.position - origin).normalized;
            right = Vector3.Cross(towardCamera, camera.transform.up).normalized;
            if (right.sqrMagnitude < 0.01f) right = camera.transform.right;
            up = Vector3.Cross(right, towardCamera).normalized;
        }

        void Add(Vector3 center, Vector3 horizontal, Vector3 vertical, float halfWidth, float halfHeight,
            Shape shape, Color color, float opacity, float phase = 0)
        {
            if (opacity <= 0 || count == MaxQuads) return;
            int vertex = count++ * 4;
            Vector3 x = horizontal * halfWidth;
            Vector3 y = vertical * halfHeight;
            Transform holder = renderer.transform;
            vertices[vertex] = holder.InverseTransformPoint(center - x - y);
            vertices[vertex + 1] = holder.InverseTransformPoint(center + x - y);
            vertices[vertex + 2] = holder.InverseTransformPoint(center + x + y);
            vertices[vertex + 3] = holder.InverseTransformPoint(center - x + y);
            color.a = opacity;
            var settings = new Vector2((float)shape, phase);
            for (int i = vertex; i < vertex + 4; i++)
            {
                colors[i] = color;
                shapes[i] = settings;
            }
        }

        void Submit()
        {
            if (count == 0)
            {
                Hide();
                return;
            }
            // Keep the vertex channels at full capacity as the active index range grows and shrinks.
            mesh.SetVertices(vertices);
            mesh.SetUVs(1, shapes);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, 0, count * 6, MeshTopology.Triangles, 0, false);
            var bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < count * 4; i++)
                bounds.Encapsulate(vertices[i]);
            mesh.bounds = bounds;
            renderer.enabled = true;
        }

        static float Seed(int index, float interval) => Mathf.Repeat((index + 1) * interval, 1);
    }
}
