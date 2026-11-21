using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NoggiDemo.Editor
{
    static class DemoScene
    {
        const string Root = "Assets/NoggiDemo";
        const string Generated = Root + "/Generated";
        const string ScenePath = Root + "/NoggiLighting.unity";

        [MenuItem("Noggi Demo/Create or open sample scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            ConfigurePipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sdfPath = Root + "/Textures/sdf_texture_generic.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(sdfPath);
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var face = Material("Face - Noggi SDF", "Noggi Demo/Face SDF (URP)");
            face.SetTexture("_FaceSdf", AssetDatabase.LoadAssetAtPath<Texture2D>(sdfPath));
            face.SetColor("_LitColor", ColorHex("#F6BEAA"));
            face.SetColor("_ShadeColor", ColorHex("#AC6373"));
            face.SetFloat("_Feather", 0.009f);
            var outline = Material("Face - Backface hull", "Noggi Demo/Face Outline (URP)");
            outline.SetColor("_OutlineColor", ColorHex("#191D39"));
            outline.SetFloat("_OutlineWidth", .019f);

            var bodyInk = Toon("Jacket - Unity Toon Shader", "#477BA2", "#294875", "#222849", 45);
            var bodyClean = Toon("Jacket - No outline", "#477BA2", "#294875", "#222849", 0);
            var hair = Toon("Hair", "#43567E", "#283351", "#202641", 0);
            var neck = Toon("Neck", "#F2B59F", "#B3747B", "#8F5465", 0);
            var collar = Toon("Collar", "#85ACB8", "#507891", "#3B526F", 0);
            var ink = Flat("Features", "#28283F");
            var eye = Flat("Eye white", "#FFF2DD");
            var iris = Flat("Iris", "#4B9D9B");
            var catchlight = Flat("Eye highlight", "#FFFAEC");
            var rose = Flat("Cheek detail", "#D88888");
            var brass = Flat("Brass detail", "#F5BE77");
            var halo = Flat("Studio halo", "#536987");
            var plate = Flat("Studio plinth", "#283751");

            var studio = new GameObject("Studio").transform;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(studio);
            camera.transform.position = new Vector3(-.46f, 1.48f, -7);
            camera.orthographic = true;
            camera.orthographicSize = 1.42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorHex("#202D47");
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var key = new GameObject("Main Directional Light").AddComponent<Light>();
            key.transform.SetParent(studio);
            key.type = LightType.Directional;
            key.intensity = 1.0f;
            RenderSettings.sun = key;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ColorHex("#9095A6");

            var subject = new GameObject("Aster - Lighting Sample").transform;
            var torso = Orb(subject, "Jacket | Unity Toon Shader", new Vector3(0, .74f, .08f), new Vector3(1.70f, 1.16f, .89f), bodyInk);
            Orb(subject, "Collar", new Vector3(0, 1.16f, .035f), new Vector3(.67f, .30f, .57f), collar);
            Orb(subject, "Neck", new Vector3(0, 1.28f, -.035f), new Vector3(.37f, .49f, .37f), neck);
            Orb(subject, "Jacket zipper", new Vector3(0, .79f, -.368f), new Vector3(.037f, .45f, .03f), ink);
            Orb(subject, "Zipper pull", new Vector3(0, .98f, -.366f), new Vector3(.09f, .078f, .037f), brass);
            Orb(subject, "Jacket pin", new Vector3(.39f, .86f, -.311f), new Vector3(.11f, .11f, .044f), brass);

            var head = new GameObject("Head | local forward is -Z").transform;
            head.SetParent(subject);
            head.localPosition = new Vector3(0, 1.80f, 0);
            var mesh = HeadMesh();
            AssetDatabase.CreateAsset(mesh, Generated + "/FaceUV.asset");
            MeshObject(head, "Face | frontal UV0", mesh, face);
            var faceHull = MeshObject(head, "Face outline | inverted hull", mesh, outline);

            Orb(head, "Hair cap", new Vector3(0, .27f, .141f), new Vector3(1.23f, .91f, 1.04f), hair);
            Orb(head, "Hair lock left", new Vector3(-.424f, .271f, -.216f), new Vector3(.28f, .69f, .42f), hair, -22);
            Orb(head, "Hair lock right", new Vector3(.448f, .280f, -.197f), new Vector3(.27f, .66f, .44f), hair, 25);
            Orb(head, "Swept fringe", new Vector3(-.175f, .423f, -.351f), new Vector3(.76f, .29f, .32f), hair, 26);
            Orb(head, "Fringe tip", new Vector3(.205f, .477f, -.319f), new Vector3(.58f, .27f, .30f), hair, -17);
            Orb(head, "Hair clip", new Vector3(.382f, .425f, -.440f), new Vector3(.19f, .034f, .025f), brass, -21);

            foreach (int side in new[] { -1, 1 })
            {
                float x = side * .203f;
                Orb(head, "Upper eye shape", new Vector3(x, .061f, -.401f), new Vector3(.261f, .157f, .096f), ink, -side * 5);
                Orb(head, "Eye white", new Vector3(x, .048f, -.435f), new Vector3(.218f, .11f, .055f), eye, -side * 5);
                Orb(head, "Iris", new Vector3(x + .016f, .05f, -.461f), new Vector3(.097f, .106f, .032f), iris);
                Orb(head, "Pupil", new Vector3(x + .021f, .047f, -.475f), new Vector3(.043f, .069f, .021f), ink);
                Orb(head, "Eye glint", new Vector3(x + .004f, .072f, -.482f), new Vector3(.025f, .026f, .014f), catchlight);
                Orb(head, "Brow", new Vector3(x, .190f, -.384f), new Vector3(.24f, .036f, .068f), ink, -side * 8);
                Orb(head, "Cheek mark", new Vector3(side * .306f, -.096f, -.362f), new Vector3(.10f, .029f, .058f), rose, -side * 9);
            }
            Orb(head, "Mouth", new Vector3(0, -.235f, -.38f), new Vector3(.135f, .031f, .09f), ink, 5);

            Orb(studio, "Halo", new Vector3(0, 1.52f, 1.3f), new Vector3(2.33f, 2.33f, .13f), halo);
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Plinth";
            pedestal.transform.SetParent(studio);
            pedestal.transform.localPosition = new Vector3(0, .235f, .12f);
            pedestal.transform.localScale = new Vector3(1.81f, .12f, 1.30f);
            UnityEngine.Object.DestroyImmediate(pedestal.GetComponent<Collider>());
            pedestal.GetComponent<Renderer>().sharedMaterial = plate;

            var controls = new GameObject("Play Mode Lighting Controls").AddComponent<LightingControls>();
            controls.Configure(key, torso, bodyInk, bodyClean, faceHull);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = subject.gameObject;
            Debug.Log("NOGGI_SCENE_CREATED " + ScenePath);
        }

        static void ConfigurePipeline()
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, Generated + "/ForwardRenderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.msaaSampleCount = 4;
            AssetDatabase.CreateAsset(pipeline, Generated + "/URP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            AssetDatabase.SaveAssets();
        }

        static Color ColorHex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color color))
                throw new ArgumentException(hex);
            return color;
        }

        static Material Material(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (!shader)
                throw new InvalidOperationException($"Missing shader {shaderName}");
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, $"{Generated}/{name}.mat");
            return mat;
        }

        static Material Toon(string name, string lit, string shade, string deep, float outline)
        {
            var mat = Material(name, "Toon/Toon");
            mat.SetColor("_BaseColor", ColorHex(lit));
            mat.SetColor("_1st_ShadeColor", ColorHex(shade));
            mat.SetColor("_2nd_ShadeColor", ColorHex(deep));
            mat.SetFloat("_BaseColor_Step", .53f);
            mat.SetFloat("_1st_ShadeColor_Step", .28f);
            mat.SetFloat("_BaseShade_Feather", .018f);
            mat.SetFloat("_1st2nd_Shades_Feather", .022f);
            mat.SetFloat("_Outline_Width", outline);
            mat.SetFloat("_Farthest_Distance", 30);
            mat.SetFloat("_Nearest_Distance", 1);
            mat.SetColor("_Outline_Color", ColorHex("#191D39"));
            mat.EnableKeyword("_OUTLINE_NML");
            if (outline == 0)
                mat.EnableKeyword("_DISABLE_OUTLINE");
            mat.SetShaderPassEnabled("SRPDefaultUnlit", outline > 0);
            return mat;
        }

        static Material Flat(string name, string hex)
        {
            var mat = Material(name, "Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", ColorHex(hex));
            return mat;
        }

        static MeshRenderer Orb(Transform parent, string name, Vector3 position, Vector3 scale, Material mat, float roll = 0)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(0, 0, roll);
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            return renderer;
        }

        static MeshRenderer MeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            var ob = new GameObject(name);
            ob.transform.SetParent(parent, false);
            ob.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ob.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        static Mesh HeadMesh()
        {
            const int segments = 80;
            const int bands = 48;
            const int stride = segments + 1;
            var points = new Vector3[stride * (bands + 1)];
            var uv = new Vector2[points.Length];
            var tris = new int[segments * bands * 6];

            for (int row = 0; row <= bands; row++)
            {
                float latitude = Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, row / (float)bands);
                float yNormalized = Mathf.Sin(latitude);
                float radial = Mathf.Max(.00001f, Mathf.Cos(latitude));
                for (int col = 0; col <= segments; col++)
                {
                    float longitude = col * (2 * Mathf.PI / segments);
                    float x = Mathf.Cos(longitude) * radial * .58f * Mathf.Lerp(.70f, 1.08f, Mathf.InverseLerp(-.9f, .55f, yNormalized));
                    float y = yNormalized * .635f;
                    float z = Mathf.Sin(longitude) * radial * .435f;
                    if (z < 0)
                        z -= .044f * Mathf.Exp(-42 * x * x - 90 * (y + .05f) * (y + .05f));
                    int index = row * stride + col;
                    points[index] = new Vector3(x, y, z);
                    uv[index] = new Vector2(.5f + x * .78f, .5f + y * .72f);
                }
            }
            int i = 0;
            for (int row = 0; row < bands; row++)
            for (int col = 0; col < segments; col++)
            {
                int a = row * stride + col;
                int b = a + 1;
                int c = a + stride;
                tris[i++] = a;
                tris[i++] = c;
                tris[i++] = b;
                tris[i++] = b;
                tris[i++] = c;
                tris[i++] = c + 1;
            }
            var mesh = new Mesh { name = "Face with planar SDF UV0", vertices = points, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            var normals = mesh.normals;
            for (int row = 0; row <= bands; row++)
            {
                int first = row * stride;
                int last = first + segments;
                normals[first] = normals[last] = (normals[first] + normals[last]).normalized;
            }
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
                throw new InvalidOperationException("URP is not active");
            var controls = UnityEngine.Object.FindFirstObjectByType<LightingControls>();
            var camera = Camera.main;
            foreach (string name in new[] { "Noggi Demo/Face SDF (URP)", "Noggi Demo/Face Outline (URP)", "Toon/Toon" })
            {
                var shader = Shader.Find(name);
                if (!shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("Shader failed validation: " + name);
            }
            var faceData = (TextureImporter)AssetImporter.GetAtPath(Root + "/Textures/sdf_texture_generic.png");
            if (faceData.sRGBTexture || faceData.mipmapEnabled || faceData.maxTextureSize != 4096)
                throw new InvalidOperationException("SDF was not imported as linear mask data");
            var dir = Path.Combine(Path.GetTempPath(), "noggi-unity-verify");
            Directory.CreateDirectory(dir);
            Capture(camera, controls, -60, true, dir + "/left.png");
            Capture(camera, controls, 0, true, dir + "/front.png");
            Capture(camera, controls, 60, true, dir + "/right.png");
            Capture(camera, controls, -60, false, dir + "/no-outline.png");
            Debug.Log("NOGGI_VERIFY_OK " + scene.name + " " + dir);
        }

        static void Capture(Camera camera, LightingControls controls, float angle, bool outline, string path)
        {
            controls.SetLighting(angle, outline);
            var target = RenderTexture.GetTemporary(992, 704, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}

