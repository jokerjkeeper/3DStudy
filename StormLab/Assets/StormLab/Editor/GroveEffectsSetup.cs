using System;
using BodyCurrent;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace StormLab.Editor
{
    static class GroveEffectsSetup
    {
        const string Assets = "Assets/StormLab/Grove/BodyCurrent";
        const string CasterLayer = "GroveCaster";
        const float DefaultFieldOfView = 58f;

        // Upgrades the existing scene without rebuilding the environment or moving actors.
        public static void Upgrade()
        {
            var scene = EditorSceneManager.OpenScene(GroveBuilder.ScenePath);
            var pose = Object.FindFirstObjectByType<GrovePose>();
            var binding = new SerializedObject(pose);
            var body = (Transform)binding.FindProperty("casterBody").objectReferenceValue;
            var sleeve = (Transform)binding.FindProperty("sleeve").objectReferenceValue;
            var palm = (Transform)binding.FindProperty("palm").objectReferenceValue;
            var controls = Object.FindFirstObjectByType<StormControls>();
            var camera = (Camera)new SerializedObject(controls).FindProperty("view").objectReferenceValue;
            float fieldOfView = camera.fieldOfView;
            Bind(controls, camera, body, sleeve, palm);
            camera.fieldOfView = fieldOfView;
            var meadow = GameObject.Find("Meadow ground");
            if (meadow != null)
            {
                var scale = meadow.transform.localScale;
                scale.x = Mathf.Max(scale.x, GroveBuilder.MeadowWidth);
                meadow.transform.localScale = scale;
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        internal static void Bind(StormControls controls, Camera camera, Transform body, Transform sleeve, Transform palm)
        {
            int layer = GetCasterLayer();
            AddRendererFeature(layer);
            foreach (var mesh in body.parent.GetComponentsInChildren<MeshRenderer>(true))
                mesh.gameObject.layer = layer;

            var previous = body.Find("Body current");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var holder = Pivot(body, "Body current", Vector3.zero);
            var collar = Pivot(holder, "Collar anchor", new Vector3(-0.033f, 1.39f, 0.414f));
            var shoulder = Pivot(holder, "Shoulder anchor", new Vector3(0.405f, 1.27f, 0.30f));
            var chest = Pivot(holder, "Coat anchor", new Vector3(0.18f, 1.07f, 0.414f));
            var arm = FindOrCreate(sleeve, "Sleeve current anchor", new Vector3(0.03f, 0.08f, 0.75f));
            var wrist = FindOrCreate(palm, "Wrist current anchor", new Vector3(-0.041f, -0.020f, 0.156f));
            var material = AssetDatabase.LoadAssetAtPath<Material>(Assets + "/Electric arc.mat");
            Arc(holder, "Collar to shoulder", collar, shoulder, material, 0.069f, 61, new Vector2(0f, 0.38f));
            Arc(holder, "Shoulder to sleeve", shoulder, arm, material, 0.066f, 27, new Vector2(0.38f, 0.67f));
            Arc(holder, "Sleeve to wrist", arm, wrist, material, 0.063f, 73, new Vector2(0.67f, 0.94f));
            Arc(holder, "Shoulder branch", shoulder, chest, material, 0.034f, 54, new Vector2(0.38f, 0.66f));

            if (!camera.TryGetComponent<GroveBodyCurrent>(out var current))
                current = camera.gameObject.AddComponent<GroveBodyCurrent>();
            current.Configure(controls.Sequence, holder.gameObject);
            camera.allowMSAA = false;
            camera.fieldOfView = DefaultFieldOfView;
            var settings = new SerializedObject(controls);
            settings.FindProperty("bodyCurrent").objectReferenceValue = current;
            settings.FindProperty("allowCameraZoom").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        static int GetCasterLayer()
        {
            int layer = LayerMask.NameToLayer(CasterLayer);
            if (layer >= 0) return layer;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (slot.stringValue.Length != 0) continue;
                slot.stringValue = CasterLayer;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new InvalidOperationException("Amber Grove needs a free GameObject layer for the caster mask.");
        }

        static void AddRendererFeature(int layer)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/StormLab/Generated/ForwardRenderer.asset");
            var feature = renderer.rendererFeatures.Find(item => item is BodyCurrentFeature) as BodyCurrentFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<BodyCurrentFeature>();
                feature.name = "Amber Grove Body Current";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }

            // URP uses the local file IDs to repair renderer-feature references on import.
            var rendererSettings = new SerializedObject(renderer);
            var map = rendererSettings.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long fileId);
                map.GetArrayElementAtIndex(i).longValue = fileId;
            }

            rendererSettings.ApplyModifiedPropertiesWithoutUndo();

            feature.characterLayer = 1 << layer;
            feature.maskShader = AssetDatabase.LoadAssetAtPath<Shader>(Assets + "/Shaders/CharacterMask.shader");
            feature.compositeShader = AssetDatabase.LoadAssetAtPath<Shader>(Assets + "/Shaders/Composite.shader");
            feature.outlinePixels = 0.95f;
            feature.fresnelStrength = 0.15f;
            EditorUtility.SetDirty(feature);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        static Transform FindOrCreate(Transform parent, string name, Vector3 position)
        {
            var anchor = parent.Find(name);
            if (anchor == null) return Pivot(parent, name, position);
            anchor.localPosition = position;
            return anchor;
        }

        static Transform Pivot(Transform parent, string name, Vector3 position)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            return pivot;
        }

        internal static void Arc(Transform holder, string name, Transform start, Transform end, Material material, float width, int seed, Vector2 flowRange)
        {
            var arc = Pivot(holder, name, Vector3.zero).gameObject.AddComponent<BodyArc>();
            arc.startAnchor = start;
            arc.endAnchor = end;
            arc.characterSpace = holder;
            arc.archDirection = new Vector3(0.15f, 0.15f, 1f);
            arc.archHeight = 0.048f;
            arc.jitter = 0.023f;
            arc.seed = seed;
            arc.flowRange = flowRange;

            var line = arc.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0, 0.35f), new Keyframe(0.5f, 1f), new Keyframe(1, 0.32f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            arc.Sample(0f);
        }
    }
}
