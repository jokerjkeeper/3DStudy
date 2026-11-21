using System;
using UnityEngine;

namespace StormLab
{
    public sealed class BattlePose : MonoBehaviour
    {
        [Serializable]
        public struct CastFrame
        {
            public Sprite sprite;
            [Tooltip("Casting hand center in the source frame, measured from the top left in pixels.")]
            public Vector2 handPixel;

            public CastFrame(Sprite sprite, Vector2 handPixel)
            {
                this.sprite = sprite;
                this.handPixel = handPixel;
            }
        }

        [SerializeField] ChargeSequence sequence;
        [SerializeField] SpriteRenderer caster;
        [SerializeField] Transform handSocket;
        [SerializeField] SpriteRenderer sentinel;
        [SerializeField] Collider sentinelReceiver;
        [SerializeField] CastFrame[] idle;
        [SerializeField] CastFrame[] charge;
        [SerializeField] CastFrame[] release;
        [SerializeField] CastFrame recovery;
        [SerializeField] Sprite[] sentinelIdle;
        [SerializeField] Sprite[] sentinelHit;

        const float HitDuration = 0.50f;
        Vector3 sentinelRest;
        float hitAge = HitDuration;
        bool hasFrames;

        public void Configure(ChargeSequence controller, SpriteRenderer casterSprite, Transform hand,
            SpriteRenderer sentinelSprite, Collider receiver, CastFrame[] idleFrames, CastFrame[] chargeFrames,
            CastFrame[] releaseFrames, CastFrame recoverFrame, Sprite[] restingTarget, Sprite[] hitTarget)
        {
            sequence = controller;
            caster = casterSprite;
            handSocket = hand;
            sentinel = sentinelSprite;
            sentinelReceiver = receiver;
            idle = idleFrames;
            charge = chargeFrames;
            release = releaseFrames;
            recovery = recoverFrame;
            sentinelIdle = restingTarget;
            sentinelHit = hitTarget;
            caster.sprite = null;
            ApplyCastFrame(idle[0]);
        }

        void Awake()
        {
            hasFrames = sequence != null && caster != null && handSocket != null && sentinel != null &&
                idle is { Length: > 0 } && charge is { Length: >= 4 } && release is { Length: >= 2 } &&
                sentinelIdle is { Length: > 0 } && sentinelHit is { Length: >= 3 };
            if (hasFrames) sentinelRest = sentinel.transform.localPosition;
        }

        void OnEnable()
        {
            if (!hasFrames) return;
            sequence.PoseSampled += Sample;
            sequence.Arrived += React;
            sequence.PreviewHit += React;
        }

        void OnDisable()
        {
            if (sequence == null) return;
            sequence.PoseSampled -= Sample;
            sequence.Arrived -= React;
            sequence.PreviewHit -= React;
        }

        public void ResetPose()
        {
            if (!hasFrames) return;
            hitAge = HitDuration;
            sentinel.transform.localPosition = sentinelRest;
            sentinel.sprite = sentinelIdle[0];
            ApplyCastFrame(idle[0]);
        }

        public void React(RaycastHit hit)
        {
            if (!hasFrames || hit.collider != sentinelReceiver) return;
            hitAge = 0;
            sentinel.sprite = sentinelHit[0];
        }

        CastFrame IdleFrame() => idle[Mathf.FloorToInt(Time.time * 2.5f) % idle.Length];

        void Sample(ChargeSequence.Phase phase, float progress)
        {
            var frame = phase switch
            {
                ChargeSequence.Phase.Charge or ChargeSequence.Phase.Preview =>
                    charge[progress < 0.18f ? 0 : progress < 0.42f ? 1 : 2 + Mathf.FloorToInt(Time.time * 7f) % 2],
                ChargeSequence.Phase.Travel => release[progress < 0.54f ? 0 : 1],
                ChargeSequence.Phase.Impact when progress < 0.37f => recovery,
                _ => IdleFrame()
            };
            ApplyCastFrame(frame);
        }

        void ApplyCastFrame(CastFrame frame)
        {
            if (caster.sprite == frame.sprite) return;
            caster.sprite = frame.sprite;
            var pixel = new Vector2(frame.handPixel.x + 0.5f, frame.sprite.rect.height - frame.handPixel.y - 0.5f);
            Vector2 hand = (pixel - frame.sprite.pivot) / frame.sprite.pixelsPerUnit;
            handSocket.localPosition = new Vector3(hand.x, hand.y, -0.12f);
        }

        void LateUpdate()
        {
            if (!hasFrames) return;
            if (sequence.Current == ChargeSequence.Phase.Idle)
                ApplyCastFrame(IdleFrame());

            if (hitAge < HitDuration)
            {
                hitAge = Mathf.Min(HitDuration, hitAge + Time.deltaTime);
                float progress = hitAge / HitDuration;
                sentinel.sprite = sentinelHit[progress < 0.18f ? 0 : progress < 0.58f ? 1 : 2];
                float recoil = Mathf.Sin(progress * Mathf.PI) * (1f - progress);
                var shift = sentinel.transform.parent.InverseTransformDirection(-sentinel.transform.right) * (0.46f * recoil);
                sentinel.transform.localPosition = sentinelRest + shift;
            }
            else
            {
                sentinel.sprite = sentinelIdle[Mathf.FloorToInt(Time.time * 2f) % sentinelIdle.Length];
                sentinel.transform.localPosition = sentinelRest;
            }
        }
    }
}
