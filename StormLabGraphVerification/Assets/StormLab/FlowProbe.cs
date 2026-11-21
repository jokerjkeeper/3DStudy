using System.Collections;
using System.IO;
using System.Linq;
using StormLab;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

sealed class FlowProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (System.Environment.GetCommandLineArgs().Contains("-stormVfxSmoke"))
            new GameObject("VFX verification").AddComponent<FlowProbe>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.7f);
        var controls = FindAnyObjectByType<StormControls>();
        var seq = controls.Sequence;
        var flow = seq.Flow;
        var particles = seq.Hand.GetComponentInChildren<ParticleSystem>();
        var arcs = seq.Hand.GetComponentsInChildren<LineRenderer>();
        var ring = GameObject.Find("Workflow halo 1").GetComponent<LineRenderer>();
        var trail = GameObject.Find("Traveling discharge").GetComponent<TrailRenderer>();
        var flash = GameObject.Find("Impact point light").GetComponent<Light>();
        int normalHits = 0, previewHits = 0;
        seq.Arrived += _ => normalHits++;
        seq.PreviewHit += _ => previewHits++;
        var charge = flow.nodes[0];
        var release = flow.nodes[1];
        var hit = flow.nodes[2];

        Check(controls.PreviewWorkflow(flow, charge.id), "start isolated stage");
        yield return new WaitForSeconds(0.42f);
        Check(seq.ActiveNodeId == charge.id && ring.enabled && particles.particleCount > 3 &&
            arcs.All(l => l.enabled && l.sharedMaterial.shader.name.EndsWith("/Arc Erosion")), "three parallel layers");
        Capture("vfx-workflow-charge");
        controls.StopWorkflow();

        Check(controls.PreviewWorkflow(flow, charge.id, VfxFlow.Effect.Ring), "start solo layer");
        yield return null;
        Check(ring.enabled && arcs.All(l => !l.enabled) && particles.particleCount == 0, "solo ring");
        controls.StopWorkflow();

        Check(controls.PreviewWorkflow(flow, hit.id), "start impact preview");
        yield return null;
        Check(flash.enabled && ring.enabled && previewHits == 1 && normalHits == 0, "isolated hit is visual");
        controls.StopWorkflow();
        Check(!flash.enabled && !ring.enabled && !trail.enabled, "stop clears visual feedback");

        release.layers[0].delay = 0.10f;
        release.layers[0].duration = 0.12f;
        var releaseRing = new VfxFlow.Layer { effect = VfxFlow.Effect.Ring, duration = 0.26f };
        release.layers.Add(releaseRing);
        Check(controls.PreviewWorkflow(flow), "start full flow");
        yield return new WaitUntil(() => seq.ActiveNodeId == release.id);
        Check(ring.enabled && !trail.emitting && normalHits == 0, "delay before discharge");
        yield return new WaitUntil(() => trail.emitting);
        Check(ring.enabled && seq.ActiveNodeId == release.id, "overlapping release layers");
        yield return new WaitUntil(() => !trail.emitting);
        Check(seq.ActiveNodeId == release.id && normalHits == 0, "short layer ends before stage");
        yield return new WaitUntil(() => seq.ActiveNodeId == hit.id);
        Check(normalHits == 1 && flash.enabled && ring.enabled, "connected impact stage");
        yield return new WaitUntil(() => !seq.Busy);
        Check(normalHits == 1 && !ring.enabled && !trail.enabled && particles.particleCount == 0, "completion clears active layers");

        controls.ResetRange();
        Check(controls.FireTestShot(), "direct range entry point");
        Debug.Log("STORM_VFX_RUNTIME_PASS");
        Application.Quit(0);
    }

    static void Check(bool ok, string stage)
    {
        if (!ok)
        {
            Debug.LogError("VFX verification failed: " + stage);
            Application.Quit(1);
            throw new System.InvalidOperationException(stage);
        }
    }

    static void Capture(string name)
    {
        var rt = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderPipeline.SubmitRenderRequest(Camera.main, new RenderPipeline.StandardRequest { destination = rt });
        RenderTexture.active = rt;
        var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), name + ".png"), image.EncodeToPNG());
        Destroy(image);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
    }
}

