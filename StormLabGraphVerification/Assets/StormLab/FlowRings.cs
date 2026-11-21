using UnityEngine;
using UnityEngine.Rendering;

namespace StormLab
{
    sealed class FlowRings
    {
        const int Segments = 48;
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Strength = Shader.PropertyToID("_Strength");
        static readonly int Seed = Shader.PropertyToID("_Seed");
        readonly LineRenderer[] loops = new LineRenderer[2];

        public FlowRings(Transform parent, Material material)
        {
            var settings = new MaterialPropertyBlock();
            for (int i = 0; i < loops.Length; i++)
            {
                var line = new GameObject($"Workflow halo {i + 1}").AddComponent<LineRenderer>();
                line.transform.SetParent(parent, false);
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = Segments;
                line.widthMultiplier = i == 0 ? 0.046f : 0.026f;
                line.sharedMaterial = material;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.alignment = LineAlignment.View;
                for (int j = 0; j < Segments; j++)
                {
                    float angle = j * Mathf.PI * 2 / Segments;
                    line.SetPosition(j, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
                }

                settings.SetColor(Tint, new Color(1f, 0.60f, 0.18f));
                settings.SetFloat(Strength, i == 0 ? 0.60f : 0.44f);
                settings.SetFloat(Seed, 19f * i);
                line.SetPropertyBlock(settings);
                loops[i] = line;
            }
            Hide();
        }

        public void Draw(Vector3 center, float progress, bool expanding)
        {
            float time = Time.time;
            for (int i = 0; i < loops.Length; i++)
            {
                var line = loops[i];
                float radius = expanding ? Mathf.Lerp(0.21f, 1.22f, progress) : Mathf.Lerp(1.04f, 0.67f, progress);
                line.transform.SetPositionAndRotation(center,
                    Quaternion.Euler(22 + i * 56 + time * 71, -25 + i * 88 + time * (i == 0 ? 93 : -117), 0));
                line.transform.localScale = Vector3.one * radius * (i == 0 ? 1f : 0.78f);
                line.enabled = true;
            }
        }

        public void Hide()
        {
            foreach (var line in loops) line.enabled = false;
        }
    }
}

