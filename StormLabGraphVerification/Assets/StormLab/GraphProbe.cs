using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

sealed class GraphProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (System.Environment.GetCommandLineArgs().Contains("-stormGraphSmoke"))
            new GameObject("Verification").AddComponent<GraphProbe>();
    }

    IEnumerator Start()
    {
        var range = FindAnyObjectByType<StormLab.StormControls>();
        var arc = GameObject.Find("Main arc").GetComponent<LineRenderer>();
        var wave = GameObject.Find("Shockwave screen sampling mesh").GetComponent<Renderer>();
        yield return new WaitForSeconds(1.1f);
        var arcGraph = arc.sharedMaterial;
        var waveGraph = wave.sharedMaterial;
        Expect(arcGraph.shader.name == "StormLab/Graphs/Arc Erosion" && arcGraph.renderQueue == 3010, "Arc Graph default");
        Expect(waveGraph.shader.name == "StormLab/Graphs/Shockwave Refraction" && waveGraph.renderQueue == 2990, "Wave Graph default");
        Expect(range.FireTestShot(), "Shot ray");
        yield return new WaitForSeconds(0.10f);
        Expect(arc.enabled && wave.enabled && arc.positionCount == 20, "Impact active");
        Capture("graph-default");
        arcGraph.SetFloat("_ErosionBias", 0.3f);
        Capture("graph-high-erosion");
        arcGraph.SetFloat("_ErosionBias", 0);
        waveGraph.SetFloat("_RefractionGain", 0);
        Capture("graph-no-distortion");
        waveGraph.SetFloat("_RefractionGain", 1);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        range.GetType().GetMethod("SetGraphArc", flags).Invoke(range, new object[] { false });
        Expect(arc.sharedMaterial.shader.name == "StormLab/Arc" && wave.sharedMaterial == waveGraph, "Independent arc switch");
        range.GetType().GetMethod("SetGraphWave", flags).Invoke(range, new object[] { false });
        Expect(wave.sharedMaterial.shader.name == "StormLab/Shockwave", "Wave switch");
        Capture("hlsl");
        range.GetType().GetMethod("SetGraphArc", flags).Invoke(range, new object[] { true });
        range.GetType().GetMethod("SetGraphWave", flags).Invoke(range, new object[] { true });
        Expect(arc.sharedMaterial == arcGraph && wave.sharedMaterial == waveGraph, "Switch back");
        Capture("graph-restored");
        Debug.Log("STORM_GRAPH_RUNTIME_PASS");
        Application.Quit(0);
    }

    static void Expect(bool valid, string name)
    {
        if (!valid) throw new System.Exception("Graph test failed: " + name);
    }

    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1280, 800, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderPipeline.SubmitRenderRequest(Camera.main, new RenderPipeline.StandardRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), name + ".png"), image.EncodeToPNG());
        Destroy(image);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(target);
    }
}

