using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace StormLab.Benchmark
{
    static class HouseBenchmarkSetup
    {
        const string Root = "Assets/HouseBenchmark";
        const string Data = Root + "/Generated";
        const string Source = "Assets/model/house/";
        const string ScenePath = Root + "/HouseBenchmark.unity";

        internal static System.Diagnostics.Process Launch(string mode)
        {
            var executable = Path.GetFullPath("Builds/HouseBenchmark/HouseBenchmark.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException("Build the House Benchmark first.", executable);

            var results = Path.GetFullPath("Builds/HouseBenchmarkResults");
            Directory.CreateDirectory(results);
            var log = Path.Combine(results, $"player-hold-{mode.Split(' ')[1]}.log");
            return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
                Arguments = $"-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d12 " +
                            $"-logFile \"{log}\" -house-output \"{results}\" {mode}",
                UseShellExecute = false
            });
        }

        [MenuItem("Tools/House Benchmark/Run All in This Project")]
        public static void RunInEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting the benchmark.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                Generate();

            var previous = EditorSceneManager.playModeStartScene;
            SessionState.SetString("HouseBenchmark.PreviousStartScene", previous ? AssetDatabase.GetAssetPath(previous) : "");
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var mode = graphics.FindProperty("m_BrgStripping").intValue;
            SessionState.SetInt("HouseBenchmark.PreviousBRG", mode);
            SessionState.SetBool("HouseBenchmark.PreviousTiming", PlayerSettings.enableFrameTimingStats);
            SessionState.SetBool("HouseBenchmark.Running", true);
            SetStripping(graphics, (int)BatchRendererGroupStrippingMode.KeepAll);
            PlayerSettings.enableFrameTimingStats = true;
            EditorSceneManager.playModeStartScene = Load<SceneAsset>(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void WatchPlayMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("HouseBenchmark.Running", false))
                    return;
                SessionState.EraseBool("HouseBenchmark.Running");
                var previous = SessionState.GetString("HouseBenchmark.PreviousStartScene", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null :
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                PlayerSettings.enableFrameTimingStats = SessionState.GetBool("HouseBenchmark.PreviousTiming", false);
                var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
                SetStripping(graphics, SessionState.GetInt("HouseBenchmark.PreviousBRG", 0));
            };
        }

        static void SetStripping(SerializedObject graphics, int value)
        {
            graphics.Update();
            graphics.FindProperty("m_BrgStripping").intValue = value;
            graphics.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(graphics.targetObject);
        }

        [MenuItem("Tools/House Benchmark/Generate Scene")]
        public static void Generate()
        {
            Directory.CreateDirectory(Data);
            AssetDatabase.Refresh();
            var source = Load<GameObject>(Source + "source/SM_House.fbx");
            var sourceMaterial = Load<Material>(Source + "House URP.mat");
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 1)
                throw new InvalidOperationException("Expected one source MeshFilter.");
            var geometry = MakeMeshes(filters[0]);
            var originalScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.46f, .50f, .58f);
                RenderSettings.fog = false;

                var obj = new GameObject("House Benchmark");
                var benchmark = obj.AddComponent<HouseBenchmark>();
                benchmark.whole = geometry[0];
                benchmark.pieces = geometry.Skip(1).ToArray();
                benchmark.original = sourceMaterial;
                benchmark.material4K = TextureVariant(sourceMaterial, 4096);
                benchmark.material2K = TextureVariant(sourceMaterial, 2048);
                benchmark.forward = Pipeline("Forward", RenderingMode.Forward, false);
                benchmark.forwardPlus = Pipeline("ForwardPlus", RenderingMode.ForwardPlus, false);
                benchmark.resident = Pipeline("ForwardPlusGRD", RenderingMode.ForwardPlus, true);
                var battleRig = AssetDatabase.LoadAssetAtPath<GameObject>(Data + "/Cyberpunk City Battle Rig.prefab");
                if (battleRig)
                {
                    var battle = new SerializedObject(benchmark);
                    battle.FindProperty("cityBattleRig").objectReferenceValue = battleRig;
                    battle.FindProperty("cityBattlePipeline").objectReferenceValue =
                        Load<UniversalRenderPipelineAsset>("Assets/StormLab/Generated/StormURP.asset");
                    battle.ApplyModifiedPropertiesWithoutUndo();
                }
                benchmark.ground = Persist(new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = new Color(.26f, .28f, .29f) }, "Ground.mat");

                var cam = new GameObject("Benchmark Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.18f, .22f, .29f);
                cam.nearClipPlane = .25f;
                cam.farClipPlane = 700;
                cam.allowHDR = false;
                cam.useOcclusionCulling = false;
                benchmark.view = cam;
                var light = new GameObject("Directional light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, .94f, .85f);
                light.intensity = 1.3f;
                light.shadows = LightShadows.Hard;
                light.transform.rotation = Quaternion.Euler(48, -32, 0);
                RenderSettings.sun = light;

                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Unable to save " + ScenePath);

                Debug.Log($"House Benchmark generated {ScenePath}. Mesh {filters[0].sharedMesh.name}, " +
                          $"bounds {filters[0].sharedMesh.bounds}, triangles {geometry[0].GetIndexCount(0) / 3}. " +
                          string.Join(", ", benchmark.pieces.Select(m => $"{m.name} {m.GetIndexCount(0)/3}")));
            }
            finally
            {
                SceneManager.SetActiveScene(originalScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static UniversalRenderPipelineAsset Pipeline(string name, RenderingMode mode, bool grd)
        {
            var renderer = CreateRenderer(mode);
            renderer = Persist(renderer, name + "Renderer.asset");
            var asset = UniversalRenderPipelineAsset.Create(renderer);
            asset.useSRPBatcher = true;
            asset.supportsDynamicBatching = false;
            asset.msaaSampleCount = 1;
            asset.shadowDistance = 450;
            asset.shadowCascadeCount = 1;
            // Set serialized value without touching the active project pipeline.
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_GPUResidentDrawerMode").intValue = (int)(grd ?
                GPUResidentDrawerMode.InstancedDrawing : GPUResidentDrawerMode.Disabled);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return Persist(asset, name + ".asset");
        }

        static UniversalRendererData CreateRenderer(RenderingMode mode)
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.renderingMode = mode;
            return renderer;
        }

        static Material TextureVariant(Material source, int size)
        {
            var variant = new Material(source);
            var keys = new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };
            for (var i = 0; i < keys.Length; i++)
            {
                var path = AssetDatabase.GetAssetPath(source.GetTexture(keys[i]));
                var target = $"{Data}/{size}_{Path.GetFileName(path)}";
                if (!File.Exists(target) && !AssetDatabase.CopyAsset(path, target))
                    throw new IOException("Unable to copy texture " + path);
                var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                var settings = sourceImporter.GetDefaultPlatformTextureSettings();
                settings.name = "Standalone";
                settings.maxTextureSize = size;
                settings.overridden = true;
                settings.crunchedCompression = false;
                settings.format = sourceImporter.GetAutomaticFormat("Standalone");
                importer.SetPlatformTextureSettings(settings);
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = false;
                importer.isReadable = false;
                importer.SaveAndReimport();
                var texture = Load<Texture2D>(target);
                if (texture.width != size || texture.height != size)
                    throw new InvalidOperationException($"{target} imported at {texture.width}x{texture.height}.");
                variant.SetTexture(keys[i], texture);
                if (i == 2)
                    variant.SetTexture("_OcclusionMap", texture);
                Debug.Log($"House Benchmark {keys[i]}: original {((Texture2D)source.GetTexture(keys[i])).width}, " +
                          $"test {texture.width}x{texture.height}, {texture.graphicsFormat}");
            }
            return Persist(variant, "House-" + size + ".mat");
        }

        static T Persist<T>(T asset, string filename) where T : UnityEngine.Object
        {
            var path = Data + "/" + filename;
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);
            asset.name = Path.GetFileNameWithoutExtension(filename);
            if (!previous)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, previous);
            EditorUtility.SetDirty(previous);
            AssetDatabase.SaveAssetIfDirty(previous);
            UnityEngine.Object.DestroyImmediate(asset);
            return previous;
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new FileNotFoundException(path);
            return asset;
        }

        static Mesh[] MakeMeshes(MeshFilter filter)
        {
            var source = filter.sharedMesh;
            var matrix = filter.transform.localToWorldMatrix;
            var sourceVertices = source.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var bounds = new Bounds(sourceVertices[0], Vector3.zero);
            foreach (var p in sourceVertices) bounds.Encapsulate(p);
            var scale = 8f / Mathf.Max(bounds.size.x, bounds.size.z);
            var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var vertices = sourceVertices.Select(v => (v - origin) * scale).ToArray();
            var normalMatrix = matrix.inverse.transpose;
            var normals = source.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            var tangents = source.tangents.Select(t =>
            {
                var v = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                return new Vector4(v.x, v.y, v.z, t.w * Mathf.Sign(matrix.determinant));
            }).ToArray();
            var sourceUv = new List<Vector4>[8];
            for (var i = 0; i < sourceUv.Length; i++)
            {
                sourceUv[i] = new List<Vector4>();
                source.GetUVs(i, sourceUv[i]);
            }
            var colors = source.colors;
            var indices = Enumerable.Range(0, source.subMeshCount).SelectMany(sub => source.GetIndices(sub)).ToArray();
            if (matrix.determinant < 0)
                for (var i = 0; i < indices.Length; i += 3)
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);

            var parent = new int[vertices.Length];
            var canonical = new Dictionary<Vector3Int, int>();
            for (var i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 10000);
                if (!canonical.TryGetValue(key, out var index))
                    canonical.Add(key, index = i);
                parent[i] = index;
            }
            int Find(int index)
            {
                while (parent[index] != index)
                {
                    parent[index] = parent[parent[index]];
                    index = parent[index];
                }
                return index;
            }
            for (var i = 0; i < indices.Length; i += 3)
            {
                var a = Find(indices[i]);
                parent[Find(indices[i + 1])] = a;
                parent[Find(indices[i + 2])] = a;
            }

            var islands = new Dictionary<int, List<int>>();
            for (var i = 0; i < indices.Length; i += 3)
            {
                var key = Find(indices[i]);
                if (!islands.TryGetValue(key, out var island))
                    islands.Add(key, island = new List<int>());
                island.Add(i);
            }
            var buckets = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var houseHeight = bounds.size.y * scale;
            foreach (var island in islands.Values)
            {
                var box = new Bounds(vertices[indices[island[0]]], Vector3.zero);
                foreach (var offset in island)
                    for (var j = 0; j < 3; j++)
                        box.Encapsulate(vertices[indices[offset + j]]);
                var thinSide = Mathf.Min(box.size.x, box.size.z);
                var wideSide = Mathf.Max(box.size.x, box.size.z);
                var window = island.Count == 203 && wideSide < .9f && thinSide < .25f;
                var trim = Mathf.Min(box.size.y, thinSide) < .24f &&
                           Mathf.Max(box.size.y, wideSide) > .25f;
                foreach (var offset in island)
                {
                    var a = vertices[indices[offset]];
                    var b = vertices[indices[offset + 1]];
                    var c = vertices[indices[offset + 2]];
                    var roof = box.min.y > houseHeight * .47f ||
                               ((a.y + b.y + c.y) / 3f > houseHeight * .53f &&
                                Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y) > .35f);
                    var bucket = window ? 3 : roof ? 0 : trim ? 2 : 1;
                    buckets[bucket].Add(indices[offset]);
                    buckets[bucket].Add(indices[offset + 1]);
                    buckets[bucket].Add(indices[offset + 2]);
                }
            }

            Mesh Make(IEnumerable<int> stream, string name)
            {
                var mapping = new Dictionary<int, int>();
                var used = new List<int>();
                var compact = new List<int>();
                foreach (var i in stream)
                {
                    if (!mapping.TryGetValue(i, out var index))
                    {
                        mapping.Add(i, index = used.Count);
                        used.Add(i);
                    }
                    compact.Add(index);
                }
                if (compact.Count == 0)
                    throw new InvalidOperationException($"Empty mesh partition {name}.");
                var mesh = new Mesh { name = name, vertices = used.Select(i => vertices[i]).ToArray() };
                if (normals.Length > 0) mesh.normals = used.Select(i => normals[i]).ToArray();
                if (tangents.Length > 0) mesh.tangents = used.Select(i => tangents[i]).ToArray();
                if (colors.Length > 0) mesh.colors = used.Select(i => colors[i]).ToArray();
                for (var channel = 0; channel < sourceUv.Length; channel++)
                {
                    var uv = sourceUv[channel];
                    if (uv.Count > 0)
                        mesh.SetUVs(channel, used.Select(i => uv[i]).ToList());
                }
                mesh.SetTriangles(compact, 0, true);
                return Persist(mesh, name + ".asset");
            }
            var names = new[] { "Roof", "Walls and structure", "Timber and trim", "Window assemblies" };
            var result = new[] { Make(indices, "House Combined") }.Concat(
                buckets.Select((b, i) => Make(b, names[i]))).ToArray();
            if (result.Skip(1).Sum(m => (long)m.GetIndexCount(0)) != result[0].GetIndexCount(0))
                throw new InvalidOperationException("Mesh split changed the triangle count.");
            return result;
        }
    }
}
