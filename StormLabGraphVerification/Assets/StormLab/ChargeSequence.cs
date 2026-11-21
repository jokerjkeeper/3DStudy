using System;
using System.Collections.Generic;
using UnityEngine;

namespace StormLab
{
    public sealed class ChargeSequence : MonoBehaviour
    {
        public enum Phase { Idle, Preview, Charge, Travel, Impact }

        [SerializeField] ChargeSequenceProfile timings;
        [SerializeField] VfxFlow flow;
        [SerializeField] HandCharge hand;
        [SerializeField] TrailRenderer boltTrail;
        [SerializeField] Renderer boltHead;

        readonly List<VfxFlow.Node> path = new();
        RaycastHit target;
        Vector3 launchPoint;
        float elapsed, duration;
        int index;
        bool hitEmitted;
        FlowRings rings;
        VfxFlow.Effect? soloEffect;

        public event Action<Phase, float> PoseSampled;
        public event Action<RaycastHit> Arrived;
        public event Action<RaycastHit> PreviewHit;
        public Phase Current { get; private set; }
        public string ActiveNodeId => Busy ? path[index].id : null;
        public bool IsNodePreview { get; private set; }
        public float Progress => Current == Phase.Preview ? 1f : Busy ? Mathf.Clamp01(elapsed / duration) : 0f;
        public HandCharge Hand => hand;
        public VfxFlow Flow => flow;
        public bool Busy => Current is Phase.Charge or Phase.Travel or Phase.Impact;

        public void Configure(ChargeSequenceProfile profile, HandCharge source, TrailRenderer trail, Renderer head)
        {
            timings = profile;
            hand = source;
            boltTrail = trail;
            boltHead = head;
        }

        void Start()
        {
            rings = new FlowRings(transform, boltTrail.sharedMaterial);
            Cancel();
        }

        public void TogglePreview()
        {
            if (Busy) return;
            if (Current == Phase.Preview) Cancel();
            else Current = Phase.Preview;
        }

        public bool Play(RaycastHit hit) => Play(hit, flow, null);

        public bool Play(RaycastHit hit, VfxFlow selected, string isolatedNode, VfxFlow.Effect? solo = null)
        {
            if (Busy) return false;
            path.Clear();
            if (!string.IsNullOrEmpty(isolatedNode))
            {
                var node = selected != null ? selected.Find(isolatedNode) : null;
                if (node == null) return false;
                path.Add(node);
            }
            else if (selected != null)
            {
                if (!selected.TryGetPath(path, out var error))
                {
                    Debug.LogWarning(error, selected);
                    return false;
                }
            }
            else
            {
                path.Add(new VfxFlow.Node(VfxFlow.Stage.Charge, Vector2.zero, timings.charge, VfxFlow.Effect.Arc, VfxFlow.Effect.Particles));
                path.Add(new VfxFlow.Node(VfxFlow.Stage.Release, Vector2.zero, timings.travel, VfxFlow.Effect.Bolt));
                path.Add(new VfxFlow.Node(VfxFlow.Stage.Impact, Vector2.zero, timings.impact, VfxFlow.Effect.Hit));
            }

            IsNodePreview = isolatedNode != null;
            soloEffect = solo;
            target = hit;
            launchPoint = hand.Palm.position;
            index = 0;
            EnterStage();
            EvaluateStage();
            return true;
        }

        public void Cancel()
        {
            hand.StopCharge();
            rings?.Hide();
            HideBolt();
            Current = Phase.Idle;
            IsNodePreview = false;
        }

        void Update()
        {
            if (Current == Phase.Preview)
            {
                PoseSampled?.Invoke(Current, 1);
                hand.RenderCharge(1);
                return;
            }
            if (!Busy) return;

            float remaining = Time.deltaTime;
            while (Busy)
            {
                float step = duration - elapsed;
                bool finished = remaining >= step;
                elapsed = finished ? duration : elapsed + remaining;
                remaining -= step;
                EvaluateStage();
                if (!finished) break;
                if (++index == path.Count)
                {
                    Cancel();
                    break;
                }
                EnterStage();
                if (remaining <= 0)
                {
                    EvaluateStage();
                    break;
                }
            }
        }

        void EnterStage()
        {
            var node = path[index];
            duration = float.IsFinite(node.duration) ? Mathf.Max(0.05f, node.duration) : 0.05f;
            elapsed = 0;
            hitEmitted = false;

            Current = node.stage switch
            {
                VfxFlow.Stage.Charge => Phase.Charge,
                VfxFlow.Stage.Release => Phase.Travel,
                _ => Phase.Impact
            };
            if (node.stage == VfxFlow.Stage.Release)
            {
                PoseSampled?.Invoke(Current, 0);
                launchPoint = hand.Palm.position;
            }
        }

        void EvaluateStage()
        {
            var node = path[index];
            float progress = Progress;
            PoseSampled?.Invoke(Current, progress);
            Vector3 center = node.stage switch
            {
                VfxFlow.Stage.Charge => hand.Palm.position,
                VfxFlow.Stage.Release => Vector3.Lerp(launchPoint, target.point, progress),
                _ => target.point + target.normal * 0.24f
            };

            bool showArcs = false, showParticles = false, showRing = false, showBolt = false;
            foreach (var layer in node.layers)
            {
                if (soloEffect.HasValue && layer.effect != soloEffect.Value) continue;
                float start = Mathf.Max(0, layer.delay);
                float end = layer.duration > 0 ? Mathf.Min(duration, start + layer.duration) : duration;
                if (start >= end || elapsed < start) continue;

                if (layer.effect == VfxFlow.Effect.Hit && !hitEmitted && target.collider != null)
                {
                    hitEmitted = true;
                    if (IsNodePreview) PreviewHit?.Invoke(target);
                    else Arrived?.Invoke(target);
                }
                if (elapsed > end) continue;
                switch (layer.effect)
                {
                    case VfxFlow.Effect.Arc: showArcs = true; break;
                    case VfxFlow.Effect.Particles: showParticles = true; break;
                    case VfxFlow.Effect.Ring:
                        showRing = true;
                        rings.Draw(center, Mathf.InverseLerp(start, end, elapsed), node.stage == VfxFlow.Stage.Impact);
                        break;
                    case VfxFlow.Effect.Bolt:
                        showBolt = true;
                        DrawBolt(node.stage == VfxFlow.Stage.Release ?
                            Vector3.Lerp(launchPoint, target.point, Mathf.InverseLerp(start, end, elapsed)) : center);
                        break;
                }
            }

            hand.RenderLayers(center, node.stage == VfxFlow.Stage.Charge ? progress : 1f, showArcs, showParticles);
            if (!showRing) rings.Hide();
            if (!showBolt && boltTrail.emitting)
            {
                boltHead.enabled = false;
                boltTrail.emitting = false;
            }
        }

        void DrawBolt(Vector3 point)
        {
            boltTrail.transform.position = point;
            if (boltTrail.emitting) return;
            boltTrail.Clear();
            boltTrail.enabled = boltTrail.emitting = boltHead.enabled = true;
        }

        void HideBolt()
        {
            boltTrail.emitting = boltTrail.enabled = boltHead.enabled = false;
            boltTrail.Clear();
        }
    }
}
