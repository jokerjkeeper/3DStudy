using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

static class GraphAuthoring
{
    const string Folder = "Assets/StormLab/Shaders/Graphs";
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Dictionary<string, Type> types = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name is "Unity.ShaderGraph.Editor" or "Unity.RenderPipelines.Universal.Editor")
        .SelectMany(a => a.GetTypes()).GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());

    [Serializable] class Spec { public bool additive; public string name; public int outputX; public Group[] groups; public string rgb, alpha; }
    [Serializable] class Group { public string title; public int x, y; public Node[] nodes; }
    [Serializable] class Node
    {
        public string id, type, label, reference, mode;
        public float value, min, max;
        public int x, y;
        public string[] inputs;
    }

    static object Create(string name, params object[] args) => Activator.CreateInstance(types[name], All & ~BindingFlags.Static, null, args, null);
    static PropertyInfo Prop(object instance, string name) => instance.GetType().GetProperty(name, All)
        ?? throw new ArgumentException($"{instance.GetType()}.{name}");
    static object Get(object instance, string name) => Prop(instance, name).GetValue(instance);
    static void Set(object instance, string name, object value)
    {
        var prop = Prop(instance, name);
        if (value is string token && prop.PropertyType.IsEnum)
            value = Enum.Parse(prop.PropertyType, token);
        prop.SetValue(instance, value);
    }

    static object Call(object instance, string name, params object[] args)
    {
        var method = instance.GetType().GetMethods(All).Single(m => m.Name == name &&
            !m.IsGenericMethodDefinition && m.GetParameters().Length == args.Length);
        return method.Invoke(instance, args);
    }

    static void SetFloatSlot(object node, int id, float value)
    {
        var slot = node.GetType().GetMethod("FindSlot", All).MakeGenericMethod(types["MaterialSlot"]).Invoke(node, new object[] { id });
        var type = Prop(slot, "value").PropertyType;
        if (type == typeof(Matrix4x4))
        {
            var row = Vector4.one * value;
            Set(slot, "value", new Matrix4x4(row, row, row, row));
        }
        else if (type == typeof(Vector4))
            Set(slot, "value", Vector4.one * value);
        else if (type == typeof(float))
            Set(slot, "value", value);
        else
            throw new ArgumentException($"Unsupported literal {slot.GetType()}");
    }

    public static void Make()
    {
        Directory.CreateDirectory(Folder);
        foreach (string filename in new[] { "Arc Erosion", "Shockwave Refraction" })
        {
            var spec = JsonUtility.FromJson<Spec>(File.ReadAllText(filename + ".json"));
            CreateGraph(spec);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var arc = CreateMaterial("Arc Erosion", 3010, "_ErosionBias");
        var wave = CreateMaterial("Shockwave Refraction", 2990, "_RefractionGain");
        var scene = EditorSceneManager.OpenScene("Assets/StormLab/StormLab.unity");
        var controls = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<StormLab.StormControls>());
        controls.FindProperty("graphArcMaterial").objectReferenceValue = arc;
        controls.FindProperty("graphWaveMaterial").objectReferenceValue = wave;
        controls.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("STORM_GRAPH_GENERATED");
    }

    static Material CreateMaterial(string filename, int queue, string verifiedProperty)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>($"{Folder}/{filename}.shadergraph");
        if (!shader || ShaderUtil.ShaderHasError(shader) || shader.name == "Hidden/Shader Graph/FallbackError")
            throw new InvalidOperationException($"Shader import failed for {filename}");
        string path = $"{Folder}/{filename}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_QueueControl")) mat.SetFloat("_QueueControl", 1);
        mat.renderQueue = queue;
        if (!mat.HasProperty(verifiedProperty))
            throw new InvalidOperationException($"Missing graph property {verifiedProperty}");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void CreateGraph(Spec spec)
    {
        var target = Create("UniversalTarget");
        Call(target, "TrySetActiveSubTarget", types["UniversalUnlitSubTarget"]);
        Set(target, "surfaceType", "Transparent");
        Set(target, "zWriteControl", "ForceDisabled");
        Set(target, "alphaMode", spec.additive ? "Additive" : "Alpha");
        Set(target, "renderFace", "Both");
        Set(target, "castShadows", false);

        var graph = Create("GraphData");
        Call(graph, "AddContexts");
        var fields = new[] { Block("VertexDescription", "Position"), Block("VertexDescription", "Normal"), Block("VertexDescription", "Tangent"),
            Block("SurfaceDescription", "BaseColor"), Block("SurfaceDescription", "Alpha") };
        Array blocks = Array.CreateInstance(types["BlockFieldDescriptor"], fields.Length);
        for (int i = 0; i < fields.Length; i++) blocks.SetValue(fields[i], i);
        Array targets = Array.CreateInstance(types["Target"], 1);
        targets.SetValue(target, 0);
        Call(graph, "InitializeOutputs", targets, blocks);
        Set(graph, "path", "StormLab/Graphs");
        Set(Get(graph, "vertexContext"), "position", new Vector2(spec.outputX, 0));
        Set(Get(graph, "fragmentContext"), "position", new Vector2(spec.outputX, 230));

        var category = types["CategoryData"].GetMethod("DefaultCategory", All).Invoke(null, new object[] { null });
        Call(graph, "AddCategory", category);
        var allNodes = new Dictionary<string, object>();
        foreach (var lane in spec.groups)
        {
            var group = Create("GroupData", lane.title, new Vector2(lane.x - 20, lane.y - 44));
            Call(graph, "CreateGroup", group);
            foreach (var entry in lane.nodes)
            {
                object node = Create(entry.type);
                if (entry.type == "PropertyNode")
                {
                    var property = Create(entry.mode == "HDR" ? "ColorShaderProperty" : "Vector1ShaderProperty");
                    Set(property, "displayName", entry.label);
                    Set(property, "overrideReferenceName", entry.reference);
                    Set(property, "generatePropertyBlock", true);
                    if (entry.mode == "HDR")
                    {
                        Set(property, "colorMode", "HDR");
                        Set(property, "value", new Color(0.10f, 0.7f, 1, 1));
                    }
                    else
                    {
                        Set(property, "value", entry.value);
                        if (entry.mode == "Slider")
                        {
                            Set(property, "floatType", "Slider");
                            Set(property, "rangeValues", new Vector2(entry.min, entry.max));
                        }
                    }
                    Call(graph, "AddGraphInput", property, -1);
                    Call(category, "InsertItemIntoCategory", property, -1);
                    Set(node, "property", property);
                }
                Set(node, "group", group);
                Set(node, "previewExpanded", false);
                var draw = Get(node, "drawState");
                Set(draw, "position", new Rect(lane.x + entry.x * 217, lane.y + entry.y * 123, 185, 105));
                Set(draw, "expanded", true);
                Set(node, "drawState", draw);
                Call(graph, "AddNode", node, false);
                allNodes.Add(entry.id, node);
            }
        }
        foreach (var entry in spec.groups.SelectMany(g => g.nodes))
        {
            if (entry.inputs == null) continue;
            for (int i = 0; i < entry.inputs.Length; i++)
                if (!string.IsNullOrEmpty(entry.inputs[i])) Link(entry.inputs[i], allNodes[entry.id], i);
        }
        var graphBlocks = (IEnumerable)graph.GetType().GetMethod("GetNodes", All)
            .MakeGenericMethod(types["BlockNode"]).Invoke(graph, null);
        foreach (object block in graphBlocks)
        {
            if (Equals(Get(block, "descriptor"), fields[3])) Link(spec.rgb, block, 0);
            if (Equals(Get(block, "descriptor"), fields[4]) && spec.alpha != null && spec.alpha != "") Link(spec.alpha, block, 0);
        }
        Call(graph, "ValidateGraph");
        types["FileUtilities"].GetMethod("WriteShaderGraphToDisk", All)
            .Invoke(null, new[] { $"{Folder}/{spec.name}.shadergraph", graph });

        void Link(string source, object to, int input)
        {
            if (source.StartsWith("="))
            {
                SetFloatSlot(to, input, float.Parse(source.Substring(1), System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            string[] parts = source.Split('.');
            object from = allNodes[parts[0]];
            int output = int.Parse(parts[1]);
            var edge = Call(graph, "Connect", Call(from, "GetSlotReference", output), Call(to, "GetSlotReference", input));
            if (edge == null) throw new InvalidOperationException($"Couldn't connect {source} to {Get(to, "name")}.{input}");
        }
    }

    static object Block(string category, string name)
        => types["BlockFields"].GetNestedType(category, All).GetField(name, All).GetValue(null);
}
