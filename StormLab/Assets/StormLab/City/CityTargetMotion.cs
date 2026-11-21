using UnityEngine;

namespace StormLab
{
    public sealed class CityTargetMotion : MonoBehaviour
    {
        [SerializeField] Transform torso, head, leftShoulder, rightShoulder, leftElbow, rightElbow;

        static readonly int FaceForward = Shader.PropertyToID("_FaceForward");
        static readonly int FaceRight = Shader.PropertyToID("_FaceRight");
        const float MaxSdfSoftness = .18f;
        static readonly int FaceShadowStrength = Shader.PropertyToID("_FaceShadowStrength");
        static readonly int FaceShadowSoftness = Shader.PropertyToID("_FaceShadowSoftness");
        static readonly int FaceSdfDisabled = Shader.PropertyToID("_FaceSdfDisabled");
        static readonly int FaceView = Shader.PropertyToID("_FaceView");
        SkinnedMeshRenderer faceRenderer;
        Material faceMaterial;
        MaterialPropertyBlock faceProperties;
        int faceSlot;
        Vector3 headForward, headRight;
        bool showFaceMask;
        bool faceSdfEnabled = true;
        public float SdfStrength { get; private set; } = 1f;
        public float SdfSoftness { get; private set; }

        public void SetSdfTuning(float strength, float softness)
        {
            strength = Mathf.Clamp01(strength);
            softness = Mathf.Clamp01(softness);
            if (Mathf.Approximately(SdfStrength, strength) && Mathf.Approximately(SdfSoftness, softness))
                return;

            SdfStrength = strength;
            SdfSoftness = softness;
            ApplyFaceProperties();
        }

        public bool FaceSdfEnabled
        {
            get => faceSdfEnabled;
            set
            {
                if (faceSdfEnabled == value) return;
                faceSdfEnabled = value;
                ApplyFaceProperties();
            }
        }

        void Awake()
        {
            foreach (var surface in transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!surface.enabled || surface.sharedMesh == null) continue;
                var materials = surface.sharedMaterials;
                int slot = System.Array.FindIndex(materials, material =>
                    material != null && material.IsKeywordEnabled("_PERLICA_FACE"));
                if (slot < 0) continue;

                int headIndex = System.Array.IndexOf(surface.bones, head);
                var bindPoses = surface.sharedMesh.bindposes;
                if (headIndex < 0 || headIndex >= bindPoses.Length) continue;
                var bind = bindPoses[headIndex];
                faceRenderer = surface;
                faceMaterial = materials[slot];
                SdfStrength = faceMaterial.GetFloat(FaceShadowStrength);
                SdfSoftness = faceMaterial.GetFloat(FaceShadowSoftness) / MaxSdfSoftness;
                faceSlot = slot;
                faceProperties = new MaterialPropertyBlock();
                headForward = bind.MultiplyVector(faceMaterial.GetVector(FaceForward)).normalized;
                headRight = bind.MultiplyVector(faceMaterial.GetVector(FaceRight)).normalized;
                break;
            }
        }

        void Update()
        {
            if (faceRenderer != null && Input.GetKeyDown(KeyCode.F8))
                showFaceMask = !showFaceMask;
        }

        void OnGUI()
        {
            if (faceRenderer == null || !showFaceMask) return;
            GUI.Box(new Rect((Screen.width - 196f) * 0.5f, 14f, 196f, 27f), "FACE SDF MASK   [F8]");
        }

        public void Configure(Transform torsoBone, Transform headBone, Transform leftUpper, Transform rightUpper,
            Transform leftLower, Transform rightLower)
        {
            torso = torsoBone;
            head = headBone;
            leftShoulder = leftUpper;
            rightShoulder = rightUpper;
            leftElbow = leftLower;
            rightElbow = rightLower;
        }

        public void Sample(float time, float guard, float recoil)
        {
            float sway = Mathf.Sin(time * 2.5f);
            float breath = Mathf.Sin(time * 3.0f + 0.6f);
            torso.localRotation = Quaternion.Euler(2f * breath - 17f * recoil, 7f * sway + 12f * recoil,
                3f * sway - 6f * recoil);
            head.localRotation = Quaternion.Euler(-2f * breath + 9f * recoil, -5f * sway - 8f * recoil, -3f * sway);
            leftShoulder.localRotation = Quaternion.Euler(-8f * guard - 16f * recoil, 54f * guard,
                25f + 5f * breath - 30f * guard - 17f * recoil);
            rightShoulder.localRotation = Quaternion.Euler(-14f * guard - 16f * recoil, -53f * guard,
                -25f + 4f * sway + 37f * guard + 24f * recoil);
            leftElbow.localRotation = Quaternion.Euler(-34f * guard + 21f * recoil, 60f * guard,
                -19f * guard + 10f * recoil);
            rightElbow.localRotation = Quaternion.Euler(-31f * guard + 18f * recoil, -60f * guard,
                27f * guard - 12f * recoil);

            ApplyFaceProperties();
        }

        void ApplyFaceProperties()
        {
            if (faceRenderer == null) return;
            Transform surface = faceRenderer.transform;
            faceRenderer.GetPropertyBlock(faceProperties, faceSlot);
            faceProperties.SetVector(FaceForward, surface.InverseTransformDirection(head.TransformDirection(headForward)));
            faceProperties.SetVector(FaceRight, surface.InverseTransformDirection(head.TransformDirection(headRight)));
            faceProperties.SetFloat(FaceSdfDisabled, faceSdfEnabled ? 0f : 1f);
            faceProperties.SetFloat(FaceShadowStrength, SdfStrength);
            faceProperties.SetFloat(FaceShadowSoftness, SdfSoftness * MaxSdfSoftness);
            faceProperties.SetFloat(FaceView, showFaceMask ? 1f : faceMaterial.GetFloat(FaceView));
            faceRenderer.SetPropertyBlock(faceProperties, faceSlot);
        }
    }
}
