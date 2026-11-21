using BodyCurrent;
using UnityEngine;

namespace StormLab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(20)]
    public sealed class GroveBodyCurrent : MonoBehaviour
    {
        [SerializeField] ChargeSequence sequence;
        [SerializeField] GameObject arcs;
        [SerializeField] bool showCurrent = true;

        const float ShotPulseDuration = 0.52f;
        float shotPulseUntil = float.NegativeInfinity;
        BodyArc[] ribbons;

        public bool ShowCurrent
        {
            get => showCurrent;
            set => showCurrent = value;
        }

        public bool IsVisible => isActiveAndEnabled && showCurrent && sequence != null &&
            !sequence.IsSoloPreview && (sequence.Current is ChargeSequence.Phase.Charge or
                ChargeSequence.Phase.Preview or ChargeSequence.Phase.Travel ||
                (sequence.Current == ChargeSequence.Phase.Idle && Time.time < shotPulseUntil));

        public void PulseShot() => shotPulseUntil = Time.time + ShotPulseDuration;

        public void ClearShotPulse() => shotPulseUntil = float.NegativeInfinity;

        public void Configure(ChargeSequence flow, GameObject ribbons)
        {
            sequence = flow;
            arcs = ribbons;
            arcs.SetActive(false);
        }

        void Awake()
        {
            if (arcs != null) ribbons = arcs.GetComponentsInChildren<BodyArc>(true);
        }

        void LateUpdate()
        {
            if (ribbons == null) return;
            bool visible = IsVisible;
            if (arcs.activeSelf != visible) arcs.SetActive(visible);
            if (!visible) return;

            // Sample after GrovePose has placed the sleeve and wrist for this frame.
            foreach (var ribbon in ribbons)
                ribbon.Sample(Time.time);
        }

        void OnDisable()
        {
            ClearShotPulse();
            if (arcs != null) arcs.SetActive(false);
        }
    }
}
