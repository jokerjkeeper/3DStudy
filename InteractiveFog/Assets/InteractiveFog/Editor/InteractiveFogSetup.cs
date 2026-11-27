using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InteractiveFog.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace InteractiveFog.EditorTools
{
    /// <summary>
    /// One-shot project setup: creates a URP asset with the InteractiveFog renderer feature,
    /// assigns it to Graphics/Quality settings, and builds the demo scene.
    /// Runs automatically the first time the project opens; also available from the Tools menu.
    /// </summary>
    [InitializeOnLoad]
    static class InteractiveFogSetup
    {
        const string Root = "Assets/InteractiveFog";
        const string SettingsDir = Root + "/Settings";
        const string DemoDir = Root + "/Demo";
        const string ScenePath = DemoDir + "/InteractiveFogDemo.unity";
        const string PipelinePath = SettingsDir + "/FogURP.asset";
        const string RendererPath = SettingsDir + "/FogURP_Renderer.asset";

        static InteractiveFogSetup()
        {
            if (File.Exists(ScenePath) || SessionState.GetBool("InteractiveFogSetupTried", false)) return;
            SessionState.SetBool("InteractiveFogSetupTried", true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                BuildAll();
            };
        }

        [MenuItem("Tools/Interactive Fog/Setup URP + Build Demo Scene")]
        public static void BuildAll()
        {
            EnsurePipeline();
            BuildScene();
        }

        [MenuItem("Tools/Interactive Fog/Setup URP Only")]
        public static void SetupPipelineOnly() => EnsurePipeline();

        // ---------------------------------------------------------------- pipeline

        static void EnsurePipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            AssetDatabase.Refresh();

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                TryAssignDefaultPostProcessData(rendererData);
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            if (!rendererData.rendererFeatures.Exists(f => f is InteractiveFogFeature))
            {
                var feature = ScriptableObject.CreateInstance<InteractiveFogFeature>();
                feature.name = "InteractiveFog";
                feature.shader = Shader.Find("Hidden/InteractiveFog/Raymarch");
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
                AssetDatabase.SaveAssets();

                // keep URP's feature map in sync (used by the renderer data inspector)
                var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mapField?.GetValue(rendererData) is List<long> map &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                    map.Add(localId);

                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            Debug.Log("[InteractiveFog] URP asset ready: " + PipelinePath);
        }

        static void TryAssignDefaultPostProcessData(UniversalRendererData data)
        {
            if (data.postProcessData != null) return;
            var m = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null) data.postProcessData = m.Invoke(null, null) as PostProcessData;
        }

        // ---------------------------------------------------------------- scene

        static float Height(float x, float z) =>
            1.2f * Mathf.Sin(x * 0.21f + 1.3f) * Mathf.Cos(z * 0.17f) +
            0.6f * Mathf.Sin(x * 0.43f + z * 0.37f) +
            0.3f * Mathf.Cos(x * 0.9f - z * 0.7f);

        static Texture2D CheckerTexture()
        {
            string path = DemoDir + "/Ground_Checker.png";
            if (!File.Exists(path))
            {
                const int S = 256, half = 128;
                var t = new Texture2D(S, S, TextureFormat.RGB24, false);
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float b = (((x / half) + (y / half)) & 1) == 0 ? 0.17f : 0.22f;
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.05f + Mathf.PerlinNoise(x * 0.21f + 9f, y * 0.21f) * 0.03f;
                    int gx = x % half, gy = y % half;
                    float g = Mathf.Min(Mathf.Min(gx, half - 1 - gx), Mathf.Min(gy, half - 1 - gy));
                    float v = (b + n) * (g < 3 ? 0.45f : 1f);
                    t.SetPixel(x, y, new Color(v, v, v * 1.02f));
                }
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material LitMaterial(string name, Color color)
        {
            string path = DemoDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Terrain BuildTerrain()
        {
            const int res = 257;
            const float size = 60f, height = 6f;
            var td = new TerrainData { heightmapResolution = res, size = new Vector3(size, height, size) };
            var h = new float[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = x / (res - 1f) * size - size * 0.5f;
                float wz = z / (res - 1f) * size - size * 0.5f;
                h[z, x] = Mathf.Clamp01((Height(wx, wz) + height * 0.5f) / height);
            }
            td.SetHeights(0, 0, h);

            var layer = new TerrainLayer { diffuseTexture = CheckerTexture(), tileSize = new Vector2(4.4f, 4.4f) };
            AssetDatabase.CreateAsset(layer, DemoDir + "/Ground.terrainlayer");
            td.terrainLayers = new[] { layer };
            AssetDatabase.CreateAsset(td, DemoDir + "/DemoTerrain.asset");

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.position = new Vector3(-size * 0.5f, -height * 0.5f, -size * 0.5f);
            var terrain = go.GetComponent<Terrain>();
            var rp = GraphicsSettings.defaultRenderPipeline;
            if (rp != null && rp.defaultTerrainMaterial != null) terrain.materialTemplate = rp.defaultTerrainMaterial;
            return terrain;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Material mat)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pivot;
            Box(p, name + "_Mesh", new Vector3(0, -size.y * 0.5f, 0), size, mat);
            return p;
        }

        static void BuildScene()
        {
            Directory.CreateDirectory(DemoDir);
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // light
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.LookRotation(-new Vector3(0.45f, 0.8f, 0.35f).normalized);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.31f);

            var terrain = BuildTerrain();

            // fog simulation
            var simGo = new GameObject("FogSimulation");
            var sim = simGo.AddComponent<FogSimulation>();
            sim.compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(Root + "/Shaders/FluidSim.compute");
            sim.terrain = terrain;
            sim.sun = sun;

            // character (simple mannequin)
            var orange = LitMaterial("Mannequin", new Color(0.95f, 0.6f, 0.15f));
            var ch = new GameObject("Character");
            ch.transform.position = new Vector3(0, terrain.SampleHeight(Vector3.zero) + terrain.transform.position.y, 0);
            Box(ch.transform, "Pelvis", new Vector3(0, 0.74f, 0), new Vector3(0.34f, 0.18f, 0.2f), orange);
            Box(ch.transform, "Torso", new Vector3(0, 1.0f, 0), new Vector3(0.4f, 0.38f, 0.24f), orange);
            Box(ch.transform, "Head", new Vector3(0, 1.33f, 0), new Vector3(0.22f, 0.24f, 0.22f), orange);
            var mover = ch.AddComponent<CharacterMover>();
            mover.terrain = terrain;
            mover.legL = Limb(ch.transform, "LegL", new Vector3(-0.1f, 0.72f, 0), new Vector3(0.13f, 0.7f, 0.14f), orange);
            mover.legR = Limb(ch.transform, "LegR", new Vector3(0.1f, 0.72f, 0), new Vector3(0.13f, 0.7f, 0.14f), orange);
            mover.armL = Limb(ch.transform, "ArmL", new Vector3(-0.27f, 1.18f, 0), new Vector3(0.1f, 0.5f, 0.12f), orange);
            mover.armR = Limb(ch.transform, "ArmR", new Vector3(0.27f, 1.18f, 0), new Vector3(0.1f, 0.5f, 0.12f), orange);
            ch.AddComponent<FogInteractor>();

            // camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.055f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<FollowCamera>();
            follow.target = ch.transform;
            camGo.transform.position = ch.transform.position + follow.offset;
            camGo.transform.LookAt(ch.transform.position);

            var stirGo = new GameObject("MouseStirInteractor");
            stirGo.transform.SetParent(camGo.transform, false);
            var stirInteractor = stirGo.AddComponent<FogInteractor>();
            stirInteractor.radius = 1.6f;
            stirInteractor.push = 0f;
            stirInteractor.pushBySpeed = 0f;
            stirInteractor.follow = 12f;
            stirInteractor.carve = 0f;
            stirInteractor.velocityGain = 1f;
            stirInteractor.enabled = false;
            camGo.AddComponent<MouseStir>().interactor = stirInteractor;

            var ui = camGo.AddComponent<FogDemoUI>();
            ui.sim = sim;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[InteractiveFog] Demo scene created: " + ScenePath + " — press Play.");
        }
    }
}
