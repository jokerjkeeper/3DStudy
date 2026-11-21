using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BodyCurrent.Editor
{
    public static class BuildBodyCurrentDemo
    {
        const string Folder = "Assets/BodyCurrent";
        const string Generated = Folder + "/Demo";
        const string ScenePath = Generated + "/BodyCurrentDemo.unity";
        static Material shell, joint, trim, visor, floor, wall, strip, arcMaterial;

        [MenuItem("Body Current/Open Demo Scene")]
        public static void Open() => EditorSceneManager.OpenScene(ScenePath);

        public static void CreateSample()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerSettings.productName = "Body Current Demo";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.antiAliasing = 0;

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            AssetDatabase.CreateAsset(renderer, Generated + "/ForwardRenderer.asset");
            var rendererSettings = new SerializedObject(renderer);
            rendererSettings.FindProperty("m_IntermediateTextureMode").intValue = 1;
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            var feature = ScriptableObject.CreateInstance<BodyCurrentFeature>();
            feature.name = "Body Current";
            feature.characterLayer = 1 << 8;
            feature.maskShader = Shader.Find("Body Current/Character Mask");
            feature.compositeShader = Shader.Find("Body Current/Composite");
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, Generated + "/BodyCurrentURP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue = "BodyCurrentCharacter";
            tags.ApplyModifiedPropertiesWithoutUndo();

            shell = Lit("Slate armor", new Color(0.22f, 0.28f, 0.35f), 0.48f);
            joint = Lit("Graphite joints", new Color(0.055f, 0.077f, 0.11f), 0.35f);
            trim = Lit("Brushed copper", new Color(0.52f, 0.24f, 0.10f), 0.52f);
            visor = Lit("Visor", new Color(0.028f, 0.10f, 0.15f), 0.72f);
            visor.EnableKeyword("_EMISSION");
            visor.SetColor("_EmissionColor", new Color(0.01f, 0.11f, 0.18f));
            floor = Lit("Floor", new Color(0.084f, 0.105f, 0.131f), 0.26f);
            wall = Lit("Set panels", new Color(0.105f, 0.13f, 0.17f), 0.3f);
            strip = Lit("Amber set lights", new Color(0.53f, 0.26f, 0.10f), 0.24f);
            strip.EnableKeyword("_EMISSION");
            strip.SetColor("_EmissionColor", new Color(0.80f, 0.22f, 0.07f));
            arcMaterial = new Material(Shader.Find("Body Current/Arc")) { name = "Electric arc" };
            AssetDatabase.CreateAsset(arcMaterial, Generated + "/Electric arc.mat");

            MakeStage();
            MakeCharacter(feature);
            MakeCamera();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        static Material Lit(string name, Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.15f);
            AssetDatabase.CreateAsset(material, Generated + "/" + name + ".mat");
            return material;
        }

        static void MakeStage()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.33f, 0.37f, 0.45f);
            Transform stage = Pivot("Stage", null, Vector3.zero);
            Shape("Floor", PrimitiveType.Cube, stage, new Vector3(0, -0.12f, 0), new Vector3(23, 0.2f, 22), floor, false);
            Shape("Platform", PrimitiveType.Cylinder, stage, new Vector3(0, -0.023f, 0), new Vector3(2.75f, 0.04f, 2.75f), wall, false);
            Shape("Rear wall", PrimitiveType.Cube, stage, new Vector3(0, 2.4f, 3.1f), new Vector3(17, 5.2f, 0.3f), wall, false);

            for (int i = -4; i <= 4; i++)
            {
                float x = i * 1.55f;
                Shape("Wall rib", PrimitiveType.Cube, stage, new Vector3(x, 1.72f, 2.9f), new Vector3(0.11f, 3.5f, 0.18f), floor, false);
                if (i % 2 != 0)
                    Shape("Amber strip", PrimitiveType.Cube, stage, new Vector3(x + 0.18f, 0.8f, 2.88f), new Vector3(0.026f, 0.79f, 0.035f), strip, false);
            }

            AddLight("Soft key", LightType.Directional, new Vector3(1, 4, -3), new Vector3(39, -31, 0), new Color(0.80f, 0.88f, 1f), 2.05f);
            AddLight("Warm fill", LightType.Point, new Vector3(-2.1f, 2.1f, -0.1f), Vector3.zero, new Color(1, 0.49f, 0.26f), 5.8f);
            AddLight("Cool wall wash", LightType.Point, new Vector3(0, 2.65f, 2.05f), Vector3.zero, new Color(0.43f, 0.59f, 0.84f), 5f);
        }

        static void MakeCharacter(BodyCurrentFeature feature)
        {
            var body = Pivot("Animated Character", null, Vector3.zero);
            var motion = body.gameObject.AddComponent<DemoMotion>();
            motion.feature = feature;

            Shape("Pelvis", PrimitiveType.Sphere, body, new Vector3(0, 1.08f, 0.02f), new Vector3(0.57f, 0.36f, 0.39f), shell);
            motion.chest = Pivot("Chest", body, new Vector3(0, 1.22f, 0));
            Shape("Abdomen", PrimitiveType.Capsule, motion.chest, new Vector3(0, 0.11f, 0), new Vector3(0.48f, 0.22f, 0.37f), joint);
            Shape("Torso armor", PrimitiveType.Sphere, motion.chest, new Vector3(0, 0.38f, 0), new Vector3(0.78f, 0.67f, 0.49f), shell);
            Shape("Chest inset", PrimitiveType.Sphere, motion.chest, new Vector3(0, 0.38f, -0.211f), new Vector3(0.52f, 0.38f, 0.11f), joint);
            Shape("Chest accent", PrimitiveType.Capsule, motion.chest, new Vector3(0, 0.43f, -0.265f), new Vector3(0.049f, 0.103f, 0.024f), trim);

            motion.head = Pivot("Head", motion.chest, new Vector3(0, 0.72f, 0));
            Shape("Neck", PrimitiveType.Capsule, motion.head, new Vector3(0, 0.02f, 0), new Vector3(0.21f, 0.13f, 0.22f), joint);
            Shape("Helmet", PrimitiveType.Sphere, motion.head, new Vector3(0, 0.23f, -0.01f), new Vector3(0.40f, 0.48f, 0.42f), shell);
            Shape("Face shield", PrimitiveType.Sphere, motion.head, new Vector3(0, 0.255f, -0.173f), new Vector3(0.36f, 0.22f, 0.14f), visor);

            var electricity = Pivot("Shoulder Arcs", body, Vector3.zero);
            motion.arcs = electricity.gameObject;
            for (int side = -1; side <= 1; side += 2)
            {
                string name = side < 0 ? "Left" : "Right";
                var arm = Pivot(name + " upper arm", motion.chest, new Vector3(side * 0.37f, 0.52f, 0));
                var elbow = Pivot(name + " forearm", arm, new Vector3(0, -0.39f, 0));
                if (side < 0)
                {
                    motion.leftArm = arm;
                    motion.leftElbow = elbow;
                }
                else
                {
                    motion.rightArm = arm;
                    motion.rightElbow = elbow;
                }

                Shape("Shoulder plate", PrimitiveType.Sphere, arm, new Vector3(side * 0.04f, -0.025f, 0), new Vector3(0.35f, 0.36f, 0.39f), shell);
                Shape("Upper arm", PrimitiveType.Capsule, arm, new Vector3(0, -0.22f, 0), new Vector3(0.235f, 0.24f, 0.25f), shell);
                Shape("Elbow joint", PrimitiveType.Sphere, elbow, Vector3.zero, new Vector3(0.205f, 0.21f, 0.21f), joint);
                Shape("Forearm armor", PrimitiveType.Capsule, elbow, new Vector3(0, -0.19f, -0.01f), new Vector3(0.22f, 0.195f, 0.23f), shell);
                Shape("Glove", PrimitiveType.Sphere, elbow, new Vector3(0, -0.39f, -0.015f), new Vector3(0.15f, 0.23f, 0.19f), joint);

                var collar = Pivot(name + " collar anchor", motion.chest, new Vector3(side * 0.15f, 0.65f, -0.155f));
                var pad = Pivot(name + " shoulder anchor", arm, new Vector3(side * 0.12f, 0.065f, -0.145f));
                var bicep = Pivot(name + " arm anchor", arm, new Vector3(side * 0.08f, -0.29f, -0.097f));
                var chest = Pivot(name + " chest anchor", motion.chest, new Vector3(side * 0.22f, 0.375f, -0.245f));
                MakeArc(electricity, name + " collar arc", collar, pad, arm, new Vector3(side * 0.22f, 0.67f, -0.72f), 0.065f, 0.036f, 0.017f, 23 + side);
                MakeArc(electricity, name + " upper arm arc", pad, bicep, arm, new Vector3(side * 0.6f, 0, -1), 0.065f, 0.033f, 0.014f, 49 + side);
                MakeArc(electricity, name + " branch", pad, chest, arm, new Vector3(0, 0.26f, -1), 0.035f, 0.028f, 0.009f, 76 + side);

                var leg = Pivot(name + " leg", body, new Vector3(side * 0.20f, 1.06f, 0.02f));
                leg.localRotation = Quaternion.Euler(2, side * -6, side * 9);
                Shape("Thigh", PrimitiveType.Capsule, leg, new Vector3(0, -0.245f, 0), new Vector3(0.28f, 0.29f, 0.29f), shell);
                Shape("Knee", PrimitiveType.Sphere, leg, new Vector3(0, -0.49f, -0.06f), new Vector3(0.23f, 0.19f, 0.22f), trim);
                Shape("Shin", PrimitiveType.Capsule, leg, new Vector3(0, -0.72f, 0.015f), new Vector3(0.237f, 0.27f, 0.25f), shell);
                Shape("Boot", PrimitiveType.Sphere, leg, new Vector3(0, -0.94f, -0.09f), new Vector3(0.255f, 0.19f, 0.43f), joint);
            }

            motion.SamplePose(0);
            foreach (var arc in electricity.GetComponentsInChildren<BodyArc>())
                arc.Sample(0);
        }

        static void MakeArc(Transform parent, string name, Transform start, Transform end, Transform space,
            Vector3 archDirection, float height, float jitter, float width, int seed)
        {
            var holder = Pivot(name, parent, Vector3.zero);
            var line = holder.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = arcMaterial;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1, 0.28f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            var arc = holder.gameObject.AddComponent<BodyArc>();
            arc.startAnchor = start;
            arc.endAnchor = end;
            arc.characterSpace = space;
            arc.archDirection = archDirection;
            arc.archHeight = height;
            arc.jitter = jitter;
            arc.seed = seed;
        }

        static void MakeCamera()
        {
            var rig = Pivot("Preview Camera", null, new Vector3(2.55f, 1.72f, -4.55f));
            rig.LookAt(new Vector3(0, 1.16f, 0));
            rig.gameObject.tag = "MainCamera";
            var camera = rig.gameObject.AddComponent<Camera>();
            camera.fieldOfView = 32;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.021f, 0.027f, 0.04f);
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var volume = Pivot("Bloom", null, Vector3.zero).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Generated + "/Demo Bloom.asset");
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.86f);
            bloom.intensity.Override(0.58f);
            bloom.scatter.Override(0.60f);
            AssetDatabase.AddObjectToAsset(bloom, profile);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
        }

        static void AddLight(string name, LightType type, Vector3 position, Vector3 angles, Color color, float intensity)
        {
            var pivot = Pivot(name, null, position);
            pivot.rotation = Quaternion.Euler(angles);
            var light = pivot.gameObject.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = 6;
            if (type == LightType.Directional)
                light.shadows = LightShadows.Soft;
        }

        static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            result.localPosition = position;
            return result;
        }

        static Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool character = true)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.layer = character ? 8 : 0;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;
            return shape.transform;
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var motion = Object.FindFirstObjectByType<DemoMotion>();
            var arcs = motion.arcs.GetComponentsInChildren<BodyArc>();
            motion.SamplePose(0.63f);
            foreach (var arc in arcs)
            {
                arc.Sample(0.63f);
                var line = arc.GetComponent<LineRenderer>();
                if (line.GetPosition(0) != arc.startAnchor.position ||
                    line.GetPosition(line.positionCount - 1) != arc.endAnchor.position)
                    throw new System.InvalidOperationException("An arc is detached from its anchors.");
            }

            var camera = Camera.main;
            var target = new RenderTexture(1320, 960, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            const int panelWidth = 470;
            var comparison = new Texture2D(panelWidth * 3, target.height, TextureFormat.RGB24, false);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            float outline = motion.feature.outlinePixels;
            float fresnel = motion.feature.fresnelStrength;
            try
            {
                RenderFrame();
                File.WriteAllBytes(Path.GetFullPath("Preview.png"), image.EncodeToPNG());
                motion.arcs.SetActive(false);

                for (int panel = 0; panel < 3; panel++)
                {
                    motion.feature.outlinePixels = panel == 0 ? 0 : outline;
                    motion.feature.fresnelStrength = panel == 1 ? 0 : fresnel;
                    RenderFrame();
                    comparison.SetPixels(panel * panelWidth, 0, panelWidth, target.height,
                        image.GetPixels(454, 0, panelWidth, target.height));
                }

                comparison.Apply();
                File.WriteAllBytes(Path.GetFullPath("Comparison.png"), comparison.EncodeToPNG());
            }
            finally
            {
                motion.feature.outlinePixels = outline;
                motion.feature.fresnelStrength = fresnel;
                motion.arcs.SetActive(true);
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(comparison);
            }

            void RenderFrame()
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
            }
        }
    }
}