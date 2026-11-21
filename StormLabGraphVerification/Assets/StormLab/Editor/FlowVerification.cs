using System;
using System.Collections.Generic;
using StormLab;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

static class FlowVerification
{
    const string Scene = "Assets/StormLab/StormLab.unity";
    const string FlowPath = "Assets/StormLab/Charge/Storm Lab Workflow.asset";

    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(Scene);
        var sequence = UnityEngine.Object.FindAnyObjectByType<ChargeSequence>();
        var flow = AssetDatabase.LoadAssetAtPath<VfxFlow>(FlowPath);
        if (flow == null)
        {
            var timings = AssetDatabase.LoadAssetAtPath<ChargeSequenceProfile>("Assets/StormLab/Charge/Hand Charge Sequence.asset");
            flow = ScriptableObject.CreateInstance<VfxFlow>();
            flow.Seed(timings.charge, timings.travel, timings.impact);
            AssetDatabase.CreateAsset(flow, FlowPath);
        }
        var binding = new SerializedObject(sequence);
        binding.FindProperty("flow").objectReferenceValue = flow;
        binding.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var path = new List<VfxFlow.Node>();
        var test = UnityEngine.Object.Instantiate(flow);
        if (!test.TryGetPath(path, out _) || path.Count != 3 || test.TryConnect(path[2], path[0]))
            throw new InvalidOperationException("Initial path or cycle validation failed.");
        if (!test.TryConnect(path[0], path[2]) || !test.TryGetPath(path, out _) || path.Count != 2)
            throw new InvalidOperationException("Reconnecting an output failed.");
        test.Remove(path[1]);
        if (!test.TryGetPath(path, out _) || path.Count != 1)
            throw new InvalidOperationException("Deleting a connected node failed.");
        UnityEngine.Object.DestroyImmediate(test);
        Debug.Log("STORM_VFX_GRAPH_EDIT_PASS");

        var report = BuildPipeline.BuildPlayer(new[] { Scene }, "TestPlayer/Graph Test.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Workflow build failed.");
        Debug.Log("STORM_VFX_BUILD_PASS");
    }
}

