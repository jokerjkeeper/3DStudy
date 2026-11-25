using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;

namespace StormLab.Benchmark
{
    static class HouseBenchmarkFrameEvents
    {
        static readonly int[] Groups = { 2, 3, 4, 5 };
        static readonly Type Utility = typeof(EditorApplication).Assembly
            .GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility");
        static readonly string Result = Path.GetFullPath("Builds/HouseBenchmarkResults");
        static Process player;
        static HashSet<int> priorTargets;
        static int oldTarget, groupIndex, remote;
        static double deadline, attachTime;
        static string captureFault;
        static Array captured;
        static int eventIndex;
        static double detailDeadline;
        static readonly BindingFlags Member = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [MenuItem("Tools/House Benchmark/Export Frame Debugger Events")]
        public static void Export()
        {
            if (player != null)
                throw new InvalidOperationException("Frame capture is already running.");
            if (UnityEngine.FrameDebugger.enabled)
                throw new InvalidOperationException("Disable the active Frame Debugger first.");
            oldTarget = ProfilerDriver.connectedProfiler;
            groupIndex = 0;
            File.WriteAllText(Path.Combine(Result, "frame-debugger-status.txt"), "");
            StartGroup();
            EditorApplication.update += OnUpdate;
        }

        static void StartGroup()
        {
            priorTargets = ProfilerDriver.GetAvailableProfilers().ToHashSet();
            player = HouseBenchmarkSetup.Launch($"-house-hold {Groups[groupIndex]} 64");
            deadline = EditorApplication.timeSinceStartup + 25;
            attachTime = EditorApplication.timeSinceStartup + 2;
            captureFault = null;
            remote = 0;
            captured = null;
        }

        static void OnUpdate()
        {
            try
            {
                var now = EditorApplication.timeSinceStartup;
                if (player.HasExited)
                    throw new InvalidOperationException($"Hold player exited: {player.ExitCode}.");
                if (now > deadline)
                    throw new TimeoutException(remote == 0
                        ? "No unambiguous Development Player profiler target."
                        : "No remote Frame Debugger events.");
                if (now < attachTime) return;
                if (remote == 0)
                {
                    var matches = ProfilerDriver.GetAvailableProfilers().Where(id => !priorTargets.Contains(id)).ToArray();
                    if (matches.Length != 1) return;
                    remote = matches[0];
                    ProfilerDriver.connectedProfiler = remote;
                    Utility.GetMethod("SetEnabled").Invoke(null, new object[] { true, remote });
                    return;
                }
                if (captured == null)
                {
                    var events = (Array)Utility.GetMethod("GetFrameEvents").Invoke(null, null);
                    if (events == null || events.Length == 0) return;
                    captured = events;
                    var path = Path.Combine(Result, $"frame-debugger-A{Groups[groupIndex]}-64.tsv");
                    using (var file = new StreamWriter(path))
                    {
                        file.WriteLine("index\tevent\tpass_or_object");
                        for (var i = 0; i < captured.Length; i++)
                        {
                            var name = Utility.GetMethod("GetFrameEventInfoName").Invoke(null, new object[] { i });
                            file.WriteLine($"{i}\t{TypeAt(i)}\t{name}");
                        }
                    }
                    eventIndex = Enumerable.Range(0, captured.Length).FirstOrDefault(i =>
                        TypeAt(i) is "HybridBatch" or "StaticBatch" or "InstancedMesh" or "SRPBatch");
                    Utility.GetProperty("limit").SetValue(null, eventIndex + 1);
                    detailDeadline = now + 2;
                    return;
                }

                var data = Activator.CreateInstance(Utility.Assembly.GetType(
                    "UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData"), true);
                var received = (bool)Utility.GetMethod("GetFrameEventData").Invoke(null, new[] { (object)eventIndex, data });
                if (received)
                {
                    object Field(string key) => data.GetType().GetField("m_" + key, Member)?.GetValue(data);
                    var row = $"{eventIndex}\t{TypeAt(eventIndex)}\t{Field("InstanceCount")}\t{Field("DrawCallCount")}\t{Field("PassName")}";
                    File.WriteAllText(Path.Combine(Result, $"frame-debugger-A{Groups[groupIndex]}-64-detail.tsv"),
                        "index\tevent\tinstances\tdraw_calls\tpass\n" + row + "\n");
                }
                else if (now < detailDeadline)
                    return;
                Finish();
            }
            catch (Exception error)
            {
                captureFault = error.GetBaseException().Message;
                Finish();
            }
        }

        static string TypeAt(int index)
        {
            var item = captured.GetValue(index);
            return item.GetType().GetField("m_Type", Member).GetValue(item).ToString();
        }

        static void Finish()
        {
            if (remote != 0)
            {
                Utility.GetMethod("SetEnabled").Invoke(null, new object[] { false, remote });
                ProfilerDriver.connectedProfiler = oldTarget;
            }
            if (!player.HasExited)
                player.Kill();
            player.Dispose();
            player = null;
            File.AppendAllText(Path.Combine(Result, "frame-debugger-status.txt"),
                $"A{Groups[groupIndex]} remote {remote}: {captureFault ?? $"{captured.Length} events"}\n");
            if (++groupIndex < Groups.Length)
                StartGroup();
            else
                EditorApplication.update -= OnUpdate;
        }
    }
}
