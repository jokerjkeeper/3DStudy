using System.Collections.Generic;
using System.IO;
using StormLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

static class LanternQuayAuthoring
{
    const string Folder = "Assets/StormLab/Battle";
    const string ScenePath = Folder + "/LanternQuay.unity";

    static Material mortar, trim, iron, glass, lantern, deck, sea, leaves, bark, terra, roof;
    static Material[] stone;

    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/StormLab/StormLab.unity");
        var controls = Object.FindAnyObjectByType<StormControls>();
        var sequence = Object.FindAnyObjectByType<ChargeSequence>();
        var camera = GameObject.Find("Range Camera").GetComponent<Camera>();
        var volume = Object.FindAnyObjectByType<Volume>();
        var fixture = GameObject.Find("Hand Charge Fixture").transform;
        var anchor = sequence.Hand.Palm;
        anchor.SetParent(fixture, false);
        Object.DestroyImmediate(fixture.Find("Open right gauntlet").gameObject);
        fixture.SetParent(null, false);
        Destroy("Discharge emitter", "Impact test bay", "Test range structure", "Warm wall wash", "Cool emitter wash");
        SetupCamera(camera);
        SetupLighting(volume);
        BuildStreet();
        var caster = BuildCaster(new Vector3(2.55f, 0.06f, -0.50f), out var arm, out var forearm, out var wrist, out var cuff);
        var target = BuildTarget(new Vector3(-2.78f, 0.06f, 1.15f), out var visual, out var chest, out var aim);
        caster.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target.position - caster.position, Vector3.up));
        target.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(caster.position - target.position, Vector3.up));
        fixture.SetParent(wrist, false);
        fixture.localPosition = new Vector3(0, 0.035f, 0.19f);
        fixture.localRotation = Quaternion.identity;
        anchor.localPosition = Vector3.zero;
        anchor.localRotation = Quaternion.identity;
        var pose = new GameObject("Battle choreography").AddComponent<BattlePose>();
        pose.Configure(sequence, arm, forearm, wrist, visual, chest);
        var bound = new SerializedObject(controls);
        bound.FindProperty("muzzle").objectReferenceValue = anchor;
        bound.FindProperty("testTarget").objectReferenceValue = aim;
        bound.FindProperty("emitter").objectReferenceValue = cuff;
        bound.FindProperty("bodies").arraySize = 0;
        bound.FindProperty("battlePose").objectReferenceValue = pose;
        bound.FindProperty("sceneTitle").stringValue = "Lantern Quay";
        bound.FindProperty("sceneDescription").stringValue = "Battle choreography";
        bound.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(item => item.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("STORM_BATTLE_CREATED");
    }

    static void SetupCamera(Camera view)
    {
        view.name = "Battle Camera";
        view.transform.position = new Vector3(2.80f, 4.4f, -10.35f);
        view.transform.LookAt(new Vector3(-0.25f, 1.43f, 1.65f));
        view.fieldOfView = 44f;
        view.farClipPlane = 100;
        view.backgroundColor = new Color(0.67f, 0.76f, 0.70f);
    }

    static void SetupLighting(Volume volume)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.63f, 0.67f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.039f;
        RenderSettings.fogColor = new Color(0.66f, 0.70f, 0.61f);

        var sun = GameObject.Find("Overhead wash").GetComponent<Light>();
        sun.name = "Low afternoon sun";
        sun.transform.rotation = Quaternion.Euler(32, -53, 0);
        sun.color = new Color(1f, 0.76f, 0.48f);
        sun.intensity = 1.72f;
        Point("Cool quay bounce", new Vector3(-4.2f, 3.18f, -2.6f), new Color(0.49f, 0.72f, 1f), 8.9f, 13);
        Point("Lantern window spill", new Vector3(4.72f, 2.70f, 3.18f), new Color(1f, 0.57f, 0.21f), 9.7f, 8);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, Folder + "/Quay Lighting.asset");
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 0.76f;
        bloom.threshold.value = 0.91f;
        bloom.scatter.value = 0.65f;
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.ACES;
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.28f;
        vignette.smoothness.value = 0.55f;
        var depth = profile.Add<DepthOfField>(true);
        depth.mode.value = DepthOfFieldMode.Gaussian;
        depth.gaussianStart.value = 16.4f;
        depth.gaussianEnd.value = 27.5f;
        depth.gaussianMaxRadius.value = 0.75f;
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
    }

    static void BuildStreet()
    {
        mortar = Lit("Deep mortar", 0x3B494B);
        trim = Lit("Sandstone trim", 0xB5A17D);
        iron = Lit("Wrought iron", 0x242F37, 0.48f);
        glass = Lit("Amber glass", 0x564138, 0.17f, new Color(1f, 0.34f, 0.07f) * 0.29f);
        lantern = Lit("Lantern flame", 0xE4A656, 0.11f, new Color(1f, 0.48f, 0.13f) * 2.45f);
        deck = Lit("Quay base", 0x6C6859);
        sea = Lit("Harbour water", 0x42676B, 0.41f);
        leaves = Lit("Olive leaves", 0x7D8C45);
        bark = Lit("Tree bark", 0x514137);
        terra = Lit("Terracotta wall", 0xAC7661);
        roof = Lit("Slate roof", 0x364D57, 0.19f);
        stone = new[] { Lit("Paving warm", 0xA69880), Lit("Paving pale", 0xAFA289), Lit("Paving ochre", 0x9C896E),
            Lit("Paving grey", 0x939689), Lit("Paving dark", 0x887969) };

        var root = new GameObject("Quay environment").transform;
        Box(root, "Water plane", new Vector3(0, -2.7f, 8), new Vector3(90, 0.20f, 90), sea);
        Box(root, "Battle terrace", new Vector3(0, -0.28f, 0.85f), new Vector3(18.8f, 0.56f, 10.4f), deck, true);
        Box(root, "Under-quay masonry", new Vector3(0, -1.25f, 0.75f), new Vector3(18.9f, 1.75f, 10.15f), mortar);
        Box(root, "Cantilever edge", new Vector3(0, -0.10f, -4.46f), new Vector3(19.05f, 0.20f, 0.40f), trim);
        Paving(root);
        for (int i = 0; i < 29; i++)
        {
            float x = -9.1f + i * 0.65f;
            Box(root, "Quay face block", new Vector3(x, -0.39f, -4.28f), new Vector3(0.63f, 0.36f, 0.16f), stone[i % stone.Length]);
        }

        Arcade(root, -7.9f, 6.9f, 3.30f, 5.8f, mortar, false);
        Arcade(root, -4.3f, 8.7f, 3.40f, 4.85f, deck, true);
        Arcade(root, 0.5f, 10.2f, 3.50f, 5.65f, terra, false);
        Arcade(root, 4.41f, 6.2f, 4.35f, 6.45f, deck, true);
        Arcade(root, 8.5f, 7.5f, 3.66f, 5.6f, terra, false);

        for (int i = 0; i < 11; i++)
        {
            float x = -22f + 4.6f * i;
            float height = 3.2f + (i * 7 % 6) * 0.72f;
            Box(root, "Distant harbour silhouette", new Vector3(x, -1.1f + height / 2, 21f + i % 4 * 2.8f),
                new Vector3(4.13f, height, 3.8f), i % 3 == 0 ? mortar : deck);
        }

        // A low stone rail and vegetation frame the shot while keeping the fight lane clear.
        Rail(root, -7.0f, -3.70f, 3);
        Rail(root, 5.35f, -3.85f, 4);
        Tree(root, new Vector3(-6.30f, 0, 3.55f), 1.16f, 517);
        Tree(root, new Vector3(7.70f, 0, 3.85f), 1.37f, 812);
        Tree(root, new Vector3(-7.65f, 0, -2.60f), 1.37f, 991);
        Lamp(root, new Vector3(-5.25f, 0, 2.85f));
        Lamp(root, new Vector3(5.28f, 0, 2.20f));
        for (int i = 0; i < 4; i++)
        {
            float x = 5.3f + i * 0.62f;
            Piece(root, "Cask", PrimitiveType.Cylinder, new Vector3(x, 0.34f, 4.37f), new Vector3(0.49f, 0.33f, 0.49f), bark);
            Piece(root, "Cask band", PrimitiveType.Cylinder, new Vector3(x, 0.42f, 4.37f), new Vector3(0.51f, 0.04f, 0.51f), iron);
        }
        for (int i = 0; i < 9; i++)
        {
            float x = -4.7f + i * 1.15f;
            Box(root, "Promenade inlay", new Vector3(x, 0.008f, 3.48f), new Vector3(0.78f, 0.014f, 0.10f), trim);
        }
    }

    static void Paving(Transform parent)
    {
        var rng = new System.Random(2078);
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var indices = new List<int>[stone.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = new List<int>();
        for (int row = 0; row < 29; row++)
        for (int column = 0; column < 40; column++)
        {
            float x = -9.24f + column * 0.461f + (row % 2) * 0.19f;
            float z = -4.17f + row * 0.344f;
            float width = 0.425f + (float)rng.NextDouble() * 0.025f;
            float length = 0.308f + (float)rng.NextDouble() * 0.025f;
            float bevel = 0.042f;
            int first = vertices.Count;
            vertices.AddRange(new[] {
                new Vector3(x + bevel, 0.006f, z), new Vector3(x, 0.006f, z + bevel),
                new Vector3(x, 0.006f, z + length - bevel), new Vector3(x + bevel, 0.006f, z + length),
                new Vector3(x + width - bevel, 0.006f, z + length), new Vector3(x + width, 0.006f, z + length - bevel),
                new Vector3(x + width, 0.006f, z + bevel), new Vector3(x + width - bevel, 0.006f, z)
            });
            for (int k = 0; k < 8; k++) uv.Add(Vector2.zero);
            var surface = indices[rng.Next(indices.Length)];
            for (int k = 1; k < 7; k++) surface.AddRange(new[] { first, first + k, first + k + 1 });
        }
        var mesh = new Mesh { name = "Chamfered paving stones" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = stone.Length;
        for (int i = 0; i < stone.Length; i++) mesh.SetTriangles(indices[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Folder + "/Quay Paving.asset");
        var obj = Pivot(parent, "Paving stonework", Vector3.zero);
        obj.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.gameObject.AddComponent<MeshRenderer>().sharedMaterials = stone;
    }

    static void Arcade(Transform environment, float x, float z, float width, float height, Material material, bool civic)
    {
        var parent = Pivot(environment, civic ? "Arcaded guild house" : "Harbour townhouse", new Vector3(x, 0, z));
        Box(parent, "Wall mass", new Vector3(0,height / 2,0), new Vector3(width,height,2.8f), material);
        for (int level = 0; level < 4; level++)
        {
            float y = level == 0 ? 0.22f : level == 1 ? 2.45f : level == 2 ? height - 0.36f : height;
            Box(parent, "Stone stringcourse", new Vector3(0,y,-1.41f), new Vector3(width + 0.24f, 0.17f, 0.30f), trim);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Box(parent, "Corner pilaster", new Vector3(side * (width / 2 - 0.14f),height * 0.5f,-1.49f), new Vector3(0.26f,height,0.29f), trim);
            var slab = Box(parent, "Sloping slate", new Vector3(side * width * 0.265f,height + 0.27f,0.015f),
                new Vector3(width * 0.58f, 0.16f, 3.20f), roof);
            slab.transform.localRotation = Quaternion.Euler(0, 0, side * -22f);
        }
        for (int unit = -1; unit <= 1; unit++)
        {
            float wx = unit * width * 0.28f;
            ArchWindow(parent, wx, 0.33f, civic ? 0.49f : 0.39f, 1.44f, unit == 0 ? lantern : glass);
            ArchWindow(parent, wx, 2.98f, 0.34f, 1.05f, glass);
            if (height > 5.6f)
                Box(parent, "Loft aperture", new Vector3(wx, height - 0.89f,-1.445f), new Vector3(0.41f,0.54f,0.10f), iron);
        }
        if (civic)
        {
            Box(parent, "Balcony ledge", new Vector3(0, 2.80f, -1.67f), new Vector3(width * 0.9f, 0.15f, 0.67f), trim);
            Box(parent, "Balcony top rail", new Vector3(0, 3.18f, -1.95f), new Vector3(width * 0.9f, 0.085f, 0.09f), iron);
            for (int i = 0; i < 14; i++)
                Box(parent, "Baluster", new Vector3((i - 6.5f) * width / 16, 2.99f, -1.95f), new Vector3(0.056f, 0.33f, 0.063f), iron);
        }
    }

    static void ArchWindow(Transform parent, float x, float bottom, float radius, float body, Material window)
    {
        float y = bottom + body;
        Box(parent, "Window recess", new Vector3(x, bottom + body / 2,-1.423f), new Vector3(radius*1.92f,body,0.095f), glass);
        var crown = Piece(parent, "Arched glass", PrimitiveType.Cylinder, new Vector3(x, y,-1.455f), new Vector3(radius*1.9f,0.045f,radius*1.9f), window);
        crown.transform.localRotation = Quaternion.Euler(90,0,0);
        Box(parent, "Window glazing", new Vector3(x, bottom + body / 2,-1.478f), new Vector3(radius*1.63f,body*0.98f,0.041f), window);
        Box(parent, "Window mullion", new Vector3(x, bottom + (body+radius)*0.5f,-1.535f), new Vector3(0.049f,body+radius,0.050f), iron);
        Box(parent, "Window transom", new Vector3(x,y - body*0.42f,-1.541f), new Vector3(radius*1.9f,0.045f,0.045f), iron);
        foreach (float side in new[] { -1f, 1f })
            Box(parent, "Arch jamb", new Vector3(x + side*radius,bottom+body/2,-1.54f), new Vector3(0.13f,body,0.22f), trim);
        for (int k = 0; k < 9; k++)
        {
            float angle = Mathf.PI * k / 8;
            var block = Box(parent, "Arch voussoir", new Vector3(x+Mathf.Cos(angle)*radius, y+Mathf.Sin(angle)*radius,-1.55f),
                new Vector3(0.16f,0.16f,0.23f), trim);
            block.transform.localRotation = Quaternion.Euler(0,0,angle * Mathf.Rad2Deg);
        }
        Box(parent, "Window sill", new Vector3(x,bottom,-1.565f), new Vector3(radius*2.4f,0.13f,0.26f), trim);
    }

    static void Rail(Transform parent, float x, float z, int count)
    {
        for (int i = 0; i <= count; i++)
        {
            float p = x + i * 0.61f;
            Box(parent, "Parapet post", new Vector3(p, 0.34f, z), new Vector3(0.25f,0.69f,0.26f), mortar);
            Box(parent, "Parapet cap", new Vector3(p, 0.70f, z), new Vector3(0.34f,0.14f,0.34f), trim);
        }
        Box(parent, "Parapet rail", new Vector3(x + count * 0.305f,0.55f,z),new Vector3(count*0.61f,0.08f,0.10f),iron);
        Box(parent, "Parapet footing", new Vector3(x + count * 0.305f,0.14f,z),new Vector3(count*0.61f+0.43f,0.23f,0.45f),mortar);
    }

    static void Tree(Transform parent, Vector3 position, float scale, int seed)
    {
        var root = Pivot(parent, "Quay olive tree", position);
        root.localScale = Vector3.one * scale;
        Box(root, "Planted curb", new Vector3(0,0.11f,0), new Vector3(1.65f,0.24f,1.45f), trim);
        Box(root, "Tree bed", new Vector3(0,0.239f,0), new Vector3(1.4f,0.026f,1.20f), bark);
        Segment(root, "Olive trunk", new Vector3(0,0.20f,0), new Vector3(-0.12f,2.30f,0), 0.22f, bark);
        var rng = new System.Random(seed);
        for (int i = 0; i < 21; i++)
        {
            float theta = i * 2.4f;
            float radius = 0.28f + 0.82f * (float)rng.NextDouble();
            float y = 1.55f + (float)rng.NextDouble() * 1.55f;
            float s = 0.66f + (float)rng.NextDouble() * 0.50f;
            Piece(root, "Leaf cluster", PrimitiveType.Sphere, new Vector3(Mathf.Cos(theta)*radius,y,Mathf.Sin(theta)*radius*0.8f),
                new Vector3(s, s*0.61f, s*0.93f), leaves);
        }
    }

    static void Lamp(Transform parent, Vector3 position)
    {
        var root = Pivot(parent, "Amber street lantern", position);
        Piece(root,"Lamp base",PrimitiveType.Cylinder,new Vector3(0,0.16f,0),new Vector3(0.34f,0.17f,0.34f),trim);
        Piece(root,"Lamp stem",PrimitiveType.Cylinder,new Vector3(0,1.17f,0),new Vector3(0.09f,1.15f,0.09f),iron);
        Box(root,"Lamp glass",new Vector3(0,2.37f,0),new Vector3(0.26f,0.44f,0.24f),lantern);
        Box(root,"Lamp cap",new Vector3(0,2.63f,0),new Vector3(0.35f,0.11f,0.35f),iron);
        foreach(float x in new[] { -0.135f, 0.135f })
            Box(root,"Lamp frame",new Vector3(x,2.37f,-0.118f),new Vector3(0.032f,0.47f,0.034f),iron);
    }

    static Transform BuildCaster(Vector3 position, out Transform shoulder, out Transform elbow, out Transform wrist, out Renderer cuff)
    {
        var teal = Lit("Caster teal wool",0x2D5860);
        var green = Lit("Caster cloak",0x354242);
        var linen = Lit("Caster coat piping",0xE1B169);
        var face = Lit("Caster skin",0xBD876B);
        var leather = Lit("Caster boots",0x413533);
        var copper = Lit("Caster copper",0xBF8751,0.44f);
        var rune = Lit("Caster crystal",0x75CADF,0.2f,new Color(0.20f,0.61f,1.0f)*2.6f);
        var body = Pivot(null,"Wayfarer caster",position);
        var rig = Pivot(body,"Pose rig",Vector3.zero);
        Piece(rig,"Travel boots",PrimitiveType.Capsule,new Vector3(-0.20f,0.29f,0.09f),new Vector3(0.25f,0.28f,0.26f),leather);
        Piece(rig,"Travel boots",PrimitiveType.Capsule,new Vector3(0.21f,0.29f,0.02f),new Vector3(0.26f,0.28f,0.26f),leather);
        Piece(rig,"Long coat",PrimitiveType.Cylinder,new Vector3(0,0.68f,0),new Vector3(0.80f,0.35f,0.52f),teal);
        Piece(rig,"Jacket",PrimitiveType.Capsule,new Vector3(0,1.24f,0),new Vector3(0.70f,0.47f,0.46f),teal);
        Piece(rig,"Cloak mantle",PrimitiveType.Sphere,new Vector3(0,1.43f,-0.06f),new Vector3(0.89f,0.34f,0.53f),green);
        var cape = Box(rig,"Hanging cloak",new Vector3(0,0.95f,-0.24f),new Vector3(0.68f,0.98f,0.12f),green);
        cape.transform.localRotation = Quaternion.Euler(14,0,6);
        Box(rig,"Copper belt",new Vector3(0,0.98f,0.025f),new Vector3(0.68f,0.10f,0.49f),copper);
        Box(rig,"Travel pouch",new Vector3(-0.34f,0.90f,0.08f),new Vector3(0.18f,0.26f,0.19f),leather);
        Piece(rig,"Head",PrimitiveType.Sphere,new Vector3(0,1.79f,0.03f),new Vector3(0.46f,0.49f,0.42f),face);
        Piece(rig,"Hood",PrimitiveType.Sphere,new Vector3(0,1.83f,-0.075f),new Vector3(0.55f,0.55f,0.45f),green);
        Piece(rig,"Scarf",PrimitiveType.Cylinder,new Vector3(0,1.57f,0.045f),new Vector3(0.52f,0.080f,0.40f),linen);
        Box(rig,"Short scarf tail",new Vector3(-0.26f,1.39f,0.20f),new Vector3(0.11f,0.30f,0.06f),linen);
        Piece(rig,"Eye",PrimitiveType.Sphere,new Vector3(-0.127f,1.81f,0.18f),new Vector3(0.053f,0.052f,0.044f),leather);
        Piece(rig,"Eye",PrimitiveType.Sphere,new Vector3(0.127f,1.81f,0.18f),new Vector3(0.053f,0.052f,0.044f),leather);
        Segment(rig,"Left sleeve",new Vector3(-0.33f,1.39f,0),new Vector3(-0.47f,1.02f,0.12f),0.22f,teal);
        Segment(rig,"Left bracer",new Vector3(-0.47f,1.02f,0.12f),new Vector3(-0.36f,0.92f,0.32f),0.15f,copper);
        shoulder = Pivot(rig,"Right upper arm",new Vector3(0.36f,1.40f,0));
        elbow = Pivot(shoulder,"Right forearm",new Vector3(0,0,0.38f));
        wrist = Pivot(elbow,"Right hand",new Vector3(0,0,0.35f));
        Segment(shoulder,"Casting sleeve",Vector3.zero,new Vector3(0,0,0.38f),0.23f,teal);
        Segment(elbow,"Casting bracer",Vector3.zero,new Vector3(0,0,0.35f),0.20f,copper);
        Piece(wrist,"Open casting palm",PrimitiveType.Sphere,new Vector3(0,0.035f,0.08f),new Vector3(0.20f,0.10f,0.22f),face);
        for (int i = 0; i < 4; i++)
            Segment(wrist,"Casting finger",new Vector3((i-1.5f)*0.041f,0.03f,0.14f),
                new Vector3((i-1.5f)*0.054f,0.068f,0.228f),0.042f,face);
        cuff = Piece(wrist,"Runic focus",PrimitiveType.Sphere,new Vector3(0,0.086f,0.065f),new Vector3(0.12f,0.07f,0.11f),rune);
        shoulder.localRotation = Quaternion.Euler(59,0,-16);
        elbow.localRotation = Quaternion.Euler(-31,0,10);
        return body;
    }

    static Transform BuildTarget(Vector3 position, out Transform visual, out Transform chest, out Transform aim)
    {
        var metal = Lit("Sentinel armour",0x413D48,0.42f);
        var edges = Lit("Sentinel copper trim",0xA27351,0.48f);
        var core = Lit("Sentinel ember",0xD48746,0.18f,new Color(1f,0.33f,0.07f)*1.35f);
        var target = Pivot(null,"Quay sentinel",position);
        var receiver = Pivot(target,"Sentinel hit surface",new Vector3(0,0.99f,0));
        var collider = receiver.gameObject.AddComponent<CapsuleCollider>();
        collider.height = 1.86f;
        collider.radius = 0.52f;
        aim = Pivot(receiver,"Chest aim point",new Vector3(0,0.28f,0.02f));
        visual = Pivot(target,"Sentinel visual",Vector3.zero);
        chest = Pivot(visual,"Recoil pivot",new Vector3(0,0.69f,0));
        for (int side = -1; side <= 1; side += 2)
        {
            Box(visual,"Foundation foot",new Vector3(side*0.24f,0.17f,0.09f),new Vector3(0.36f,0.27f,0.48f),metal);
            Segment(visual,"Sentinel greave",new Vector3(side*0.25f,0.25f,0),new Vector3(side*0.18f,0.75f,0),0.31f,metal);
            Piece(chest,"Shoulder plate",PrimitiveType.Sphere,new Vector3(side*0.44f,0.68f,0),new Vector3(0.42f,0.36f,0.44f),edges);
            Segment(chest,"Sentinel arm",new Vector3(side*0.50f,0.58f,0),new Vector3(side*0.62f,0.13f,0.08f),0.27f,metal);
        }
        Box(chest,"Sentinel breastplate",new Vector3(0,0.43f,0),new Vector3(0.77f,0.70f,0.50f),metal);
        Box(chest,"Sentinel collar",new Vector3(0,0.77f,0),new Vector3(0.66f,0.095f,0.52f),edges);
        Box(chest,"Sentinel helm",new Vector3(0,0.96f,0.006f),new Vector3(0.48f,0.37f,0.42f),metal);
        Box(chest,"Visor light",new Vector3(0,0.98f,0.225f),new Vector3(0.33f,0.047f,0.034f),core);
        var chestCore = Box(chest,"Chest furnace",new Vector3(0,0.44f,0.264f),new Vector3(0.19f,0.19f,0.045f),core);
        chestCore.transform.localRotation = Quaternion.Euler(0,0,45);
        return target;
    }

    static Transform Pivot(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent,false);
        t.localPosition = position;
        return t;
    }

    static Renderer Segment(Transform parent, string name, Vector3 from, Vector3 to, float width, Material material)
    {
        var direction = to - from;
        var part = Piece(parent,name,PrimitiveType.Capsule,(from+to)*0.5f,new Vector3(width,direction.magnitude*0.59f,width),material);
        part.transform.localRotation = Quaternion.FromToRotation(Vector3.up,direction);
        return part;
    }

    static Renderer Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = false)
        => Piece(parent,name,PrimitiveType.Cube,position,scale,material,collider);

    static Renderer Piece(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider = false)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent,false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        if (!collider) Object.DestroyImmediate(obj.GetComponent<Collider>());
        var renderer = obj.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    static Material Lit(string label, int hex, float metallic = 0, Color emission = default)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = label };
        mat.SetColor("_BaseColor",new Color32((byte)(hex>>16),(byte)(hex>>8),(byte)hex,255));
        mat.SetFloat("_Metallic",metallic);
        mat.SetFloat("_Smoothness", metallic > 0.3f ? 0.50f : 0.31f);
        if (emission.maxColorComponent > 0)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor",emission);
        }
        AssetDatabase.CreateAsset(mat,Folder+"/"+label+".mat");
        return mat;
    }

    static void Point(string name, Vector3 position, Color color, float power, float radius)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.type = LightType.Point;
        light.transform.position = position;
        light.color = color;
        light.intensity = power;
        light.range = radius;
    }

    static void Destroy(params string[] names)
    {
        foreach (var name in names)
        {
            var item = GameObject.Find(name);
            if (item != null) Object.DestroyImmediate(item);
        }
    }
}

