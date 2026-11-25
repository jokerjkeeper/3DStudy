using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StormLab.Benchmark
{
    public sealed partial class HouseBenchmark : MonoBehaviour
    {
        public Mesh whole;
        public Mesh[] pieces;
        public Material original;
        public Material material4K;
        public Material material2K;
        public Material ground;
        public UniversalRenderPipelineAsset forward;
        public UniversalRenderPipelineAsset forwardPlus;
        public UniversalRenderPipelineAsset resident;
        public Camera view;
#if UNITY_EDITOR
        public static Func<int, IEnumerator> VerifyFrame;
#endif

        const int LastGroup = 6;
        const int Width = 1920;
        UniversalRenderPipelineAsset instancingPipeline;
        Material instancingMaterial;
        const int Height = 1080;
        const int Warmup = 180;
        const int Samples = 240;
        const float Spacing = 10f;
        GameObject block;
        GameObject floor;
        RenderTexture target;
        string output;
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static readonly string[] TextureRoles = { "Base Color", "Normal", "Mask" };
        static readonly string[] TextureKeys = { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            var fixedIndex = Array.IndexOf(args, "-house-hold");
            var hold = fixedIndex >= 0 && fixedIndex + 2 < args.Length;
#if UNITY_EDITOR
            if (!hold && !UnityEditor.SessionState.GetBool("HouseBenchmark.Running", false))
            {
                StartPreview();
                yield break;
            }
#endif
            var outputIndex = Array.IndexOf(args, "-house-output");
            output = outputIndex >= 0 && outputIndex + 1 < args.Length
                ? args[outputIndex + 1] : Path.GetFullPath(Path.Combine("Builds", "HouseBenchmarkResults", DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(output);
            var vsync = QualitySettings.vSyncCount;
            var maxFps = Application.targetFrameRate;
            var mipLimit = QualitySettings.globalTextureMipmapLimit;
            var pipeline = QualitySettings.renderPipeline;
            var defaultPipeline = GraphicsSettings.defaultRenderPipeline;

            QualitySettings.vSyncCount = 0;
            QualitySettings.globalTextureMipmapLimit = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                { name = "House benchmark 1920x1080", antiAliasing = 1 };
            target.Create();
            // Measure/capture via the GPU camera target. In hold mode show the actual Game camera.
            view.targetTexture = hold ? null : target;

            try
            {
                if (hold)
                {
                    Configure(int.Parse(args[fixedIndex + 1], Invariant), int.Parse(args[fixedIndex + 2], Invariant));
                    yield break;
                }

                File.WriteAllText(Path.Combine(output, "environment.txt"),
                    $"Mode: {(Application.isEditor ? "Editor" : Debug.isDebugBuild ? "Development Player" : "Player")}\n" +
                    $"Unity: {Application.unityVersion}\nGPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})\n" +
                    $"Graphics memory report: {SystemInfo.graphicsMemorySize} MiB\nCPU: {SystemInfo.processorType}\n" +
                    $"OS: {SystemInfo.operatingSystem}\nResolution: {Width}x{Height} offscreen GPU target\n" +
                    $"Warmup: {Warmup}\nSample target: {Samples}\nTriangles per house: {whole.GetIndexCount(0)/3}\n" +
                    $"Parts: {string.Join(", ", pieces.Select(m => $"{m.name}:{m.GetIndexCount(0)/3}"))}\n");
                WriteTextureInfo();
                yield return CapturePair(false);
                yield return CapturePair(true);
                var csv = Path.Combine(output, "performance.csv");
                File.WriteAllText(csv, "group,houses,renderers,visible_static_frustum,static_batched,grd_initialized,samples,draw_calls,setpass,render_thread_ms,frame_ms,triangles,vertices,cpu_main_thread_ms\n");
                foreach (var count in new[] { 64, 256 })
                    for (var group = 1; group <= LastGroup; group++)
                        yield return Measure(group, count, csv);
#if UNITY_EDITOR
                if (VerifyFrame != null)
                    foreach (var group in new[] { 2, 3, 4, 5, LastGroup })
                    {
                        Configure(group, 64);
                        for (var i = 0; i < 12; i++) yield return null;
                        yield return VerifyFrame(group);
                    }
#endif
                File.WriteAllText(Path.Combine(output, "complete.txt"), DateTime.UtcNow.ToString("O", Invariant));
                Debug.Log("House Benchmark complete: " + output);
#if UNITY_EDITOR
                if (UnityEditor.SessionState.GetBool("HouseBenchmark.Running", false))
                    UnityEditor.EditorApplication.delayCall += UnityEditor.EditorApplication.ExitPlaymode;
#else
                Application.Quit();
#endif
            }
            finally
            {
                if (!hold)
                {
                    view.targetTexture = null;
                    target.Release();
                    Destroy(target);
                    QualitySettings.vSyncCount = vsync;
                    QualitySettings.globalTextureMipmapLimit = mipLimit;
                    Application.targetFrameRate = maxFps;
                    QualitySettings.renderPipeline = pipeline;
                    GraphicsSettings.defaultRenderPipeline = defaultPipeline;
                }
            }
        }

        void SetPipeline(UniversalRenderPipelineAsset asset)
        {
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
        }

        void ClearLayout()
        {
            if (block)
            {
                block.SetActive(false);
                Destroy(block);
            }
            if (floor)
            {
                floor.SetActive(false);
                Destroy(floor);
            }
            block = new GameObject("House layout");
        }

        MeshRenderer PutMesh(Transform parent, Mesh mesh, Material material)
        {
            var obj = new GameObject(mesh.name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = obj.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return r;
        }

        void Floor(float size)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Benchmark ground";
            floor.transform.position = Vector3.down * 0.02f;
            floor.transform.localScale = new Vector3(size, 1, size) / 10f;
            var renderer = floor.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ground;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Keep floor out of GRD in both Forward+ conditions.
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", ground.color);
            renderer.SetPropertyBlock(properties);
            Destroy(floor.GetComponent<Collider>());
        }

        void Configure(int group, int count)
        {
            ClearLayout();
            UniversalRenderPipelineAsset active = group == 5 ? resident : group == 4 ? forwardPlus : forward;
            Material houseMaterial = previewMode && preview2K ? material2K : material4K;
            if (group == LastGroup)
            {
                if (!instancingPipeline)
                {
                    instancingPipeline = Instantiate(forward);
                    instancingPipeline.name = "Forward without SRP Batcher";
                    instancingPipeline.useSRPBatcher = false;
                    instancingMaterial = new Material(material4K) { enableInstancing = true };
                }
                instancingMaterial.CopyPropertiesFromMaterial(houseMaterial);
                instancingMaterial.enableInstancing = true;
                houseMaterial = instancingMaterial;
                active = instancingPipeline;
            }
            SetPipeline(active);
            var side = Mathf.RoundToInt(Mathf.Sqrt(count));
            var extent = side * Spacing;
            for (var z = 0; z < side; z++)
            for (var x = 0; x < side; x++)
            {
                var house = new GameObject($"House {z * side + x + 1}");
                house.transform.SetParent(block.transform, false);
                house.transform.localPosition = new Vector3(x - (side - 1) * 0.5f, 0, z - (side - 1) * 0.5f) * Spacing;
                if (group == 1)
                    PutMesh(house.transform, whole, houseMaterial);
                else
                    foreach (var mesh in pieces)
                        PutMesh(house.transform, mesh, houseMaterial);
            }
            if (group == 3)
                StaticBatchingUtility.Combine(block);
            Floor(extent * 1.35f);
            view.orthographic = true;
            if (previewMode) view.ResetAspect();
            else view.aspect = (float)Width / Height;
            view.orthographicSize = extent * 0.50f + 4f;
            view.transform.position = new Vector3(extent * 0.31f, extent * 1.04f, -extent * 0.83f);
            view.transform.LookAt(new Vector3(0, 2.5f, 0));
            if (previewMode) SavePreviewCamera();
        }

        IEnumerator Measure(int group, int count, string csv)
        {
            Configure(group, count);
            for (var i = 0; i < Warmup; i++)
                yield return null;

            var renderers = block.GetComponentsInChildren<MeshRenderer>();
            var planes = GeometryUtility.CalculateFrustumPlanes(view);
            var frustum = renderers.Count(r => GeometryUtility.TestPlanesAABB(planes, r.bounds));
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            using var vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", 1);
            var drawValues = new List<double>(Samples);
            var setPassValues = new List<double>(Samples);
            var renderValues = new List<double>(Samples);
            var frameValues = new List<double>(Samples);
            var mainValues = new List<double>(Samples);
            var triangleValues = new List<double>(Samples);
            var vertexValues = new List<double>(Samples);
            var timing = new FrameTiming[1];
            ulong lastTimestamp = 0;
            for (var i = 0; i < Samples + 4; i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                if (i < 4)
                    continue;
                if (draws.Valid) drawValues.Add(draws.LastValue);
                if (setPass.Valid) setPassValues.Add(setPass.LastValue);
                if (triangles.Valid) triangleValues.Add(triangles.LastValue);
                if (vertices.Valid) vertexValues.Add(vertices.LastValue);
                frameValues.Add(Time.unscaledDeltaTime * 1000d);
                if (FrameTimingManager.GetLatestTimings(1, timing) != 0 &&
                    timing[0].frameStartTimestamp != lastTimestamp)
                {
                    lastTimestamp = timing[0].frameStartTimestamp;
                    if (timing[0].cpuRenderThreadFrameTime > 0)
                        renderValues.Add(timing[0].cpuRenderThreadFrameTime);
                    if (timing[0].cpuMainThreadFrameTime > 0)
                        mainValues.Add(timing[0].cpuMainThreadFrameTime);
                }
            }

            var row = string.Join(",", new[] { $"A{group}", count.ToString(Invariant),
                renderers.Length.ToString(Invariant), frustum.ToString(Invariant),
                renderers.Count(r => r.isPartOfStaticBatch).ToString(Invariant), ResidentDrawerState(), Samples.ToString(Invariant),
                Median(drawValues), Median(setPassValues), Median(renderValues), Median(frameValues),
                Median(triangleValues), Median(vertexValues), Median(mainValues) });
            File.AppendAllText(csv, row + "\n");
            Debug.Log($"House Benchmark {row}; CPU timing samples: {renderValues.Count}");
        }

        static readonly System.Reflection.PropertyInfo ResidentDrawer = typeof(GPUResidentDrawer).GetProperty("instance",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        static string ResidentDrawerState() =>
            ResidentDrawer == null ? "unknown" : (ResidentDrawer.GetValue(null) != null).ToString();

        static string Median(List<double> data)
        {
            if (data.Count < 120)
                return "NA";
            data.Sort();
            return ((data[(data.Count - 1) / 2] + data[data.Count / 2]) * 0.5).ToString("0.###", Invariant);
        }

        void WriteTextureInfo()
        {
            using var file = new StreamWriter(Path.Combine(output, "textures.csv"));
            file.WriteLine("variant,role,width,height,graphics_format,mips,unity_runtime_bytes,mip_payload_bytes");
            foreach (var pair in new[] { ("Original", original), ("4K", material4K), ("2K", material2K) })
            for (var i = 0; i < TextureKeys.Length; i++)
            {
                var tex = (Texture2D)pair.Item2.GetTexture(TextureKeys[i]);
                long bytes = 0;
                for (var mip = 0; mip < tex.mipmapCount; mip++)
                    bytes += (long)GraphicsFormatUtility.ComputeMipmapSize(
                        Mathf.Max(1, tex.width >> mip), Mathf.Max(1, tex.height >> mip), tex.graphicsFormat);
                file.WriteLine($"{pair.Item1},{TextureRoles[i]},{tex.width},{tex.height},{tex.graphicsFormat}," +
                               $"{tex.mipmapCount},{UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex)},{bytes}");
            }
        }

        IEnumerator CapturePair(bool close)
        {
            ClearLayout();
            SetPipeline(forward);
            Floor(32);
            var r = PutMesh(block.transform, whole, material4K);
            view.orthographic = false;
            view.fieldOfView = 51;
            view.aspect = (float)Width / Height;
            var focus = close ? new Vector3(-0.3f, 3.5f, -2.7f) : new Vector3(0, 3.4f, 0);
            var offset = close ? new Vector3(-3.3f, 2.3f, -6.6f) : new Vector3(-9.4f, 9.2f, -17.4f);
            view.transform.position = focus + offset * (Mathf.Tan(22.5f * Mathf.Deg2Rad) / Mathf.Tan(25.5f * Mathf.Deg2Rad));
            view.transform.LookAt(focus);

            foreach (var pair in new[] { ("4K", material4K), ("2K", material2K) })
            {
                r.sharedMaterial = pair.Item2;
                for (var i = 0; i < 30; i++)
                    yield return null;
                yield return new WaitForEndOfFrame();
                var previous = RenderTexture.active;
                var img = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                RenderTexture.active = target;
                img.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(output, $"texture-{(close ? "close" : "normal")}-{pair.Item1}.png"),
                    img.EncodeToPNG());
                Destroy(img);
            }
        }
    }
}
