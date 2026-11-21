using System;
using StormLab;
using StormLab.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

static class BattleVerification
{
    public static void Run()
    {
        var window = ScriptableObject.CreateInstance<VfxFlowWindow>();
        var flow = ScriptableObject.CreateInstance<VfxFlow>();
        try
        {
            AssetDatabase.CreateAsset(flow, "Assets/BattleTimeProbe.asset");
            flow.Seed();
            var controls = new SerializedObject(window);
            controls.FindProperty("flow").objectReferenceValue = flow;
            controls.ApplyModifiedPropertiesWithoutUndo();
            var first = flow.nodes[0];
            if (window.TryApplyDuration(first, "NaN") || window.TryApplyDuration(first, "0.01") ||
                !window.TryApplyDuration(first, "1.72") || Mathf.Abs(first.duration - 1.72f) > 0.001f)
                throw new InvalidOperationException("Stage time did not commit or invalid input was accepted.");
            AssetDatabase.ImportAsset("Assets/BattleTimeProbe.asset", ImportAssetOptions.ForceSynchronousImport);
            var loaded = AssetDatabase.LoadAssetAtPath<VfxFlow>("Assets/BattleTimeProbe.asset");
            if (Mathf.Abs(loaded.nodes[0].duration - 1.72f) > 0.001f)
                throw new InvalidOperationException("Saved time was not reloaded.");
            Debug.Log("STORM_STAGE_TIME_PASS");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
            AssetDatabase.DeleteAsset("Assets/BattleTimeProbe.asset");
        }

        var report = BuildPipeline.BuildPlayer(new[] { VfxFlowWindow.BattleScenePath, VfxFlowWindow.RangeScenePath },
            "TestPlayer/Graph Test.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Battle build failed.");
        Debug.Log("STORM_BATTLE_BUILD_PASS");
    }
}

