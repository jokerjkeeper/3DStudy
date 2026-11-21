using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

static class GraphTestBuild
{
    public static void Build()
    {
        ChargeAuthoring.UpdateCulling();
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/StormLab/StormLab.unity" }, "TestPlayer/Graph Test.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Graph test build failed");
        Debug.Log("STORM_GRAPH_BUILD_PASS");
    }
}
