using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using StormLab;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

sealed class ChargeProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (System.Environment.GetCommandLineArgs().Contains("-stormChargeSmoke"))
            new GameObject("Charge verification").AddComponent<ChargeProbe>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.7f);
        var seq = FindAnyObjectByType<ChargeSequence>();
        var controls = FindAnyObjectByType<StormControls>();
        var play = controls.GetType().GetMethod("PlayChargeSequence", BindingFlags.Instance | BindingFlags.NonPublic);
        var particles = seq.Hand.GetComponentInChildren<ParticleSystem>();
        var arcs = seq.Hand.GetComponentsInChildren<LineRenderer>();
        var trail = GameObject.Find("Traveling discharge").GetComponent<TrailRenderer>();
        var flash = GameObject.Find("Impact point light").GetComponent<Light>();
        int hits = 0;
        seq.Arrived += _ => hits++;

        seq.TogglePreview();
        yield return new WaitForSeconds(0.6f);
        Check(seq.Current == ChargeSequence.Phase.Preview && particles.particleCount >= 15, $"Preview particles phase={seq.Current} count={particles.particleCount} playing={particles.isPlaying} sim={particles.time} emission={particles.emission.enabled}");
        Check(arcs.Length == 10 && arcs.All(l => l.enabled && l.sharedMaterial.shader.name.EndsWith("/Arc Erosion")), "Graph ribbons");
        var data = new ParticleSystem.Particle[particles.main.maxParticles];
        int count = particles.GetParticles(data);
        Check(data.Take(count).Count(p => Vector3.Dot(p.position, p.velocity) < 0) > count * 0.8f, "Inward spark velocities");
        Capture("charge-both");

        seq.Hand.ShowArcs = false;
        yield return null;
        yield return null;
        Check(arcs.All(l => !l.enabled) && particles.particleCount > 0, "Sparks isolated");
        Capture("charge-sparks");

        seq.Hand.ShowSparks = false;
        seq.Hand.ShowArcs = true;
        yield return null;
        yield return null;
        Check(arcs.All(l => l.enabled) && particles.particleCount == 0, "Arcs isolated");
        Capture("charge-arcs");
        seq.Hand.ShowSparks = true;
        Check(!flash.enabled && hits == 0, "Isolated preview has no impact");
        play.Invoke(controls, null);
        Check(seq.Current == ChargeSequence.Phase.Charge, "Chain starts");
        yield return new WaitUntil(() => seq.Current == ChargeSequence.Phase.Travel);
        Check(trail.emitting && arcs.All(l => !l.enabled) && particles.particleCount == 0 && !flash.enabled, "Travel after charge");
        yield return new WaitForSeconds(0.10f);
        Capture("charge-travel");
        yield return new WaitUntil(() => seq.Current == ChargeSequence.Phase.Impact);
        Check(!trail.emitting && flash.enabled && hits == 1, "Impact only on arrival");
        yield return new WaitForSeconds(0.095f);
        Capture("charge-impact");
        yield return new WaitUntil(() => seq.Current == ChargeSequence.Phase.Idle);
        Check(hits == 1 && !trail.enabled, "Chain cleans up");

        play.Invoke(controls, null);
        yield return new WaitForSeconds(0.25f);
        controls.ResetRange();
        yield return new WaitForSeconds(0.4f);
        Check(seq.Current == ChargeSequence.Phase.Idle && particles.particleCount == 0 &&
            arcs.All(l => !l.enabled) && hits == 1, "Reset cancels chain");
        Check(controls.FireTestShot() && flash.enabled, "Direct shot still works");
        Debug.Log("STORM_CHARGE_RUNTIME_PASS");
        Application.Quit(0);
    }

    static void Check(bool ok, string stage)
    {
        if (!ok)
        {
            Debug.LogError("Charge verification failed: " + stage);
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
