using UnityEngine;
using UnityEngine.Rendering;

namespace StormLab
{
    public sealed class HandCharge : MonoBehaviour
    {
        [SerializeField] Transform palm;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] Material arcGraph;
        [SerializeField, Min(0.1f)] float reach = 1.25f;
        [SerializeField, Min(0.005f)] float width = 0.065f;

        [field: SerializeField] public bool ShowArcs { get; set; } = true;
        [field: SerializeField] public bool ShowSparks { get; set; } = true;

        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Strength = Shader.PropertyToID("_Strength");
        static readonly int Erosion = Shader.PropertyToID("_Erosion");
        static readonly int Seed = Shader.PropertyToID("_Seed");
        static readonly Vector3[] Directions = {
            new(-0.80f, 0.77f, 0.16f), new(-0.24f, 1.10f, 0.32f),
            new(0.43f, 0.92f, -0.12f), new(0.89f, 0.52f, 0.27f),
            new(0.61f, 0.07f, 0.85f)
        };

        LineRenderer[] primary, branches;
        Transform effectOrigin;
        MaterialPropertyBlock properties;
        public Transform Palm => palm;

        public void Configure(Transform anchor, ParticleSystem system, Material graph)
        {
            palm = anchor;
            sparks = system;
            arcGraph = graph;
        }

        void Awake()
        {
            effectOrigin = new GameObject("Workflow effect origin").transform;
            effectOrigin.SetParent(palm, false);
            sparks.transform.SetParent(effectOrigin, true);
            primary = new LineRenderer[Directions.Length];
            branches = new LineRenderer[Directions.Length];
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < Directions.Length; i++)
            {
                primary[i] = MakeRibbon($"Palm arc {i + 1}", 12);
                branches[i] = MakeRibbon($"Palm branch {i + 1}", 7);
            }
            Clear();
        }

        LineRenderer MakeRibbon(string label, int points)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(effectOrigin, false);
            line.useWorldSpace = false;
            line.positionCount = points;
            line.sharedMaterial = arcGraph;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthCurve = AnimationCurve.Linear(0, 1, 1, 0.12f);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public void RenderCharge(float progress) => RenderLayers(palm.position, progress, true, true);

        public void RenderLayers(Vector3 center, float progress, bool arcLayer, bool particleLayer)
        {
            effectOrigin.position = center;
            bool showSparks = ShowSparks && particleLayer;
            if (showSparks && !sparks.isPlaying) sparks.Play();
            if (!showSparks && sparks.isPlaying)
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            for (int i = 0; i < Directions.Length; i++)
            {
                var main = primary[i];
                var branch = branches[i];
                if (!ShowArcs || !arcLayer)
                {
                    main.enabled = branch.enabled = false;
                    continue;
                }

                float phase = Time.time * 20f + i * 9.71f;
                float flicker = Mathf.PerlinNoise(phase * 0.62f, i * 13.7f);
                float span = reach * Mathf.Lerp(0.42f, 1f, progress) * Mathf.Lerp(0.72f, 1.13f, flicker);
                Vector3 tip = Directions[i] * span;
                BuildRibbon(main, Vector3.zero, tip, phase, span * 0.24f);
                DrawRibbon(main, width * Mathf.Lerp(0.57f, 1f, progress), 0.83f + flicker * 0.65f, 0.035f, i);

                Vector3 root = main.GetPosition(5 + i % 3);
                float side = i % 2 == 0 ? 1 : -1;
                Vector3 extension = new Vector3(side * 0.46f, 0.34f, side * 0.26f) * span;
                BuildRibbon(branch, root, root + extension, phase + 23f, span * 0.12f);
                DrawRibbon(branch, width * 0.57f, 0.67f + flicker * 0.54f, Mathf.Lerp(0.06f, 0.24f, flicker), i + 19f);
            }
        }

        void Clear()
        {
            foreach (var line in primary) line.enabled = false;
            foreach (var line in branches) line.enabled = false;
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void StopCharge() => Clear();

        static void BuildRibbon(LineRenderer line, Vector3 start, Vector3 end, float phase, float displacement)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.forward).normalized;
            if (side.sqrMagnitude < 0.1f) side = Vector3.right;
            Vector3 other = Vector3.Cross(axis, side);
            line.SetPosition(0, start);
            line.SetPosition(line.positionCount - 1, end);
            for (int j = 1; j < line.positionCount - 1; j++)
            {
                float t = j / (float)(line.positionCount - 1);
                float x = Mathf.PerlinNoise(j * 1.14f, phase) * 2f - 1f;
                float y = Mathf.PerlinNoise(j * 1.31f + 33f, phase + 17f) * 2f - 1f;
                line.SetPosition(j, Vector3.Lerp(start, end, t) +
                    (side * x + other * y) * (displacement * Mathf.Sin(Mathf.PI * t)));
            }
        }

        void DrawRibbon(LineRenderer line, float ribbonWidth, float strength, float erosion, float seed)
        {
            line.widthMultiplier = ribbonWidth;
            properties.SetColor(Tint, new Color(0.78f, 0.92f, 1f));
            properties.SetFloat(Strength, strength);
            properties.SetFloat(Erosion, erosion);
            properties.SetFloat(Seed, seed * 19.17f);
            line.SetPropertyBlock(properties);
            line.enabled = true;
        }
    }
}
