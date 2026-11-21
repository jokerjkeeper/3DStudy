using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StormLab.Editor
{
    static class CyberpunkCityBuilder
    {
        const string Folder = "Assets/StormLab/City";
        const string ModelPath = "Assets/cyberpunk-city/source/scene.glb";
        internal const string ScenePath = Folder + "/CyberpunkCity.unity";

        [MenuItem("Storm Lab/Open Cyberpunk City")]
        static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        // Authors the city variant from the same actors, effect assets and controls as Amber Grove.
        public static void Build()
        {
            var importedCity = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (importedCity == null)
                throw new InvalidOperationException("glTFast did not import the Cyberpunk City model.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/StormLab", "City");

            var scene = EditorSceneManager.OpenScene(GroveBuilder.ScenePath);
            var controls = Find<StormControls>(scene);
            var pose = new SerializedObject(Find<GrovePose>(scene));
            var caster = ((Transform)pose.FindProperty("casterBody").objectReferenceValue).parent;
            var sentinel = ((Transform)pose.FindProperty("sentinelBody").objectReferenceValue).parent;
            var settings = new SerializedObject(controls);
            var camera = (Camera)settings.FindProperty("view").objectReferenceValue;
            var looseStones = GameObject.Find("Loose testing stones").transform;
            looseStones.SetParent(null, true);
            Object.DestroyImmediate(GameObject.Find("Amber Grove"));

            var street = CreateStreet(importedCity);
            Ground(caster, street, -0.004f);
            Ground(sentinel, street, -0.009f);
            foreach (Transform stone in looseStones) Ground(stone, street, 0.14f);
            Light[] lights = ApplyLighting(camera, Find<Light>(scene, light => light.type == LightType.Directional), Find<Volume>(scene));
            DressActors(caster, sentinel);
            settings.FindProperty("sceneTitle").stringValue = "Cyberpunk City";
            settings.FindProperty("sceneDescription").stringValue = "Neon street / VFX test";
            var adjustableLights = settings.FindProperty("adjustableLights");
            adjustableLights.arraySize = lights.Length;
            for (int i = 0; i < lights.Length; i++)
                adjustableLights.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            settings.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, ScenePath, true))
                throw new InvalidOperationException("Could not save the city test scene.");
            AssetDatabase.SaveAssets();
        }

        static MeshCollider CreateStreet(GameObject importedCity)
        {
            var root = new GameObject("Cyberpunk City environment").transform;
            root.position = new Vector3(-3.84f, 0, 69.6f);
            root.rotation = Quaternion.Euler(0, -90f, 0);
            root.localScale = Vector3.one * 8f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(importedCity, root);
            model.name = "Cyberpunk City source";
            var meshes = model.GetComponentsInChildren<MeshFilter>();
            var surfaces = new GameObject("City collision surfaces").transform;
            var road = meshes.Single(filter => filter.name.StartsWith("landscape_02_", StringComparison.Ordinal));
            var street = AddSurface(surfaces, "City road and pavement", new[] { road });
            var walls = meshes.Where(filter => filter.name.Contains("skyscraper_wall_texture", StringComparison.Ordinal));
            AddSurface(surfaces, "Building facades", walls);
            Physics.SyncTransforms();
            return street;
        }

        static MeshCollider AddSurface(Transform parent, string name, IEnumerable<MeshFilter> sources)
        {
            // The source vertices are in oversized FBX units. Bake the city transform into collision geometry.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var source in sources)
            {
                int offset = vertices.Count;
                var transform = source.transform.localToWorldMatrix;
                foreach (Vector3 vertex in source.sharedMesh.vertices)
                    vertices.Add(transform.MultiplyPoint3x4(vertex));
                foreach (int index in source.sharedMesh.triangles)
                    triangles.Add(index + offset);
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            string path = $"{Folder}/{name}.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                saved = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(mesh);
            }

            var surface = new GameObject(name).AddComponent<MeshCollider>();
            surface.transform.SetParent(parent, false);
            surface.sharedMesh = saved;
            return surface;
        }

        static void Ground(Transform item, MeshCollider street, float clearance)
        {
            Vector3 position = item.position;
            if (!street.Raycast(new Ray(position + Vector3.up * 3f, Vector3.down), out var hit, 6f))
                throw new InvalidOperationException($"No road surface beneath {item.name} at {position}.");
            position.y = hit.point.y + clearance;
            item.position = position;
        }

        static Light[] ApplyLighting(Camera camera, Light key, Volume volume)
        {
            camera.name = "Street camera";
            camera.transform.position = new Vector3(0, 3.7f, -8.7f);
            camera.transform.LookAt(new Vector3(0, 2.2f, 6.4f));
            camera.fieldOfView = 51f;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 185f;
            camera.backgroundColor = ColorFromHex("1A2033");
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            key.name = "Twilight key";
            key.transform.rotation = Quaternion.Euler(52f, -26f, 0);
            key.color = ColorFromHex("C8D8FF");
            key.intensity = 0.88f;
            key.shadowStrength = 0.56f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ColorFromHex("7987A1");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 139f;
            RenderSettings.reflectionIntensity = 0.48f;

            var fill = new GameObject("Street light spill").transform;
            var cool = Point(fill, "Cool street spill", new Vector3(-3.6f, 2.7f, -2.1f), "58D9FF", 5.5f);
            var magenta = Point(fill, "Magenta billboard spill", new Vector3(4.4f, 3.4f, 1.4f), "FF92B8", 5.0f);

            string path = Folder + "/City Lighting.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.AddObjectToAsset(profile.Add<Bloom>(), profile);
                AssetDatabase.AddObjectToAsset(profile.Add<Tonemapping>(), profile);
            }
            profile.TryGet(out Bloom bloom);
            bloom.threshold.Override(1.13f);
            bloom.intensity.Override(0.33f);
            bloom.scatter.Override(0.62f);
            profile.TryGet(out Tonemapping tone);
            tone.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(tone);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
            return new[] { key, cool, magenta };
        }

        static Light Point(Transform parent, string name, Vector3 position, string color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = ColorFromHex(color);
            light.range = 8f;
            light.intensity = intensity;
            return light;
        }

        static void DressActors(Transform caster, Transform sentinel)
        {
            ReplaceMaterials(caster, new Dictionary<string, (string hex, float smoothness)>
            {
                ["Petrol blue cloth"] = ("445174", 0.27f),
                ["Spruce felt"] = ("303953", 0.23f),
                ["Burnt orange wool"] = ("B95267", 0.25f),
                ["Persimmon"] = ("9F3A5C", 0.24f)
            });
            ReplaceMaterials(sentinel, new Dictionary<string, (string hex, float smoothness)>
            {
                ["Pale warm limestone"] = ("768798", 0.40f),
                ["Stone joints"] = ("394558", 0.38f),
                ["Jade"] = ("4F8793", 0.34f),
                ["Clover"] = ("73A2A6", 0.32f)
            });
            foreach (var detail in sentinel.GetComponentsInChildren<Transform>()
                         .Where(item => item.name is "Wildflower" or "Grass tuft"))
                detail.gameObject.SetActive(false);
        }

        static void ReplaceMaterials(Transform actor, Dictionary<string, (string hex, float smoothness)> palette)
        {
            foreach (var mesh in actor.GetComponentsInChildren<MeshRenderer>())
            {
                var original = mesh.sharedMaterial;
                if (original == null || !palette.TryGetValue(original.name, out var paint)) continue;
                string path = $"{Folder}/City {original.name}.mat";
                var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (variant == null)
                {
                    variant = new Material(original);
                    AssetDatabase.CreateAsset(variant, path);
                }
                variant.SetColor("_BaseColor", ColorFromHex(paint.hex));
                variant.SetFloat("_Smoothness", paint.smoothness);
                EditorUtility.SetDirty(variant);
                mesh.sharedMaterial = variant;
            }
        }

        static T Find<T>(Scene scene, Func<T, bool> match = null) where T : Component
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>());
            return match == null ? components.Single() : components.Single(match);
        }

        static Color ColorFromHex(string html)
        {
            ColorUtility.TryParseHtmlString("#" + html, out Color color);
            return color;
        }
    }
}
