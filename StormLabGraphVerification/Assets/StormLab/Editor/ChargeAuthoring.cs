using System.IO;
using StormLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

static class ChargeAuthoring
{
    const string Root = "Assets/StormLab";
    const string Folder = Root + "/Charge";

    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(Root + "/StormLab.unity");
        if (Object.FindAnyObjectByType<ChargeSequence>() != null)
            throw new System.InvalidOperationException("The scene already contains its charge sequence.");

        var frame = Lit("Gauntlet body", new Color(0.15f, 0.23f, 0.27f), Color.black);
        var plate = Lit("Gauntlet plates", new Color(0.56f, 0.49f, 0.36f), Color.black);
        var ice = new Color(0.34f, 0.72f, 1f);
        var indicator = Lit("Gauntlet indicator", new Color(0.25f, 0.61f, 0.77f), ice * 0.45f);
        var light = Lit("Discharge head", new Color(0.9f, 0.96f, 1f), new Color(0.64f, 0.86f, 1f) * 5.3f);

        var graph = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Shaders/Graphs/Arc Erosion.mat");
        var whiteGraph = new Material(graph);
        whiteGraph.SetColor("_Tint", new Color(0.78f, 0.92f, 1f));
        whiteGraph.SetFloat("_CoreGain", 5.5f);
        whiteGraph.SetFloat("_FastDensity", 24f);
        whiteGraph.SetFloat("_SlowDensity", 8f);
        AssetDatabase.CreateAsset(whiteGraph, Folder + "/White Charge Ribbons.mat");
        var sparkMaterial = CreateMaterial("Charge Sparks", Shader.Find("StormLab/Charge Sparks"));

        var fixture = new GameObject("Hand Charge Fixture");
        var emitter = GameObject.Find("Discharge emitter").transform;
        fixture.transform.SetParent(emitter, false);
        fixture.transform.position = GameObject.Find("Induction chamber").transform.position;
        var glove = new GameObject("Open right gauntlet").transform;
        glove.SetParent(fixture.transform, false);
        glove.localPosition = new Vector3(-0.18f, 0.87f, -0.02f);
        glove.localRotation = Quaternion.Euler(-9, -43, -12);

        Part(glove, "Forearm housing", PrimitiveType.Cube, new Vector3(0, -0.58f, 0.035f),
            new Vector3(0.38f, 0.65f, 0.36f), frame);
        Part(glove, "Wrist hinge", PrimitiveType.Sphere, new Vector3(0, -0.30f, 0.015f),
            new Vector3(0.40f, 0.26f, 0.38f), plate);
        Part(glove, "Palm housing", PrimitiveType.Cube, new Vector3(0, 0.02f, 0),
            new Vector3(0.58f, 0.52f, 0.24f), frame);
        Part(glove, "Palm plate", PrimitiveType.Cube, new Vector3(0, 0.02f, -0.123f),
            new Vector3(0.47f, 0.42f, 0.064f), plate);
        Part(glove, "Induction pad", PrimitiveType.Sphere, new Vector3(0, 0.06f, -0.17f),
            new Vector3(0.23f, 0.23f, 0.09f), indicator);
        Finger(glove, "Thumb", new Vector3(-0.25f, -0.15f, -0.01f), new Vector3(-0.45f, -0.01f, -0.062f),
            new Vector3(-0.56f, 0.18f, -0.13f), plate, frame);
        float[] lengths = { 0.47f, 0.55f, 0.48f, 0.36f };
        for (int i = 0; i < lengths.Length; i++)
        {
            float x = -0.203f + 0.135f * i;
            float spread = (i - 1.5f) * 0.060f;
            Finger(glove, $"Finger {i + 1}", new Vector3(x, 0.245f, 0),
                new Vector3(x + spread * 0.48f, 0.245f + lengths[i] * 0.53f, -0.018f),
                new Vector3(x + spread, 0.245f + lengths[i], -0.086f), plate, frame);
        }

        var palm = new GameObject("Palm FX anchor").transform;
        palm.SetParent(glove, false);
        palm.localPosition = new Vector3(0, 0.15f, -0.27f);
        var sparks = new GameObject("Inward charge sparks").AddComponent<ParticleSystem>();
        sparks.transform.SetParent(palm, false);
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false;
        main.loop = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.45f);
        main.startSpeed = -2.12f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.13f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.53f, 0.77f, 1f), new Color(1f, 0.88f, 0.66f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 160;
        var emission = sparks.emission;
        emission.rateOverTime = 95f;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.04f;
        shape.radiusThickness = 0.12f;
        var color = sparks.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.86f, 0.16f),
                    new GradientAlphaKey(0.95f, 0.66f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sparkMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.9f;
        renderer.velocityScale = 0.14f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var hand = fixture.AddComponent<HandCharge>();
        hand.Configure(palm, sparks, whiteGraph);

        var bolt = new GameObject("Traveling discharge");
        var trail = bolt.AddComponent<TrailRenderer>();
        trail.sharedMaterial = whiteGraph;
        trail.time = 0.15f;
        trail.minVertexDistance = 0.018f;
        trail.widthMultiplier = 0.12f;
        trail.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.enabled = trail.emitting = false;
        var head = Part(bolt.transform, "Discharge head", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.14f, light);
        head.enabled = false;

        var timings = ScriptableObject.CreateInstance<ChargeSequenceProfile>();
        AssetDatabase.CreateAsset(timings, Folder + "/Hand Charge Sequence.asset");
        var sequence = new GameObject("Charge → Travel → Impact").AddComponent<ChargeSequence>();
        bolt.transform.SetParent(sequence.transform, false);
        sequence.Configure(timings, hand, trail, head);

        var controls = new SerializedObject(Object.FindAnyObjectByType<StormControls>());
        controls.FindProperty("chargeSequence").objectReferenceValue = sequence;
        controls.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("STORM_CHARGE_AUTHORED");
    }

    public static void UpdateCulling()
    {
        var scene = EditorSceneManager.OpenScene(Root + "/StormLab.unity");
        var sparks = GameObject.Find("Inward charge sparks").GetComponent<ParticleSystem>();
        var main = sparks.main;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void Finger(Transform hand, string label, Vector3 root, Vector3 joint, Vector3 tip, Material plate, Material frame)
    {
        Bone(hand, label + " lower", root, joint, 0.107f, plate);
        Bone(hand, label + " upper", joint, tip, 0.087f, frame);
        Part(hand, label + " knuckle", PrimitiveType.Sphere, joint, Vector3.one * 0.108f, plate);
    }

    static void Bone(Transform root, string label, Vector3 start, Vector3 end, float thickness, Material material)
    {
        Vector3 direction = end - start;
        var mesh = Part(root, label, PrimitiveType.Cube, start + direction * 0.5f,
            new Vector3(thickness, direction.magnitude, thickness), material);
        mesh.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
    }

    static Renderer Part(Transform parent, string label, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = label;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        return renderer;
    }

    static Material Lit(string label, Color baseColor, Color emission)
    {
        var mat = CreateMaterial(label, Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", 0.35f);
        mat.SetFloat("_Smoothness", 0.43f);
        if (emission.maxColorComponent > 0)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateMaterial(string label, Shader shader)
    {
        if (!shader) throw new System.InvalidOperationException($"Shader for {label} was not imported.");
        var mat = new Material(shader) { name = label };
        AssetDatabase.CreateAsset(mat, $"{Folder}/{label}.mat");
        return mat;
    }
}
