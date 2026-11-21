using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StormLab
{
    public sealed class ImpactFeedback : MonoBehaviour
    {
        [SerializeField] Material trailMaterial;
        [SerializeField] Material shockMaterial;
        [SerializeField] Material scorchMaterial;
        [SerializeField] Camera view;
        [SerializeField] Light flash;
        [SerializeField] Renderer shock;
        [SerializeField, Min(0.1f)] float radius = 3.6f;
        [SerializeField, Min(0.05f)] float waveDuration = 0.56f;
        [SerializeField, Min(0f)] float explosionImpulse = 7.4f;
        [SerializeField, Min(0f)] float shakeAmplitude = 0.105f;
        [SerializeField, Range(0f, 0.06f)] float refraction = 0.037f;

        const int SparkCount = 24;
        const int MarkCount = 18;
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Strength = Shader.PropertyToID("_Strength");
        static readonly int Erosion = Shader.PropertyToID("_Erosion");
        static readonly int Age = Shader.PropertyToID("_Age");
        static readonly int Refraction = Shader.PropertyToID("_Refraction");

        struct Spark
        {
            public TrailRenderer Trail;
            public Vector3 Origin, Velocity;
            public float Life;
        }

        Spark[] sparks;
        DecalProjector[] marks;
        MaterialPropertyBlock properties;
        Vector3 viewRest;
        Quaternion viewRotation;
        float elapsed = float.PositiveInfinity;
        bool waveOn, shakeOn;
        int nextMark;

        public void Configure(Camera camera, Light light, Renderer wave,
            Material trail, Material distortion, Material scorch)
        {
            view = camera;
            flash = light;
            shock = wave;
            trailMaterial = trail;
            shockMaterial = distortion;
            scorchMaterial = scorch;
        }

        void Awake()
        {
            viewRest = view.transform.localPosition;
            viewRotation = view.transform.localRotation;
            properties = new MaterialPropertyBlock();
            shock.sharedMaterial = shockMaterial;
            shock.enabled = false;
            flash.enabled = false;
            sparks = new Spark[SparkCount];
            for (int i = 0; i < sparks.Length; i++)
            {
                var trail = new GameObject($"Spark {i + 1:00}").AddComponent<TrailRenderer>();
                trail.transform.SetParent(transform, false);
                trail.sharedMaterial = trailMaterial;
                trail.time = 0.105f;
                trail.minVertexDistance = 0.013f;
                trail.alignment = LineAlignment.View;
                trail.textureMode = LineTextureMode.Stretch;
                trail.widthMultiplier = 0.039f;
                // Trail Renderer UV U runs from the leading end toward the oldest point.
                trail.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                trail.enabled = false;
                sparks[i].Trail = trail;
            }
            marks = new DecalProjector[MarkCount];
            for (int i = 0; i < marks.Length; i++)
            {
                var projector = new GameObject($"Scorch {i + 1:00}").AddComponent<DecalProjector>();
                projector.transform.SetParent(transform, false);
                projector.material = scorchMaterial;
                projector.drawDistance = 35f;
                projector.enabled = false;
                marks[i] = projector;
            }
        }

        public void UseShockMaterialOverride(Material alternate)
            => shock.sharedMaterial = alternate != null ? alternate : shockMaterial;

        public void Fire(RaycastHit hit, Color tint, bool wave, bool force, bool shake, bool scorch)
        {
            elapsed = 0;
            waveOn = wave;
            shakeOn = shake;

            flash.transform.position = hit.point + hit.normal * 0.32f;
            flash.color = tint;
            flash.range = radius * 1.85f;
            flash.intensity = 11f;
            flash.enabled = true;

            shock.transform.position = hit.point + hit.normal * 0.10f;
            shock.transform.rotation = view.transform.rotation;
            shock.transform.localScale = Vector3.one * (radius * 2.0f);
            properties.Clear();
            properties.SetColor(Tint, tint);
            properties.SetFloat(Age, 0);
            properties.SetFloat(Refraction, refraction);
            shock.SetPropertyBlock(properties);
            shock.enabled = wave;

            if (force)
            {
                foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if ((body.worldCenterOfMass - hit.point).sqrMagnitude <= radius * radius)
                        body.AddExplosionForce(explosionImpulse, hit.point - hit.normal * 0.24f, radius, 0.55f, ForceMode.Impulse);
                }
            }
            if (scorch)
                LeaveMark(hit);

            for (int i = 0; i < sparks.Length; i++)
            {
                ref Spark spark = ref sparks[i];
                Vector3 direction = Random.onUnitSphere;
                if (Vector3.Dot(direction, hit.normal) < 0)
                    direction = -direction;
                spark.Origin = hit.point + hit.normal * 0.06f;
                spark.Velocity = (direction + hit.normal * 0.20f) * Random.Range(2.5f, 9.0f);
                spark.Life = Random.Range(0.18f, 0.42f);
                spark.Trail.transform.position = spark.Origin;
                spark.Trail.Clear();
                spark.Trail.emitting = true;
                spark.Trail.enabled = true;
                properties.Clear();
                properties.SetColor(Tint, Color.Lerp(tint, new Color(1f, 0.62f, 0.19f), i % 4 == 0 ? 0.8f : 0f));
                properties.SetFloat(Strength, 0.85f);
                properties.SetFloat(Erosion, 0.04f);
                spark.Trail.SetPropertyBlock(properties);
            }
        }

        void LeaveMark(RaycastHit hit)
        {
            DecalProjector mark = marks[nextMark];
            nextMark = (nextMark + 1) % marks.Length;
            mark.enabled = false;
            mark.transform.SetParent(transform, false);
            Vector3 inward = -hit.normal;
            Vector3 up = Mathf.Abs(Vector3.Dot(inward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            var rotation = Quaternion.LookRotation(inward, up) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
            mark.transform.SetPositionAndRotation(hit.point + hit.normal * 0.025f, rotation);
            float diameter = Random.Range(0.91f, 1.18f);
            mark.size = new Vector3(diameter, diameter, 0.17f);
            mark.pivot = new Vector3(0, 0, 0.045f);
            if (hit.rigidbody != null)
                mark.transform.SetParent(hit.rigidbody.transform, true);
            mark.enabled = true;
        }

        public void Clear()
        {
            ClearTransient();
            foreach (var mark in marks)
            {
                mark.enabled = false;
                mark.transform.SetParent(transform, false);
            }
        }

        public void ClearTransient()
        {
            elapsed = waveDuration + 1;
            flash.enabled = shock.enabled = false;
            foreach (var spark in sparks)
            {
                spark.Trail.emitting = false;
                spark.Trail.Clear();
                spark.Trail.enabled = false;
            }
            view.transform.SetLocalPositionAndRotation(viewRest, viewRotation);
        }

        void LateUpdate()
        {
            if (elapsed > waveDuration + 0.12f)
                return;

            elapsed += Time.deltaTime;
            float age = elapsed / waveDuration;
            float decay = Mathf.Exp(-elapsed * 9f);
            flash.enabled = elapsed < 0.45f;
            flash.intensity = 11f * decay * (0.45f + Mathf.PerlinNoise(5f, elapsed * 57f) * 0.85f);
            shock.enabled = waveOn && age < 1;
            if (shock.enabled)
            {
                shock.GetPropertyBlock(properties);
                properties.SetFloat(Age, age);
                shock.SetPropertyBlock(properties);
            }

            float shake = shakeOn && elapsed < 0.38f ? shakeAmplitude * decay : 0;
            var offset = new Vector3(Mathf.Sin(elapsed * 105), Mathf.Cos(elapsed * 126), 0) * shake;
            view.transform.SetLocalPositionAndRotation(viewRest + viewRotation * offset, viewRotation);

            foreach (var spark in sparks)
            {
                spark.Trail.emitting = elapsed < spark.Life;
                spark.Trail.enabled = elapsed < spark.Life + spark.Trail.time;
                if (spark.Trail.emitting)
                    spark.Trail.transform.position = spark.Origin + spark.Velocity * elapsed + Vector3.down * (7f * elapsed * elapsed);
            }
        }
    }
}
