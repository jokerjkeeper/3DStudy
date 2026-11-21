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
using Random = System.Random;

namespace StormLab.Editor
{
    public sealed class GroveBuilder
    {
        const string Folder = "Assets/StormLab/Grove";
        internal const string ScenePath = Folder + "/AmberGrove.unity";
        internal const float MeadowWidth = 44f;
        readonly Dictionary<string, Material> palette = new();
        readonly Random random = new(79);
        Material grass, clover, olive, jade, fir, bark, honey, clay, cream, slate, foam, ink;
        Mesh tile, roof, brim, cap;

        [MenuItem("Storm Lab/Open Amber Grove")]
        static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        // Reconstructs the authored scene from the shared battle harness in a staging project.
        public static void Build() => new GroveBuilder().BuildScene();

        void BuildScene()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/StormLab", "Grove");
            var scene = EditorSceneManager.OpenScene(VfxFlowWindow.BattleScenePath);
            var sequence = Find<ChargeSequence>(scene);
            var controls = Find<StormControls>(scene);
            var light = GameObject.Find("Low afternoon sun").GetComponent<Light>();
            var camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            var volume = Find<Volume>(scene);
            var fixture = sequence.Hand.transform;
            fixture.SetParent(null, true);
            Object.DestroyImmediate(GameObject.Find("Quay environment"));
            Object.DestroyImmediate(GameObject.Find("Wayfarer caster"));
            Object.DestroyImmediate(GameObject.Find("Quay sentinel"));
            Object.DestroyImmediate(GameObject.Find("Battle choreography"));
            Object.DestroyImmediate(GameObject.Find("Cool quay bounce"));
            Object.DestroyImmediate(GameObject.Find("Lantern window spill"));

            CreatePalette();
            CreateMeshes();
            SetLighting(camera, light, volume);
            var set = Group(null, "Amber Grove");
            BuildTerrain(set);
            BuildShrine(set);
            PlantGrove(set);
            var (caster, sleeve, palm, gem) = BuildCaster();
            var (sentinel, receiver, aim) = BuildSentinel();

            fixture.SetParent(palm, false);
            fixture.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            fixture.localScale = Vector3.one;

            // Keep the entire charge fixture (including particle systems and its original effect anchor).
            var origin = sequence.Hand.Palm;
            origin.SetPositionAndRotation(palm.position, palm.rotation);
            var pose = Group(null, "Grove choreography").gameObject.AddComponent<GrovePose>();
            pose.Configure(sequence, caster, sleeve, palm, sentinel, receiver);
            controls.Configure(camera, origin, aim, Find<ArcFlash>(scene), Find<ImpactFeedback>(scene),
                volume, gem, BuildPhysicsProps(set));
            var settings = new SerializedObject(controls);
            settings.FindProperty("battlePose").objectReferenceValue = pose;
            settings.FindProperty("allowHideHud").boolValue = true;
            settings.FindProperty("sceneTitle").stringValue = "Amber Grove";
            settings.FindProperty("sceneDescription").stringValue = "3D spell testing ground";
            settings.ApplyModifiedPropertiesWithoutUndo();
            GroveEffectsSetup.Bind(controls, camera, caster, sleeve, palm);
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            AssetDatabase.SaveAssets();
        }

        void CreatePalette()
        {
            grass = Paint("Meadow", "799958");
            clover = Paint("Clover", "A0B465");
            olive = Paint("Chartreuse", "B8C668");
            jade = Paint("Jade", "62986E");
            fir = Paint("Deep pine", "3C756B");
            bark = Paint("Cinnamon", "91604C");
            honey = Paint("Harvest gold", "E6AA50");
            clay = Paint("Persimmon", "C9714C");
            cream = Paint("Ivory", "F4DFC0");
            slate = Paint("River pebble", "899786");
            foam = Paint("Sea glass", "83C0AA");
            ink = Paint("Deep teal", "2A4A4D");
        }

        void CreateMeshes()
        {
            tile = Profile("Soft edged stone", 12, 0.24f,
                new Vector2(0.78f, -0.5f), new Vector2(0.96f, -0.34f), new Vector2(1, 0.17f),
                new Vector2(0.87f, 0.48f), new Vector2(0.48f, 0.5f));
            roof = Profile("Shrine roof", 6, Mathf.PI / 6,
                new Vector2(0.95f, -0.5f), new Vector2(1f, -0.37f), new Vector2(0.72f, -0.13f),
                new Vector2(0.43f, 0.17f), new Vector2(0.19f, 0.5f));
            brim = Profile("Felt brim", 40, 0f,
                new Vector2(0.65f, -0.4f), new Vector2(0.96f, -0.23f), new Vector2(1f, 0.06f),
                new Vector2(0.87f, 0.4f), new Vector2(0.59f, 0.5f));
            cap = Profile("Felt crown", 32, 0f,
                new Vector2(0.81f, -0.5f), new Vector2(0.86f, -0.35f), new Vector2(0.79f, 0.03f),
                new Vector2(0.58f, 0.38f), new Vector2(0.29f, 0.49f));
        }

        void SetLighting(Camera camera, Light light, Volume volume)
        {
            camera.name = "Grove Camera";
            camera.transform.position = V(0, 4.6f, -10.9f);
            camera.transform.LookAt(V(0, 1.28f, 1.15f));
            camera.orthographic = false;
            camera.fieldOfView = 39f;
            camera.farClipPlane = 85f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("C3DDD2");
            light.name = "Honey afternoon sun";
            light.transform.rotation = Quaternion.Euler(43f, -34f, 0f);
            light.color = Hex("FFE5C1");
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.79f;
            light.shadowBias = 0.105f;
            light.shadowNormalBias = 0.15f;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("BDCFC5");
            RenderSettings.ambientEquatorColor = Hex("9BAE9E");
            RenderSettings.ambientGroundColor = Hex("746B59");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 36f;
            RenderSettings.reflectionIntensity = 0.3f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Folder + "/Grove Light.asset");
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.59f);
            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.Neutral);
            AssetDatabase.AddObjectToAsset(bloom, profile);
            AssetDatabase.AddObjectToAsset(tonemap, profile);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        void BuildTerrain(Transform set)
        {
            var land = Group(set, "Meadow and stone path");
            var floor = Shape(land, "Meadow ground", PrimitiveType.Cube, V(0, -0.24f, 5), V(MeadowWidth, 0.48f, 31), grass);
            floor.gameObject.AddComponent<BoxCollider>();
            var trail = Paint("Warm gravel", "C4B887");
            var patches = Group(land, "Grass islands");
            for (int i = 0; i < 28; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Blob(patches, "Clover bank", V(side * Between(4.7f, 10.5f), -0.19f, Between(-5f, 12f)),
                    V(Between(2, 6), Between(0.45f, 0.98f), Between(1.7f, 5f)), Pick(clover, olive, jade, grass));
            }

            // A broad, quiet arena under the arc, with the stepping stones leading back to the warm doorway.
            Blob(land, "Packed earth clearing", V(0, -0.09f, 0.15f), V(9.4f, 0.23f, 5.45f), trail);
            for (int i = 0; i < 19; i++)
            {
                float z = -4.8f + i * 0.57f;
                float x = Mathf.Sin(i * 0.47f - 0.8f) * (z < 1.7f ? 0.43f : 0.73f);
                var step = Form(land, "Hand cut stepping stone", tile, V(x, 0.026f, z),
                    V(Between(0.57f, 0.73f), Between(0.063f, 0.16f), Between(0.24f, 0.34f)),
                    Pick(slate, Paint("Buff stone", "B5B49B"), Paint("Cream stone", "D4C7A8")));
                step.localRotation = Quaternion.Euler(0, Between(-19, 19), 0);
            }

            for (int i = 0; i < 54; i++)
            {
                float x = Between(-8.6f, 8.6f), z = Between(-5.6f, 8.3f);
                if (Mathf.Abs(x) < 3.7f && z is > -2.2f and < 2.2f) continue;
                if (Mathf.Abs(x) < 1.1f || (Mathf.Abs(x) < 2.8f && z > 3.7f)) continue;
                Form(land, "Weathered pebble", tile, V(x, 0.017f, z),
                    V(Between(0.12f, 0.47f), Between(0.08f, 0.24f), Between(0.14f, 0.33f)), Pick(slate, cream, jade))
                    .localRotation = Quaternion.Euler(0, Between(0, 360), 0);
            }

            var far = Group(set, "Misty valley");
            var mist = Paint("Distant sage", "78A59D");
            for (int i = 0; i < 12; i++)
                Blob(far, "Distant treetop ridge", V(-22 + 4 * i, 0f, Between(12, 24)),
                    V(Between(6, 10), Between(3, 8), Between(6, 10)), Pick(fir, mist, jade));
        }

        void BuildShrine(Transform set)
        {
            var shrine = Group(set, "Seedkeeper's cottage", V(0.15f, 0, 5.6f));
            var washedPlaster = Paint("Apricot plaster", "EBC890");
            var darkWood = Paint("Walnut", "6F4B40");
            var roofEdge = Paint("Roof edge", "467365");
            var window = Paint("Warm glass", "FDD48C", 0.42f);
            Form(shrine, "Foundation", tile, V(0, 0.17f, 0), V(1.95f, 0.36f, 1.47f), slate);
            Form(shrine, "Octagonal plaster walls", tile, V(0, 1.18f, 0), V(1.72f, 2.2f, 1.23f), washedPlaster);
            Form(shrine, "Sill belt", tile, V(0, 0.43f, 0), V(1.78f, 0.19f, 1.27f), bark);
            Form(shrine, "Eaves", roof, V(0, 2.63f, 0), V(2.23f, 1.39f, 1.78f), roofEdge);
            Form(shrine, "Upper roof", roof, V(0, 3.16f, 0), V(1.56f, 1.28f, 1.28f), fir);
            Form(shrine, "Copper cupola", roof, V(0, 3.78f, 0), V(0.66f, 0.79f, 0.66f), clay);
            Blob(shrine, "Cottage finial", V(0, 4.17f, 0), V(0.28f, 0.39f, 0.28f), honey);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                Vector3 v = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                Blob(shrine, "Eaves finial", v * 1.72f + V(0, 2.17f, 0), V(0.24f, 0.24f, 0.24f), honey);
            }

            Form(shrine, "Door surround", tile, V(0.2f, 0.91f, -1.21f), V(0.64f, 1.6f, 0.15f), darkWood);
            Form(shrine, "Amber door", tile, V(0.2f, 0.9f, -1.33f), V(0.51f, 1.39f, 0.055f), clay);
            for (int i = -1; i <= 1; i++)
                Blob(shrine, "Door plank", V(0.2f + i * 0.23f, 0.88f, -1.39f), V(0.19f, 1.13f, 0.08f), bark);
            Blob(shrine, "Door knob", V(0.49f, 0.91f, -1.44f), V(0.12f, 0.12f, 0.09f), honey);
            Form(shrine, "Doorstep", tile, V(0.21f, 0.09f, -1.55f), V(0.8f, 0.22f, 0.44f), slate);
            foreach (float x in new[] { -0.95f, 1.13f })
            {
                Blob(shrine, "Round window frame", V(x, 1.37f, -1.13f), V(0.65f, 0.64f, 0.22f), darkWood);
                Blob(shrine, "Round window glass", V(x, 1.37f, -1.24f), V(0.49f, 0.49f, 0.13f), window);
                Shape(shrine, "Window mullion", PrimitiveType.Cube, V(x, 1.37f, -1.315f), V(0.055f, 0.48f, 0.045f), bark);
                Shape(shrine, "Window mullion", PrimitiveType.Cube, V(x, 1.37f, -1.315f), V(0.48f, 0.055f, 0.045f), bark);
            }
            Lantern(set, V(-2.62f, 0, 3.2f), 1.95f, window);
            Lantern(set, V(2.85f, 0, 3.9f), 2.28f, window);
        }

        void Lantern(Transform parent, Vector3 at, float height, Material window)
        {
            var post = Group(parent, "Path lantern", at);
            Form(post, "Stone footing", tile, V(0, 0.12f, 0), V(0.28f, 0.3f, 0.24f), slate);
            Link(post, "Cedar post", V(0, 0.17f, 0), V(0, height, 0), 0.13f, bark);
            Link(post, "Lantern arm", V(-0.18f, height, 0), V(0.42f, height, 0), 0.095f, bark);
            Blob(post, "Amber glass globe", V(0.35f, height - 0.31f, 0), V(0.35f, 0.41f, 0.33f), window);
            Form(post, "Copper canopy", roof, V(0.35f, height - 0.12f, 0), V(0.24f, 0.22f, 0.24f), clay);
            Form(post, "Metal cradle", tile, V(0.35f, height - 0.49f, 0), V(0.15f, 0.08f, 0.15f), bark);
        }

        void PlantGrove(Transform set)
        {
            Tree(set, V(-5.7f, 0, 1.9f), 1.38f, jade, clover);
            Tree(set, V(5.5f, 0, 2.15f), 1.45f, clover, olive);
            Tree(set, V(-4.2f, 0, 6.3f), 1.54f, clay, honey);
            Tree(set, V(4.25f, 0, 7.2f), 1.62f, jade, olive);
            Tree(set, V(-7.1f, 0, 7.9f), 1.5f, fir, jade);
            Tree(set, V(7.5f, 0, 7.2f), 1.6f, fir, clover);
            for (int i = 0; i < 9; i++)
                Tree(set, V(i * 2.6f - 10f, 0, Between(9.4f, 12.1f)), Between(0.88f, 1.92f), Pick(jade, fir, clover), Pick(jade, clover));

            var plants = Group(set, "Meadow flowers and ferns");
            for (int i = 0; i < 96; i++)
            {
                float x = Between(-8, 8), z = Between(-4.6f, 8.3f);
                if (Mathf.Abs(x) < 3.75f && z is > -2.1f and < 2.15f) continue;
                if (Mathf.Abs(x) < 1.1f || (z > 4 && Mathf.Abs(x) < 2.6f)) continue;
                Tuft(plants, V(x, 0.055f, z), Between(0.65f, 1.38f), i % 3 == 0);
            }
            Mushrooms(set, V(-4.25f, 0, -1.75f));
            Mushrooms(set, V(4.25f, 0, -2.25f));
            Mushrooms(set, V(-3.44f, 0, 3.5f));
            for (int i = 0; i < 13; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Tuft(plants, V(side * Between(2.7f, 4.4f), 0.015f, Between(-3.6f, -2.4f)),
                    Between(0.85f, 1.55f), true);
            }
        }

        void Tree(Transform set, Vector3 position, float size, Material leaves, Material tips)
        {
            var tree = Group(set, "Broadleaf tree", position);
            tree.localScale = Vector3.one * size;
            tree.localRotation = Quaternion.Euler(0, Between(-28, 28), 0);
            Link(tree, "Flared trunk", V(0, -0.09f, 0), V(0.16f, 2.66f, 0), 0.47f, bark);
            Link(tree, "Fork", V(0.06f, 1.2f, 0), V(-0.91f, 2.54f, 0.12f), 0.23f, bark);
            Link(tree, "Fork", V(0.1f, 1.37f, 0), V(0.85f, 2.47f, 0.22f), 0.23f, bark);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2;
                var root = Blob(tree, "Exposed root", V(Mathf.Sin(a) * 0.25f, 0.035f, Mathf.Cos(a) * 0.25f),
                    V(0.23f, 0.22f, 0.71f), bark);
                root.localRotation = Quaternion.Euler(0, i * 90, 0);
            }
            Blob(tree, "Main crown", V(0, 2.98f, 0), V(2.35f, 2.15f, 2.18f), leaves);
            Blob(tree, "Side crown", V(-0.95f, 2.59f, 0.1f), V(1.85f, 1.64f, 1.59f), leaves);
            Blob(tree, "Side crown", V(0.93f, 2.64f, 0.3f), V(1.73f, 1.58f, 1.56f), leaves);
            Blob(tree, "Sunlit crown", V(-0.41f, 3.57f, 0.01f), V(1.77f, 1.31f, 1.6f), tips);
            Blob(tree, "Sunlit crown", V(0.65f, 3.08f, -0.34f), V(1.42f, 1.05f, 1.36f), tips);
        }

        void Tuft(Transform parent, Vector3 at, float size, bool flower)
        {
            var tuft = Group(parent, flower ? "Wildflower" : "Grass tuft", at);
            tuft.localScale = Vector3.one * size;
            tuft.localRotation = Quaternion.Euler(0, Between(0, 360), 0);
            var green = Pick(jade, clover, olive);
            for (int i = -1; i <= 1; i++)
                Blob(tuft, "Leaf", V(i * 0.075f, 0.105f, 0), V(0.07f, 0.25f, 0.055f), green)
                    .localRotation = Quaternion.Euler(0, 0, -i * 34f);
            if (!flower) return;
            var petals = Pick(clay, cream, honey);
            Link(tuft, "Flower stem", V(0, 0, 0), V(0, 0.24f, 0), 0.025f, jade);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 0.4f;
                Blob(tuft, "Petal", V(Mathf.Sin(a) * 0.065f, 0.247f, Mathf.Cos(a) * 0.065f),
                    V(0.082f, 0.055f, 0.082f), petals);
            }
            Blob(tuft, "Pollen center", V(0, 0.267f, 0), V(0.079f, 0.055f, 0.079f), honey);
        }

        void Mushrooms(Transform parent, Vector3 at)
        {
            for (int i = 0; i < 3; i++)
            {
                var stem = Group(parent, "Amber mushroom", at + V(i * 0.25f, 0, i % 2 * 0.24f));
                stem.localScale = Vector3.one * (0.98f - 0.2f * i);
                Blob(stem, "Ivory stalk", V(0, 0.19f, 0), V(0.14f, 0.39f, 0.15f), cream);
                Form(stem, "Mushroom cap", brim, V(0, 0.36f, 0), V(0.25f, 0.19f, 0.25f), i == 1 ? honey : clay);
                for (int j = 0; j < 4; j++)
                {
                    float a = j * Mathf.PI / 2;
                    Blob(stem, "Cap fleck", V(Mathf.Sin(a) * 0.118f, 0.439f, Mathf.Cos(a) * 0.117f),
                        V(0.046f, 0.025f, 0.05f), cream);
                }
            }
        }

        (Transform, Transform, Transform, Renderer) BuildCaster()
        {
            var caster = Group(null, "Milo • grove spellkeeper", V(2.38f, -0.004f, -0.30f));
            caster.localRotation = Quaternion.Euler(0, 202, 0);
            var body = Group(caster, "Casting body");
            var coat = Paint("Petrol blue cloth", "3F817D");
            var hat = Paint("Spruce felt", "357468");
            var fluff = Paint("Warm fur", "DCA270");
            var shoes = Paint("Dark leather", "68483D");
            var scarf = Paint("Burnt orange wool", "D87142");
            var gemMaterial = Paint("Copper brooch crystal", "84BEA4", 0.1f);
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                Blob(caster, "Curled boot", V(x, 0.16f, 0.15f), V(0.35f, 0.31f, 0.52f), shoes);
                Blob(caster, "Gaiter", V(x, 0.39f, 0.02f), V(0.23f, 0.51f, 0.26f), ink);
                Blob(caster, "Boot cuff", V(x, 0.34f, 0.066f), V(0.32f, 0.19f, 0.33f), bark);
            }
            Form(body, "Heavy wool coat", cap, V(0, 0.80f, 0), V(0.58f, 1.05f, 0.39f), coat);
            Form(body, "Coat hem", brim, V(0, 0.38f, 0), V(0.50f, 0.13f, 0.34f), hat);
            Blob(body, "Shoulders", V(0, 1.14f, 0), V(0.86f, 0.56f, 0.65f), coat);
            Blob(body, "Left sleeve", V(-0.39f, 0.99f, 0.075f), V(0.32f, 0.61f, 0.32f), coat)
                .localRotation = Quaternion.Euler(-16, 0, -25);
            Blob(body, "Left mitten", V(-0.49f, 0.76f, 0.19f), V(0.26f, 0.24f, 0.26f), fluff);
            var sleeve = Blob(body, "Articulated casting sleeve", V(0.5f, 1, 0.18f), Vector3.one, coat);
            var palm = Group(body, "Casting hand");
            Blob(palm, "Wool cuff", V(-0.04f, 0.015f, -0.053f), V(0.28f, 0.26f, 0.22f), ink);
            Blob(palm, "Open mitten", V(0.035f, -0.03f, -0.015f), V(0.26f, 0.19f, 0.24f), fluff);
            Blob(palm, "Thumb", V(-0.008f, 0.052f, 0.041f), V(0.11f, 0.12f, 0.116f), fluff);
            Blob(body, "Scarf collar", V(0, 1.32f, 0.04f), V(0.77f, 0.31f, 0.68f), scarf);
            Blob(body, "Scarf tail", V(0.27f, 1.12f, 0.30f), V(0.20f, 0.54f, 0.12f), scarf)
                .localRotation = Quaternion.Euler(0, 0, 24);
            Blob(body, "Scarf knot", V(0.20f, 1.3f, 0.35f), V(0.27f, 0.25f, 0.17f), clay);
            var gem = Blob(body, "Enamel scarf pin", V(0.16f, 1.27f, 0.433f), V(0.12f, 0.13f, 0.057f), gemMaterial)
                .GetComponent<Renderer>();

            Blob(body, "Round head", V(0, 1.66f, 0.07f), V(0.74f, 0.70f, 0.67f), fluff);
            foreach (float x in new[] { -1f, 1f })
            {
                Blob(body, "Ear", V(x * 0.34f, 1.65f, 0.10f), V(0.23f, 0.24f, 0.29f), fluff);
                Blob(body, "Cheek", V(x * 0.166f, 1.52f, 0.345f), V(0.22f, 0.13f, 0.11f), cream);
                Blob(body, "Eye", V(x * 0.133f, 1.67f, 0.378f), V(0.075f, 0.106f, 0.036f), ink);
                Blob(body, "Eye catchlight", V(x * 0.133f - 0.013f, 1.692f, 0.397f), V(0.022f, 0.023f, 0.015f), cream);
            }
            Blob(body, "Button nose", V(0, 1.59f, 0.408f), V(0.141f, 0.098f, 0.094f), bark);
            var hatRoot = Group(body, "Folded forest hat", V(0, 1.94f, 0.04f));
            hatRoot.localRotation = Quaternion.Euler(-8, 0, -12);
            Form(hatRoot, "Wide curved brim", brim, Vector3.zero, V(0.69f, 0.20f, 0.54f), hat);
            Form(hatRoot, "Hat crown", cap, V(0.04f, 0.25f, 0), V(0.52f, 0.63f, 0.41f), hat);
            Form(hatRoot, "Woven hatband", brim, V(0.017f, 0.084f, 0), V(0.47f, 0.13f, 0.35f), Paint("Hatband", "84634A"));
            Blob(hatRoot, "Brass clasp", V(-0.14f, 0.105f, 0.34f), V(0.205f, 0.14f, 0.075f), honey);
            Blob(hatRoot, "Pressed leaf", V(0.42f, 0.24f, 0.27f), V(0.11f, 0.34f, 0.055f), olive)
                .localRotation = Quaternion.Euler(9, 0, -35);
            return (body, sleeve, palm, gem);
        }

        (Transform, Collider, Transform) BuildSentinel()
        {
            var root = Group(null, "Pebble • moss sentinel", V(-2.4f, -0.009f, 0.68f));
            root.localRotation = Quaternion.Euler(0, 148, 0);
            var body = Group(root, "Recoil body");
            var stone = Paint("Pale warm limestone", "B7B29B");
            var dark = Paint("Stone joints", "6C796E");
            var core = Paint("Amber resin", "E7A45D", 0.4f);
            foreach (float x in new[] { -1f, 1f })
            {
                Form(root, "Broad stone boot", tile, V(x * 0.34f, 0.19f, 0.17f), V(0.35f, 0.36f, 0.49f), dark);
                Blob(root, "Leg joint", V(x * 0.35f, 0.46f, 0), V(0.43f, 0.46f, 0.47f), stone);
                Form(body, "Rounded stone shoulder", tile, V(x * 0.65f, 1.13f, 0), V(0.34f, 0.59f, 0.38f), stone)
                    .localRotation = Quaternion.Euler(0, 0, x * 20f);
                Form(body, "Heavy stone fist", tile, V(x * 0.82f, 0.78f, 0.075f), V(0.294f, 0.49f, 0.31f), dark);
                Blob(body, "Shoulder moss", V(x * 0.64f, 1.40f, -0.015f), V(0.542f, 0.19f, 0.59f), jade);
            }

            Form(body, "Stone breastplate", tile, V(0, 0.99f, 0), V(0.63f, 1.0f, 0.44f), stone);
            Blob(body, "Chest medallion", V(0, 1.09f, 0.425f), V(0.53f, 0.54f, 0.15f), dark);
            Blob(body, "Amber chest core", V(0, 1.09f, 0.503f), V(0.36f, 0.37f, 0.11f), core);
            Blob(body, "Core highlight", V(-0.075f, 1.19f, 0.548f), V(0.09f, 0.065f, 0.044f), cream);
            Form(body, "Stone head", tile, V(0, 1.69f, 0.055f), V(0.57f, 0.75f, 0.42f), stone);
            Blob(body, "Carved visor", V(0, 1.65f, 0.43f), V(0.72f, 0.25f, 0.17f), ink);
            foreach (float x in new[] { -0.145f, 0.145f })
                Blob(body, "Amber eye", V(x, 1.66f, 0.51f), V(0.106f, 0.104f, 0.045f), core);
            Blob(body, "Moss cushion", V(-0.06f, 2.02f, 0.046f), V(0.87f, 0.26f, 0.69f), jade);
            Blob(body, "Fresh moss", V(0.24f, 2.08f, 0.11f), V(0.47f, 0.19f, 0.45f), clover);
            Tuft(body, V(-0.16f, 2.09f, 0.02f), 0.77f, true);
            Tuft(body, V(0.51f, 1.49f, 0.09f), 0.64f, false);

            var hit = Group(body, "Sentinel hit surface", V(0, 1.10f, 0));
            var receiver = hit.gameObject.AddComponent<BoxCollider>();
            receiver.size = V(0.98f, 0.96f, 0.87f);
            var aim = Group(hit, "Chest aim point", V(0, 0, 0.40f));
            return (body, receiver, aim);
        }

        Rigidbody[] BuildPhysicsProps(Transform set)
        {
            var props = Group(set, "Loose testing stones");
            var bodies = new Rigidbody[3];
            for (int i = 0; i < bodies.Length; i++)
            {
                var stone = Form(props, "Loose stone", tile, V(-3.0f - i * 0.49f, 0.13f, -0.83f + (i % 2) * 0.51f),
                    V(0.18f, 0.24f, 0.17f), Pick(slate, foam, cream));
                stone.gameObject.AddComponent<BoxCollider>().size = V(1.6f, 1, 1.6f);
                bodies[i] = stone.gameObject.AddComponent<Rigidbody>();
                bodies[i].mass = 0.68f;
            }
            return bodies;
        }

        static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        static Vector3 V(float x, float y, float z) => new(x, y, z);
        float Between(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        T Pick<T>(params T[] values) => values[random.Next(values.Length)];

        static Transform Group(Transform parent, string name, Vector3 position = default)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            transform.localPosition = position;
            return transform;
        }

        static Transform Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        static Transform Blob(Transform parent, string name, Vector3 position, Vector3 size, Material material)
            => Shape(parent, name, PrimitiveType.Sphere, position, size, material);

        static void Link(Transform parent, string name, Vector3 start, Vector3 end, float width, Material material)
        {
            var delta = end - start;
            var piece = Blob(parent, name, (end + start) / 2, V(width, delta.magnitude + width, width), material);
            piece.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
        }

        static Transform Form(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var transform = Group(parent, name, position);
            transform.localScale = scale;
            transform.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            transform.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return transform;
        }

        static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }

        Material Paint(string name, string color, float emission = 0f)
        {
            if (palette.TryGetValue(name, out var existing)) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", Hex(color));
            material.SetFloat("_Smoothness", 0.21f);
            if (emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Hex(color) * emission);
            }
            AssetDatabase.CreateAsset(material, $"{Folder}/{name}.mat");
            palette[name] = material;
            return material;
        }

        static Mesh Profile(string name, int sides, float offset, params Vector2[] rings)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row < rings.Length; row++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * (2 * Mathf.PI / sides) + offset;
                    vertices.Add(V(Mathf.Sin(angle) * rings[row].x, rings[row].y, Mathf.Cos(angle) * rings[row].x));
                    if (row == 0) continue;
                    int previous = (row - 1) * sides + side;
                    int next = (row - 1) * sides + (side + 1) % sides;
                    triangles.AddRange(new[] { previous, next, previous + sides, next, next + sides, previous + sides });
                }
            }
            int bottom = vertices.Count;
            vertices.Add(V(0, rings[0].y, 0));
            int top = vertices.Count;
            vertices.Add(V(0, rings[^1].y, 0));
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int last = (rings.Length - 1) * sides;
                triangles.AddRange(new[] { bottom, next, side, top, last + side, last + next });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, $"{Folder}/{name}.asset");
            return mesh;
        }
    }
}
