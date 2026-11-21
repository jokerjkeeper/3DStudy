using UnityEngine;

namespace StormLab
{
    public sealed class ArcFlash : MonoBehaviour
    {
        [SerializeField] Material material;
        [SerializeField, Range(8, 48)] int vertices = 20;
        [SerializeField, Min(0.01f)] float amplitude = 0.53f;
        [SerializeField, Min(0.01f)] float mainWidth = 0.12f;
        [SerializeField, Min(0.1f)] float lifetime = 0.43f;
        [SerializeField, Range(0, 8)] int branches = 5;
        [SerializeField, Min(0.01f)] float branchLifetime = 0.16f;

        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Strength = Shader.PropertyToID("_Strength");
        static readonly int Erosion = Shader.PropertyToID("_Erosion");
        static readonly int Seed = Shader.PropertyToID("_Seed");

        LineRenderer[] lines;
        MaterialPropertyBlock properties;
        Vector3 source, destination;
        Color tint;
        float phase, age;
        bool showBranches;

        public void SetMaterial(Material value) => material = value;

        public void UseMaterialOverride(Material alternate)
        {
            foreach (var line in lines)
                line.sharedMaterial = alternate != null ? alternate : material;
        }

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            lines = new LineRenderer[branches + 1];
            for (int i = 0; i < lines.Length; i++)
            {
                var line = new GameObject(i == 0 ? "Main arc" : $"Branch {i}").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = material;
                line.positionCount = i == 0 ? vertices : 8;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.widthCurve = i == 0 ? AnimationCurve.Linear(0, 0.87f, 1, 1) :
                    AnimationCurve.Linear(0, 1, 1, 0.12f);
                line.enabled = false;
                lines[i] = line;
            }
        }

        public void Fire(Vector3 start, Vector3 end, Color color, bool enableBranches)
        {
            source = start;
            destination = end;
            tint = color;
            showBranches = enableBranches;
            phase = Random.Range(1f, 800f);
            age = 0;
            Draw();
        }

        public void Clear()
        {
            age = lifetime;
            foreach (var line in lines)
                line.enabled = false;
        }

        void Update()
        {
            if (age >= lifetime || !lines[0].enabled)
                return;

            age += Time.deltaTime;
            if (age >= lifetime)
                Clear();
            else
                Draw();
        }

        void Draw()
        {
            Vector3 span = destination - source;
            Vector3 axis = span.normalized;
            Vector3 sideways = Vector3.Cross(axis, Vector3.up).normalized;
            if (sideways.sqrMagnitude < 0.1f)
                sideways = Vector3.right;
            Vector3 up = Vector3.Cross(sideways, axis);
            float width = amplitude * Mathf.Clamp(span.magnitude / 4f, 0.15f, 1.25f);
            float time = age * 29f;
            var main = lines[0];
            main.widthMultiplier = mainWidth;
            for (int i = 0; i < main.positionCount; i++)
            {
                float t = (float)i / (main.positionCount - 1);
                float envelope = i == 0 || i == main.positionCount - 1 ? 0 : Mathf.Sin(Mathf.PI * t);
                main.SetPosition(i, Vector3.Lerp(source, destination, t) +
                    Noise(sideways, up, i * 0.71f, time, phase) * (width * envelope));
            }
            float progress = age / lifetime;
            SetAppearance(main, 1f - progress * 0.5f, Mathf.Pow(progress, 1.8f), phase);

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                // Stagger each branch so a shot has multiple short flashes.
                float branchAge = (age - (i - 1) * 0.023f) / branchLifetime;
                line.enabled = showBranches && branchAge >= 0 && branchAge < 1;
                if (!line.enabled)
                    continue;

                float seed = phase + 17.7f * i;
                int root = Mathf.Clamp(Mathf.RoundToInt(main.positionCount * (0.14f + 0.70f * i / lines.Length)), 1, main.positionCount - 2);
                Vector3 start = main.GetPosition(root);
                float side = i % 2 == 0 ? 1 : -1;
                Vector3 end = start + (sideways * side * (0.56f + 0.25f * Mathf.Sin(seed)) +
                    up * (0.37f * Mathf.Cos(seed)) + axis * 0.69f) * Mathf.Min(1.2f, span.magnitude * 0.32f);
                line.widthMultiplier = mainWidth * 0.44f;
                for (int j = 0; j < line.positionCount; j++)
                {
                    float t = (float)j / (line.positionCount - 1);
                    float envelope = j == 0 || j == line.positionCount - 1 ? 0 : Mathf.Sin(Mathf.PI * t);
                    line.SetPosition(j, Vector3.Lerp(start, end, t) +
                        Noise(sideways, up, j * 0.53f, time, seed) * (0.24f * envelope));
                }
                SetAppearance(line, 0.75f, Mathf.Pow(branchAge, 1.5f), seed);
            }
        }

        static Vector3 Noise(Vector3 sideways, Vector3 up, float x, float time, float seed)
        {
            float horizontal = Mathf.PerlinNoise(x + seed, time + 11f) * 2f - 1f;
            float vertical = Mathf.PerlinNoise(x + seed + 20f, time + 31f) * 2f - 1f;
            return sideways * horizontal + up * vertical;
        }

        void SetAppearance(LineRenderer line, float strength, float erosion, float seed)
        {
            properties.SetColor(Tint, tint);
            properties.SetFloat(Strength, strength);
            properties.SetFloat(Erosion, erosion);
            properties.SetFloat(Seed, seed);
            line.SetPropertyBlock(properties);
            line.enabled = true;
        }
    }
}
