using System.Collections.Generic;
using UnityEngine;

namespace NoggiSdfToolkit
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Noggi Face SDF Basis")]
    public sealed class FaceSdfBasis : MonoBehaviour
    {
        static readonly int SdfDisabledId = Shader.PropertyToID("_FaceSdfDisabled");
        static readonly int SdfStrengthId = Shader.PropertyToID("_FaceSdfStrength");
        static readonly int RightId = Shader.PropertyToID("_HeadRightWS");
        static readonly int ForwardId = Shader.PropertyToID("_HeadForwardWS");

        [Tooltip("Renderer containing the face material.")]
        [SerializeField] Renderer target;
        [Min(0), Tooltip("Zero-based face material slot on the target.")]
        [SerializeField] int materialSlot;
        [Tooltip("A head-aligned transform. Its local -Z points out of the face and +X points to screen right in a frontal view.")]
        [SerializeField] Transform headBasis;
        [SerializeField] bool sdfEnabled = true;

        [SerializeField, Range(0f, 1f)] float strength = 1f;

        public float Strength
        {
            get => strength;
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Approximately(strength, next)) return;
                strength = next;
                if (isActiveAndEnabled) Apply();
            }
        }

        public bool SdfEnabled
        {
            get => sdfEnabled;
            set
            {
                if (sdfEnabled == value) return;
                sdfEnabled = value;
                if (isActiveAndEnabled) Apply();
            }
        }

        readonly List<Material> materials = new();
        MaterialPropertyBlock block;
        Renderer appliedTarget;
        int appliedSlot;

        public void Bind(Renderer renderer, int slot, Transform basis)
        {
            target = renderer;
            materialSlot = slot;
            headBasis = basis;
            Apply();
        }

        void OnEnable() => Apply();
        void LateUpdate() => Apply();
        void OnDisable() => Clear();

        void Apply()
        {
            if (appliedTarget && (appliedTarget != target || appliedSlot != materialSlot))
                Clear();
            if (!HasSlot(target, materialSlot))
                return;

            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block, materialSlot);
            block.SetFloat(SdfDisabledId, sdfEnabled ? 0f : 1f);
            block.SetFloat(SdfStrengthId, strength);
            Vector3 right = headBasis ? headBasis.right : Vector3.zero;
            Vector3 forward = headBasis ? -headBasis.forward : Vector3.zero;
            float useBasis = headBasis ? 1f : 0f;
            block.SetVector(RightId, new Vector4(right.x, right.y, right.z, useBasis));
            block.SetVector(ForwardId, new Vector4(forward.x, forward.y, forward.z, useBasis));
            target.SetPropertyBlock(block, materialSlot);
            appliedTarget = target;
            appliedSlot = materialSlot;
        }

        bool HasSlot(Renderer renderer, int slot)
        {
            if (!renderer || slot < 0)
                return false;
            renderer.GetSharedMaterials(materials);
            return slot < materials.Count;
        }

        void Clear()
        {
            if (HasSlot(appliedTarget, appliedSlot))
            {
                appliedTarget.GetPropertyBlock(block, appliedSlot);
                block.SetFloat(SdfDisabledId, 0f);
                block.SetFloat(SdfStrengthId, 1f);
                block.SetVector(RightId, Vector4.zero);
                block.SetVector(ForwardId, Vector4.zero);
                appliedTarget.SetPropertyBlock(block, appliedSlot);
            }
            appliedTarget = null;
        }
    }
}
