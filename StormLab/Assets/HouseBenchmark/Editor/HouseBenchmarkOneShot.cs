using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StormLab.Benchmark
{
    static class HouseBenchmarkOneShot
    {
        const string Request = "Temp/HouseBenchmark.run";

        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            if (!File.Exists(Request)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Request)) return;
                File.Delete(Request);
                try { HouseBenchmarkSetup.RunInEditor(); }
                catch (Exception failure)
                {
                    File.WriteAllText("Temp/HouseBenchmark.error.txt", failure.ToString());
                    Debug.LogException(failure);
                }
            };
        }
    }
}
