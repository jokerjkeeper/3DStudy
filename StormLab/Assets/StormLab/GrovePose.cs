using UnityEngine;

namespace StormLab
{
    public sealed class GrovePose : BattleActorPose
    {
        [SerializeField] ChargeSequence sequence;
        [SerializeField] Transform casterBody, sleeve, palm, sentinelBody;
        [SerializeField] Collider receiver;

        static readonly Vector3 Shoulder = new(0.36f, 1.18f, 0.04f);
        static readonly Vector3 Rest = new(0.55f, 0.77f, 0.19f);
        static readonly Vector3 Charged = new(0.76f, 1.37f, 0.35f);
        static readonly Vector3 Extended = new(0.98f, 1.25f, 0.30f);
        float recoilAge = 1f;

        public void Configure(ChargeSequence flow, Transform body, Transform arm, Transform hand,
            Transform target, Collider hitSurface)
        {
            sequence = flow;
            casterBody = body;
            sleeve = arm;
            palm = hand;
            sentinelBody = target;
            receiver = hitSurface;
            ResetPose();
        }

        void OnEnable()
        {
            sequence.PoseSampled += Sample;
            sequence.Arrived += React;
            sequence.PreviewHit += React;
        }

        void OnDisable()
        {
            sequence.PoseSampled -= Sample;
            sequence.Arrived -= React;
            sequence.PreviewHit -= React;
        }

        public override void React(RaycastHit hit)
        {
            if (hit.collider == receiver) recoilAge = 0f;
        }

        public override void ResetPose()
        {
            recoilAge = 1f;
            sentinelBody.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Apply(Rest, 0f, 0f);
        }

        void Sample(ChargeSequence.Phase phase, float progress)
        {
            float lift = Mathf.SmoothStep(0f, 1f, progress * 2.3f);
            switch (phase)
            {
                case ChargeSequence.Phase.Charge:
                case ChargeSequence.Phase.Preview:
                    var charged = Charged + Vector3.up * (Mathf.Sin(Time.time * 6f) * 0.022f);
                    Apply(Vector3.Lerp(Rest, charged, lift), 7f * lift, 0.025f * lift);
                    break;
                case ChargeSequence.Phase.Travel:
                    Apply(Vector3.Lerp(Extended, Charged, progress * 0.35f), -10f, -0.035f);
                    break;
                case ChargeSequence.Phase.Impact:
                    Apply(Vector3.Lerp(Charged, Rest, lift), Mathf.Lerp(-9f, 0f, lift), 0f);
                    break;
            }
        }

        void LateUpdate()
        {
            if (sequence.Current == ChargeSequence.Phase.Idle)
                Apply(Rest, Mathf.Sin(Time.time * 2.4f) * 1.2f, Mathf.Sin(Time.time * 2.4f) * 0.025f);

            recoilAge = Mathf.Min(1f, recoilAge + Time.deltaTime / 0.52f);
            float recoil = Mathf.Sin(recoilAge * Mathf.PI) * (1f - recoilAge) * 2f;
            float idle = Mathf.Sin(Time.time * 2.1f) * 0.025f;
            sentinelBody.SetLocalPositionAndRotation(new Vector3(-0.24f * recoil, idle, -0.11f * recoil),
                Quaternion.Euler(-9f * recoil, -12f * recoil, 15f * recoil));
        }

        void Apply(Vector3 hand, float lean, float bob)
        {
            casterBody.SetLocalPositionAndRotation(Vector3.up * bob, Quaternion.Euler(0f, 0f, lean));
            palm.localPosition = hand;
            sleeve.localPosition = (Shoulder + hand) * 0.5f;
            sleeve.localRotation = Quaternion.FromToRotation(Vector3.up, hand - Shoulder);
            sleeve.localScale = new Vector3(0.29f, Vector3.Distance(Shoulder, hand) + 0.19f, 0.31f);
        }
    }
}
