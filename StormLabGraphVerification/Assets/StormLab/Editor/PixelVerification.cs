using System;
using StormLab.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

static class PixelVerification
{
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new[] { VfxFlowWindow.BattleScenePath, VfxFlowWindow.RangeScenePath },
            "TestPlayer/Graph Test.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Pixel battle build failed.");
        Debug.Log("STORM_PIXELS_BUILD_PASS");
    }
}

