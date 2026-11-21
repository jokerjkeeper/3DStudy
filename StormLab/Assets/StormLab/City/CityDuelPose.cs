using System;
using UnityEngine;

namespace StormLab
{
    public sealed class CityDuelPose : BattleActorPose
    {
        [Serializable]
        public struct Arm
        {
            public Transform upper, elbow, wrist;
            [SerializeField] Quaternion upperRest, elbowRest;

            public Arm(Transform upper, Transform elbow, Transform wrist)
            {
                this.upper = upper;
                this.elbow = elbow;
                this.wrist = wrist;
                upperRest = upper.localRotation;
                elbowRest = elbow.localRotation;
            }

            public void Point(Vector3 upperDirection, Vector3 forearmDirection)
            {
                upper.localRotation = upperRest;
                elbow.localRotation = elbowRest;
                upper.rotation = Quaternion.FromToRotation(elbow.position - upper.position, upperDirection) * upper.rotation;
                elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position, forearmDirection) * elbow.rotation;
            }
        }

        [SerializeField] ChargeSequence sequence;
        [SerializeField] Arm castingArm, spareArm;
        [SerializeField] Transform caster, palmAnchor, palmEffect, target, aim;
        [SerializeField] Collider receiver;

        [SerializeField] CityTargetMotion targetMotion;

        const float ReactionDuration = 0.72f;
        const float ShotDuration = 0.52f;
        float reactionAge = ReactionDuration;
        float shotAge = ShotDuration;
        float guard;
        Vector3 shotPoint;
        Quaternion casterRest;

        void Awake() => casterRest = caster.localRotation;

        public void Configure(ChargeSequence sequence, Arm castingArm, Arm spareArm, Transform caster,
            Transform palmAnchor, Transform palmEffect, Transform target, Transform aim, Collider receiver)
        {
            this.sequence = sequence;
            this.castingArm = castingArm;
            this.spareArm = spareArm;
            this.caster = caster;
            this.palmAnchor = palmAnchor;
            this.palmEffect = palmEffect;
            this.target = target;
            this.aim = aim;
            this.receiver = receiver;
            casterRest = caster.localRotation;
            ResetPose();
        }

        public void ConfigureTargetMotion(CityTargetMotion motion) => targetMotion = motion;

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

        public override void PrepareShot(Vector3 point)
        {
            shotAge = 0f;
            shotPoint = point;
            Apply(1f, 1f, Time.time, shotPoint);
        }

        public override void React(RaycastHit hit)
        {
            if (hit.collider == receiver) reactionAge = 0f;
        }

        public override void ResetPose()
        {
            reactionAge = ReactionDuration;
            shotAge = ShotDuration;
            guard = 0f;
            target.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Apply(0f, 0f, 0f, aim.position);
            if (targetMotion != null) targetMotion.Sample(0f, 0f, 0f);
        }

        void Sample(ChargeSequence.Phase phase, float progress)
        {
            if (phase != ChargeSequence.Phase.Preview) shotAge = ShotDuration;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2.6f));
            switch (phase)
            {
                case ChargeSequence.Phase.Charge:
                case ChargeSequence.Phase.Preview:
                    Apply(t, 0f, Time.time, aim.position);
                    break;
                case ChargeSequence.Phase.Travel:
                    Apply(1f, 1f - progress * 0.25f, Time.time, aim.position);
                    break;
                case ChargeSequence.Phase.Impact:
                    Apply(1f - t, 1f - t, Time.time, aim.position);
                    break;
            }
        }

        void LateUpdate()
        {
            float time = Time.time;
            if (!sequence.Busy && shotAge < ShotDuration)
            {
                shotAge = Mathf.Min(ShotDuration, shotAge + Time.deltaTime);
                float settle = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.09f, ShotDuration, shotAge));
                Apply(settle, settle, time, shotPoint);
            }
            else if (sequence.Current == ChargeSequence.Phase.Idle)
                Apply(0f, 0f, time, aim.position);

            reactionAge = Mathf.Min(ReactionDuration, reactionAge + Time.deltaTime);
            float recoil = Mathf.SmoothStep(0f, 1f, reactionAge / 0.09f)
                * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.09f, ReactionDuration, reactionAge));
            bool preparing = sequence.Current is ChargeSequence.Phase.Charge or ChargeSequence.Phase.Preview or ChargeSequence.Phase.Travel;
            guard = Mathf.MoveTowards(guard, preparing ? 1f : 0f, Time.deltaTime * 3.8f);
            Vector3 away = Vector3.ProjectOnPlane(target.parent.position - caster.position, Vector3.up).normalized;
            target.SetLocalPositionAndRotation(away * (0.26f * recoil),
                Quaternion.AngleAxis(-10f * recoil, Vector3.Cross(away, Vector3.up)));
            if (targetMotion != null) targetMotion.Sample(time, guard * (1f - recoil), recoil);
        }

        void Apply(float lift, float release, float time, Vector3 point)
        {
            float breath = Mathf.Sin(time * 1.7f + 0.45f);
            caster.localRotation = casterRest * Quaternion.Euler(0f, 9f * release, 4f * lift - 8f * release);
            Vector3 toward = Vector3.ProjectOnPlane(aim.position - caster.position, Vector3.up).normalized;
            Vector3 front = caster.forward;
            Vector3 down = Vector3.down;
            Vector3 releaseDirection = (point - castingArm.upper.position).normalized;
            Vector3 upperCharge = toward * 0.78f + front * 0.35f + down * 0.53f;
            Vector3 forearmCharge = toward * 0.46f + front * 0.49f - down * 0.94f;
            Vector3 upper = Vector3.Slerp((down + toward * 0.36f).normalized, upperCharge.normalized, lift);
            Vector3 forearm = Vector3.Slerp((down + toward * 0.34f + front * (0.14f + 0.025f * breath)).normalized,
                forearmCharge.normalized, lift);
            castingArm.Point(Vector3.Slerp(upper, releaseDirection, release),
                Vector3.Slerp(forearm, releaseDirection, release));
            spareArm.Point(down - toward * 0.35f,
                down - toward * 0.40f + front * (0.14f + 0.02f * breath + 0.31f * lift));
            palmEffect.SetPositionAndRotation(palmAnchor.position, caster.rotation);
        }
    }
}
