using System.Collections;
using System.IO;
using System.Linq;
using StormLab;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

sealed class BattleProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (System.Environment.GetCommandLineArgs().Contains("-stormBattleSmoke"))
            new GameObject("Battle verification").AddComponent<BattleProbe>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.8f);
        var controls = FindAnyObjectByType<StormControls>();
        var seq = controls.Sequence;
        var palm = seq.Hand.Palm;
        var wrist = palm.parent.parent;
        Check(SceneManager.GetActiveScene().name == "LanternQuay" && wrist.name == "Right hand", "right-hand binding");
        Vector3 rest = palm.position;
        string hitName = null;
        seq.Arrived += hit => hitName = hit.collider.name;
        Capture("battle-idle");
        Check(controls.PreviewWorkflow(seq.Flow), "one-click battle flow");
        yield return new WaitForSeconds(0.85f);
        Check(seq.Current == ChargeSequence.Phase.Charge && Vector3.Distance(rest, palm.position) > 0.25f &&
            seq.Hand.GetComponentInChildren<ParticleSystem>().particleCount > 0, "animated charge and particles");
        var lines = seq.Hand.GetComponentsInChildren<LineRenderer>();
        Check(lines.Any(l => l.enabled && Vector3.Distance(l.transform.position, palm.position) < 0.15f), "charge follows hand");
        Capture("battle-charge");
        yield return new WaitUntil(() => seq.Current == ChargeSequence.Phase.Travel);
        var bolt = GameObject.Find("Traveling discharge");
        Check(Vector3.Distance(bolt.transform.position, palm.position) < 0.35f, "launch uses current palm position");
        yield return new WaitForSeconds(0.11f);
        Capture("battle-release");
        yield return new WaitUntil(() => seq.Current == ChargeSequence.Phase.Impact);
        Check(hitName == "Sentinel hit surface", "workflow hit the battle target");
        var targetVisual = GameObject.Find("Sentinel visual").transform;
        var targetRest = targetVisual.localPosition;
        yield return new WaitForSeconds(0.10f);
        Check(Vector3.Distance(targetVisual.localPosition, targetRest) > 0.04f, "sentinel reaction");
        Capture("battle-impact");
        yield return new WaitUntil(() => !seq.Busy);

        var initial = seq.Flow.nodes[0].duration;
        seq.Flow.nodes[0].duration = 0.31f;
        Check(controls.PreviewWorkflow(seq.Flow, seq.Flow.nodes[0].id), "replay updated stage");
        yield return new WaitForSeconds(0.13f);
        Check(seq.Current == ChargeSequence.Phase.Charge, "short stage still playing");
        yield return new WaitForSeconds(0.3f);
        Check(!seq.Busy, "new duration took effect");
        seq.Flow.nodes[0].duration = initial;
        controls.ResetRange();
        Check(controls.FireTestShot(), "manual test shot");
        Debug.Log("STORM_BATTLE_RUNTIME_PASS");
        Application.Quit(0);
    }

    static void Check(bool valid, string label)
    {
        if (valid) return;
        Debug.LogError("Battle verification failed: " + label);
        Application.Quit(1);
        throw new System.InvalidOperationException(label);
    }

    static void Capture(string name)
    {
        var rt = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderPipeline.SubmitRenderRequest(Camera.main, new RenderPipeline.StandardRequest { destination = rt });
        RenderTexture.active = rt;
        var frame = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
        frame.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), name + ".png"), frame.EncodeToPNG());
        Destroy(frame);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
    }
}

