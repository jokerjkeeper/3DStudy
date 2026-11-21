using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StormLab.Editor
{
    static class RangeBuilder
    {
        const string Root = "Assets/StormLab";
        const string Generated = Root + "/Generated";
        const string ScenePath = Root + "/StormLab.unity";

        [MenuItem("Storm Lab/Open test range")]
        static void OpenRange()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new System.InvalidOperationException("The test range already exists.");
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
            ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            AssetDatabase.CreateAsset(renderer, Generated + "/ForwardRenderer.asset");
            var decal = ScriptableObject.CreateInstance<DecalRendererFeature>();
            decal.name = "Scorch decals";
            var decalSettings = new SerializedObject(decal);
            decalSettings.FindProperty("m_Settings.technique").enumValueIndex = 1; // DBuffer
            decalSettings.FindProperty("m_Settings.dBufferSettings.surfaceData").enumValueIndex = 0; // Albedo
            decalSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(decal, renderer);
            renderer.rendererFeatures.Add(decal);
            // Keep the renderer's feature map in sync with the persisted subasset.
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(decal, out _, out long fileId);
            var rendererSettings = new SerializedObject(renderer);
            var featureMap = rendererSettings.FindProperty("m_RendererFeatureMap");
            featureMap.arraySize = 1;
            featureMap.GetArrayElementAtIndex(0).longValue = fileId;
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = true;
            pipeline.supportsCameraOpaqueTexture = true;
            pipeline.supportsCameraDepthTexture = true;

            pipeline.maxAdditionalLightsCount = 6;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_OpaqueDownsampling").enumValueIndex = 0;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pipeline, Generated + "/StormURP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.productName = "Storm Lab";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.30f, 0.37f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.028f, 0.051f, 0.075f);

            var camera = new GameObject("Range Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(9.6f, 7.6f, -12.2f);
            camera.transform.LookAt(new Vector3(0.0f, 0.85f, 2.05f));
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var sun = new GameObject("Overhead wash").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52, -37, 0);
            sun.color = new Color(0.73f, 0.83f, 1f);
            sun.intensity = 1.04f;
            sun.shadows = LightShadows.Soft;

            var rim = new GameObject("Warm wall wash").AddComponent<Light>();
            rim.type = LightType.Point;
            rim.transform.position = new Vector3(-4.7f, 2.8f, 6.7f);
            rim.color = new Color(1f, 0.49f, 0.23f);
            rim.range = 10f;
            rim.intensity = 9f;

            var cool = new GameObject("Cool emitter wash").AddComponent<Light>();
            cool.type = LightType.Point;
            cool.transform.position = new Vector3(-4f, 2.35f, -3.3f);
            cool.color = new Color(0.21f, 0.64f, 1f);
            cool.range = 7f;
            cool.intensity = 3f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Generated + "/RangeProfile.asset");
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.98f;
            bloom.intensity.value = 0.63f;
            bloom.scatter.value = 0.68f;
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;
            var grade = profile.Add<Vignette>(true);
            grade.intensity.value = 0.19f;
            grade.smoothness.value = 0.47f;
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Global HDR volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var floor = Lit("Basalt floor", new Color(0.21f, 0.25f, 0.29f), 0.08f, 0.44f);
            var tile = Lit("Inset steel", new Color(0.28f, 0.34f, 0.39f), 0.32f, 0.50f);
            var wall = Lit("Bulkhead", new Color(0.27f, 0.33f, 0.39f), 0.13f, 0.38f);
            var frame = Lit("Graphite frame", new Color(0.095f, 0.13f, 0.17f), 0.44f, 0.43f);
            var brushed = Lit("Canister steel", new Color(0.45f, 0.53f, 0.56f), 0.39f, 0.42f);
            var hazard = Lit("Safety amber", new Color(0.87f, 0.49f, 0.19f), 0.18f, 0.55f);
            var blueStrip = Lit("Blue indicator", new Color(0.14f, 0.52f, 0.70f), 0.1f, 0.55f, new Color(0.10f, 0.60f, 0.90f) * 1.4f);
            var orangeStrip = Lit("Amber indicator", new Color(0.78f, 0.35f, 0.09f), 0.1f, 0.51f, new Color(1f, 0.36f, 0.075f) * 1.85f);
            var core = Lit("Emitter core", new Color(0.16f, 0.54f, 0.70f), 0.2f, 0.62f, new Color(0.08f, 0.71f, 1f) * 3f);

            var room = new GameObject("Test range structure").transform;
            Cube(room, "Range floor", new Vector3(0, -0.24f, 1.6f), new Vector3(21.0f, 0.48f, 19.5f), floor, true);
            for (int x = -9; x <= 9; x += 2)
                Cube(room, "Floor seam", new Vector3(x, 0.009f, 1.6f), new Vector3(0.024f, 0.016f, 18.8f), tile);
            for (int z = -7; z <= 11; z += 2)
                Cube(room, "Cross seam", new Vector3(0.0f, 0.009f, z), new Vector3(20.6f, 0.016f, 0.024f), tile);
            Cube(room, "Rear bulkhead", new Vector3(0, 1.9f, 10.0f), new Vector3(21.0f, 3.8f, 0.55f), frame, true);
            Cube(room, "West bulkhead", new Vector3(-10.1f, 1.55f, 1.4f), new Vector3(0.50f, 3.1f, 16.5f), frame, true);
            for (int i = 0; i < 9; i++)
            {
                float x = -9.2f + i * 2.3f;
                Cube(room, $"Rear panel {i + 1}", new Vector3(x, 1.77f, 9.66f), new Vector3(2.08f, 2.9f, 0.17f), wall, true);
                Cube(room, "Rear lower insert", new Vector3(x, 0.48f, 9.53f), new Vector3(1.80f, 0.24f, 0.13f), tile);
                if (i % 2 == 0)
                    Cube(room, "Rear vertical indicator", new Vector3(x - 0.82f, 2.35f, 9.52f), new Vector3(0.07f, 1.09f, 0.08f), blueStrip);
            }
            for (int i = 0; i < 6; i++)
            {
                float z = -5.3f + i * 2.52f;
                Cube(room, $"West panel {i + 1}", new Vector3(-9.75f, 1.47f, z), new Vector3(0.21f, 2.62f, 2.29f), wall, true);
                Cube(room, "West rail", new Vector3(-9.6f, 0.62f, z), new Vector3(0.13f, 0.11f, 2.10f), orangeStrip);
            }
            Cube(room, "Rear light track", new Vector3(0f, 3.42f, 9.54f), new Vector3(20.3f, 0.08f, 0.10f), blueStrip);

            var bay = new GameObject("Impact test bay").transform;
            Vector3 bayCenter = new(0.5f, 0, 3.05f);
            Cylinder(bay, "Impact platform", bayCenter + Vector3.up * 0.09f, new Vector3(5.56f, 0.09f, 5.56f), frame, true);
            Cylinder(bay, "Impact deck", bayCenter + Vector3.up * 0.175f, new Vector3(5.35f, 0.012f, 5.35f), tile, true);
            for (int i = 0; i < 32; i++)
            {
                float angle = 360f * i / 32;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                var marker = Cube(bay, "Bay perimeter inlay", bayCenter + direction * 2.47f + Vector3.up * 0.196f,
                    new Vector3(0.33f, 0.008f, 0.075f), i % 8 == 0 ? orangeStrip : hazard);
                marker.transform.rotation = Quaternion.Euler(0, angle, 0);
            }
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = new(0.25f + i * 0.48f, 0.024f, -0.28f);
                Cube(room, "Approach stripe", p, new Vector3(0.27f, 0.010f, 0.66f), hazard).transform.rotation =
                    Quaternion.Euler(0, -32, 0);
            }
            Cube(bay, "Target plinth", new Vector3(0.55f, 0.48f, 3.44f), new Vector3(1.11f, 0.61f, 0.96f), frame, true);
            Cube(bay, "Strike plate", new Vector3(0.55f, 1.35f, 3.44f), new Vector3(1.23f, 1.25f, 0.27f), brushed, true);
            Cube(bay, "Strike plate upper marker", new Vector3(0.55f, 1.83f, 3.294f), new Vector3(0.96f, 0.041f, 0.025f), orangeStrip);
            Cube(bay, "Strike plate lower marker", new Vector3(0.55f, 0.90f, 3.294f), new Vector3(0.96f, 0.041f, 0.025f), orangeStrip);
            var target = new GameObject("Default aim point").transform;
            target.SetParent(bay);
            target.position = new Vector3(0.55f, 1.33f, 3.34f);

            var bodies = new List<Rigidbody>();
            Vector3[] debris = {
                new(-1.03f, 0.54f, 2.65f), new(-0.65f, 0.54f, 3.78f), new(1.68f, 0.54f, 2.63f),
                new(2.10f, 0.54f, 3.32f), new(1.66f, 0.54f, 4.55f), new(-0.17f, 0.54f, 4.72f),
                new(0.34f, 0.54f, 1.50f), new(-1.44f, 0.54f, 3.78f)
            };
            for (int i = 0; i < debris.Length; i++)
            {
                var can = Cube(bay, $"Test block {i + 1:00}", debris[i], new Vector3(0.55f, 0.69f, 0.55f), i % 2 == 0 ? brushed : hazard, true);
                can.transform.rotation = Quaternion.Euler(0, i * 23f, 0);
                // Keep decorative bands under a unit-scale root so they follow the rigidbody.
                var body = new GameObject(can.name).AddComponent<Rigidbody>();
                body.transform.SetParent(bay, false);
                body.transform.SetPositionAndRotation(can.transform.position, can.transform.rotation);
                body.mass = 0.90f;
                body.angularDamping = 0.18f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                can.name = "Housing";
                can.transform.SetParent(body.transform, true);
                Cube(body.transform, "Graphite bumper", can.transform.position, new Vector3(0.57f, 0.072f, 0.57f), frame)
                    .transform.rotation = can.transform.rotation;
                bodies.Add(body);
            }

            var launcher = new GameObject("Discharge emitter").transform;
            Vector3 origin = new(-2.90f, 0, -2.42f);
            Cylinder(launcher, "Emitter footing", origin + Vector3.up * 0.14f, new Vector3(2.18f, 0.14f, 2.18f), frame, true);
            Cylinder(launcher, "Emitter upper deck", origin + Vector3.up * 0.285f, new Vector3(1.94f, 0.042f, 1.94f), brushed, true);
            Cube(launcher, "Power pedestal", origin + Vector3.up * 0.76f, new Vector3(0.78f, 0.92f, 0.76f), frame, true);
            Cube(launcher, "Power status", origin + new Vector3(0.402f, 0.69f, 0), new Vector3(0.02f, 0.28f, 0.47f), blueStrip);
            var coil = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            coil.name = "Induction chamber";
            coil.transform.SetParent(launcher, false);
            coil.transform.SetPositionAndRotation(origin + Vector3.up * 1.35f, Quaternion.Euler(0, 0, 90));
            coil.transform.localScale = new Vector3(0.42f, 0.65f, 0.42f);
            Object.DestroyImmediate(coil.GetComponent<Collider>());
            coil.GetComponent<Renderer>().sharedMaterial = core;
            var muzzle = new GameObject("Arc start").transform;
            muzzle.SetParent(launcher, false);
            muzzle.position = origin + new Vector3(0.53f, 1.38f, 0.0f);

            var wave = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wave.name = "Shockwave screen sampling mesh";
            Object.DestroyImmediate(wave.GetComponent<Collider>());
            var waveRenderer = wave.GetComponent<Renderer>();
            waveRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waveRenderer.receiveShadows = false;
            waveRenderer.enabled = false;
            var flash = new GameObject("Impact point light").AddComponent<Light>();
            flash.type = LightType.Point;
            flash.enabled = false;
            var arcs = new GameObject("Arc generator").AddComponent<ArcFlash>();
            var arcMaterial = MakeMaterial("Arc and spark trail", Shader.Find("StormLab/Arc"));
            arcs.SetMaterial(arcMaterial);

            var feedback = new GameObject("Impact feedback").AddComponent<ImpactFeedback>();
            wave.transform.SetParent(feedback.transform, false);
            flash.transform.SetParent(feedback.transform, false);
            feedback.Configure(camera, flash, waveRenderer, arcMaterial,
                MakeMaterial("Shockwave", Shader.Find("StormLab/Shockwave")), ScorchMaterial());

            new GameObject("Storm Lab controls").AddComponent<StormControls>()
                .Configure(camera, muzzle, target, arcs, feedback, volume, coil.GetComponent<Renderer>(), bodies.ToArray());
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Storm Lab range generated.");
        }

        static Material ScorchMaterial()
        {
            var decalShader = AssetDatabase.LoadAssetAtPath<Shader>(
                UniversalRenderPipelineAsset.packagePath + "/Shaders/Decal.shadergraph");
            var material = MakeMaterial("Scorch DBuffer decal", decalShader);
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 p = new Vector2(x / 127f, y / 127f) * 2 - Vector2.one;
                float r = p.magnitude;
                float speckle = Mathf.PerlinNoise(x * 0.118f + 6f, y * 0.122f);
                float edge = r + speckle * 0.21f;
                float soot = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.18f, 0.99f, edge))) * (0.62f + speckle * 0.38f);
                float charred = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.46f, r));
                var color = Color.Lerp(new Color(0.20f, 0.095f, 0.036f), new Color(0.027f, 0.016f, 0.014f), charred);
                color.a = soot;
                texture.SetPixel(x, y, color);
            }
            string path = Generated + "/Scorch.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            material.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetFloat("Normal_Blend", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Lit(string name, Color color, float metallic, float smoothness, Color emission = default)
        {
            var material = MakeMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MakeMaterial(string name, Shader shader)
        {
            if (shader == null)
                throw new System.InvalidOperationException($"Shader missing for {name}.");
            var material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, $"{Generated}/{name}.mat");
            return material;
        }

        static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider = false)
            => Primitive(PrimitiveType.Cube, parent, name, position, size, material, collider);

        static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider = false)
        {
            var cylinder = Primitive(PrimitiveType.Cylinder, parent, name, position, size, material, false);
            if (collider)
            {
                var mesh = cylinder.AddComponent<MeshCollider>();
                mesh.sharedMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
            }
            return cylinder;
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }
    }
}
