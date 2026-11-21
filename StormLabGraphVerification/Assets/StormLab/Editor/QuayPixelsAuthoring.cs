using System;
using System.IO;
using StormLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

static class QuayPixelsAuthoring
{
    const string Root = "Assets/StormLab/Battle";
    const string Pixels = Root + "/Pixels";

    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(Root + "/LanternQuay.unity");
        var sequence = UnityEngine.Object.FindAnyObjectByType<ChargeSequence>();
        var controller = UnityEngine.Object.FindAnyObjectByType<BattlePose>();
        var caster = GameObject.Find("Wayfarer caster").transform;
        var target = GameObject.Find("Quay sentinel").transform;
        var receiver = target.Find("Sentinel hit surface").GetComponent<Collider>();
        var sourceFixture = sequence.Hand.transform;

        var shader = Shader.Find("StormLab/Pixel Actor");
        if (!shader || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Pixel shader did not compile.");
        var actorMat = new Material(shader) { name = "Quay pixel actors" };
        AssetDatabase.CreateAsset(actorMat, Pixels + "/Quay Pixel Actors.mat");
        var shadowMat = new Material(Shader.Find("Sprites/Default")) { name = "Quay ground contact" };
        AssetDatabase.CreateAsset(shadowMat, Pixels + "/Quay Contact Shadow.mat");

        var view = GameObject.Find("Battle Camera").GetComponent<Camera>();
        Quaternion billboard = Quaternion.LookRotation(Vector3.ProjectOnPlane(view.transform.forward, Vector3.up));
        var castSprite = Actor(caster, "Wayfarer pixel actor", actorMat, billboard);
        var targetSprite = Actor(target, "Sentinel pixel actor", actorMat, billboard);
        var socket = new GameObject("Right hand pixel socket").transform;
        socket.SetParent(castSprite.transform, false);
        sourceFixture.SetParent(socket, false);
        sourceFixture.localPosition = Vector3.zero;
        sourceFixture.localRotation = Quaternion.identity;
        sequence.Hand.Palm.localPosition = Vector3.zero;
        sequence.Hand.Palm.localRotation = Quaternion.identity;

        caster.Find("Pose rig").gameObject.SetActive(false);
        target.Find("Sentinel visual").gameObject.SetActive(false);
        var shadow = LoadSprite("Pixel Contact Shadow", 28, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
        GroundShadow(caster, shadow, shadowMat);
        GroundShadow(target, shadow, shadowMat);

        var idle = new[] { Frame("Wayfarer Idle A", 21, 36), Frame("Wayfarer Idle B", 21, 35) };
        var charge = new[] { Frame("Wayfarer Gather", 18, 30), Frame("Wayfarer Lift", 15, 23),
            Frame("Wayfarer Channel A", 13, 18), Frame("Wayfarer Channel B", 13, 17) };
        var release = new[] { Frame("Wayfarer Release", 7, 25), Frame("Wayfarer Follow", 9, 28) };
        var targetIdle = new[] { LoadActor("Sentinel Idle A"), LoadActor("Sentinel Idle B") };
        var targetHit = new[] { LoadActor("Sentinel Impact"), LoadActor("Sentinel Recoil"), LoadActor("Sentinel Recover") };
        targetSprite.sprite = targetIdle[0];
        controller.Configure(sequence, castSprite, socket, targetSprite, receiver, idle, charge, release,
            Frame("Wayfarer Recover", 15, 32), targetIdle, targetHit);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("STORM_PIXELS_AUTHORED");
    }

    static SpriteRenderer Actor(Transform root, string name, Material mat, Quaternion rotation)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(root, false);
        renderer.transform.rotation = rotation;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return renderer;
    }

    static void GroundShadow(Transform root, Sprite sprite, Material mat)
    {
        var contact = Actor(root, "Soft contact shadow", mat, Quaternion.Euler(90, 0, 0));
        var foot = root.position;
        contact.transform.position = new Vector3(foot.x, 0.016f, foot.z);
        contact.transform.localScale = new Vector3(1.14f, 0.75f, 1);
        contact.sprite = sprite;
        contact.receiveShadows = false;
    }

    static BattlePose.CastFrame Frame(string name, float x, float y) => new(LoadActor(name), new Vector2(x, y));
    static Sprite LoadActor(string name) => LoadSprite(name, 26, new Vector2(0.5f, 4f / 58), FilterMode.Point);

    static Sprite LoadSprite(string name, float ppu, Vector2 pivot, FilterMode filter)
    {
        string path = $"{Pixels}/{name}.png";
        if (!File.Exists(path)) throw new InvalidOperationException("Missing sprite " + path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spritePixelsPerUnit = ppu;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.filterMode = filter;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

