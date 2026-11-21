using UnityEngine;

namespace BodyCurrent
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class BodyArc : MonoBehaviour
    {
        public Transform startAnchor;
        public Transform endAnchor;
        public Transform characterSpace;
        public Vector3 archDirection = Vector3.forward;
        [Min(0)] public float archHeight = 0.025f;
        [Min(0)] public float jitter = 0.027f;
        [Min(1)] public float changesPerSecond = 18f;
        public int seed;
        public Vector2 flowRange = new(0f, 1f);

        static readonly int FlowRange = Shader.PropertyToID("_FlowRange");
        const int PointCount = 11;
        LineRenderer line;

        public void Sample(float time)
        {
            if (!startAnchor || !endAnchor || !characterSpace)
                return;

            if (!line)
            {
                line = GetComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = PointCount;
                var properties = new MaterialPropertyBlock();
                line.GetPropertyBlock(properties);
                properties.SetVector(FlowRange, new Vector4(flowRange.x, flowRange.y, 0f, 0f));
                line.SetPropertyBlock(properties);
            }
            Vector3 start = startAnchor.position;
            Vector3 end = endAnchor.position;
            Vector3 outward = characterSpace.TransformDirection(archDirection.normalized);
            Vector3 sideways = Vector3.Cross((end - start).normalized, outward).normalized;
            int tick = Mathf.FloorToInt(time * changesPerSecond);
            var random = new System.Random(unchecked(seed * 486187739 + tick));

            line.SetPosition(0, transform.InverseTransformPoint(start));
            for (int i = 1; i < PointCount - 1; i++)
            {
                float along = i / (PointCount - 1f);
                float envelope = Mathf.Sin(along * Mathf.PI);
                float offset = (float)random.NextDouble() * jitter;
                float across = ((float)random.NextDouble() * 2 - 1) * jitter;
                Vector3 position = Vector3.Lerp(start, end, along);
                position += envelope * (outward * (archHeight + offset) + sideways * across);
                line.SetPosition(i, transform.InverseTransformPoint(position));
            }
            line.SetPosition(PointCount - 1, transform.InverseTransformPoint(end));

            float pulse = 0.56f + 0.44f * (float)random.NextDouble();
            line.startColor = new Color(0.22f, 0.64f, 1f, pulse);
            line.endColor = new Color(0.60f, 0.86f, 1f, pulse);
        }
    }
}
