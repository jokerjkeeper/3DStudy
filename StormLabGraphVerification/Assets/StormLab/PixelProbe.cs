using System.Collections;
using System.IO;
using System.Linq;
using StormLab;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

sealed class PixelProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (System.Environment.GetCommandLineArgs().Contains("-stormPixelSmoke"))
            new GameObject("Pixel verification").AddComponent<PixelProbe>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.9f);
        var controls = FindAnyObjectByType<StormControls>();
        var seq = controls.Sequence;
        var caster = GameObject.Find("Wayfarer pixel actor").GetComponent<SpriteRenderer>();
        var sentinel = GameObject.Find("Sentinel pixel actor").GetComponent<SpriteRenderer>();
        var socket = GameObject.Find("Right hand pixel socket").transform;
        var trail = GameObject.Find("Traveling discharge").GetComponent<TrailRenderer>();
        Check(caster.sprite.texture.filterMode == FilterMode.Point && sentinel.sprite.texture.filterMode == FilterMode.Point,
            "pixel filtering");
        Check(seq.Hand.Palm.IsChildOf(socket), "socket parenting");
        string hitName = null;
        seq.Arrived += hit => hitName = hit.collider.name;
        Vector3 idle = socket.position;
        Capture("quay-pixels-idle");
        Check(controls.PreviewWorkflow(seq.Flow), "play existing node flow");
        yield return new WaitUntil(() => caster.sprite.name.Contains("Channel"));
        yield return new WaitForSeconds(0.14f);
        Check(seq.Current == ChargeSequence.Phase.Charge && Vector3.Distance(idle, socket.position) > 0.52f,
            "cast frames move hand");
        Check(seq.Hand.GetComponentsInChildren<LineRenderer>().Any(line => line.enabled &&
            Vector3.Distance(line.transform.position, socket.position) < 0.07f), "charge lines follow exact pixel socket");
        Check(seq.Hand.GetComponentInChildren<ParticleSystem>().particleCount > 0, "charge particles");
        Capture("quay-pixels-charge");

        var channel = socket.position;
        yield return new WaitUntil(() => seq.Current == ChargeSequence.Phase.Travel);
        Check(caster.sprite.name == "Wayfarer Release" && Vector3.Distance(socket.position, channel) > 0.19f, "release frame");
        Check(trail.emitting && Vector3.Distance(trail.transform.position, socket.position) < 0.59f, "bolt starts at new hand pose");
        yield return new WaitForSeconds(0.08f);
        Capture("quay-pixels-release");

        yield return new WaitUntil(() => seq.Current == ChargeSequence.Phase.Impact);
        Check(hitName == "Sentinel hit surface" && sentinel.sprite.name == "Sentinel Impact", "target impact frame");
        yield return new WaitForSeconds(0.105f);
        Check(sentinel.sprite.name == "Sentinel Recoil", "target recoil frame");
        Capture("quay-pixels-impact");
        yield return new WaitUntil(() => !seq.Busy);
        Check(caster.sprite.name.Contains("Idle") && sentinel.sprite.name.Contains("Idle"), "actors return to idle");
        controls.ResetRange();
        Check(controls.FireTestShot() && sentinel.sprite.name == "Sentinel Impact", "direct test shot also animates target");
        Debug.Log("STORM_PIXELS_RUNTIME_PASS");
        Application.Quit(0);
    }

    static void Check(bool valid, string stage)
    {
        if (valid) return;
        Debug.LogError("Pixel verification failed: " + stage);
        Application.Quit(1);
        throw new System.InvalidOperationException(stage);
    }

    static void Capture(string name)
    {
        var rt = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderPipeline.SubmitRenderRequest(Camera.main, new RenderPipeline.StandardRequest { destination = rt });
        RenderTexture.active = rt;
        var screen = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        screen.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
        screen.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), name + ".png"), screen.EncodeToPNG());
        Destroy(screen);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
    }
}

