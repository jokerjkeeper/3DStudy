using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StormLab.Editor
{
    sealed class VfxFlowWindow : EditorWindow
    {
        internal const string DefaultFlowPath = "Assets/StormLab/Charge/Storm Lab Workflow.asset";
        internal const string RangeScenePath = "Assets/StormLab/StormLab.unity";
        internal const string BattleScenePath = "Assets/StormLab/Battle/LanternQuay.unity";
        const string DurationControl = "StormLab.StageDuration";
        enum PreviewScene { Current, TestRange, LanternQuay }

        const float InspectorWidth = 254;
        static readonly Color Canvas = new(0.067f, 0.09f, 0.121f);
        static readonly Color Edge = new(0.32f, 0.75f, 0.87f);
        static readonly Color Ink = new(0.88f, 0.93f, 0.98f);

        [SerializeField] VfxFlow flow;
        [SerializeField] Vector2 pan = new(18, 36);
        [SerializeField] float zoom = 0.82f;
        [SerializeField] string selectedId;
        [SerializeField] bool hasFramed;
        [SerializeField] PreviewScene previewScene;
        string durationNodeId;
        VfxFlow durationFlow;
        string durationText;
        VfxFlow.Effect? draggedLayer;
        string movingId, connectingId;
        bool panning;
        Vector2 grabOffset, inspectorScroll;
        string message;
        readonly List<VfxFlow.Node> playbackPath = new();

        [MenuItem("Storm Lab/VFX Workflow")]
        public static void Open()
        {
            var window = GetWindow<VfxFlowWindow>("VFX Workflow");
            window.minSize = new Vector2(860, 435);
            window.Show();
        }

        [MenuItem("Storm Lab/Open battle test")]
        static void OpenBattle()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Open battle test", "Exit Play Mode to change scenes.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(BattleScenePath);
        }

        void OnEnable()
        {
            minSize = new Vector2(860, 435);
            if (flow == null) flow = AssetDatabase.LoadAssetAtPath<VfxFlow>(DefaultFlowPath);
            Undo.undoRedoPerformed += RefreshUndo;
        }

        void OnDisable() => Undo.undoRedoPerformed -= RefreshUndo;
        void RefreshUndo()
        {
            durationNodeId = null;
            Repaint();
        }

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            if (!hasFramed && flow != null && Event.current.type == EventType.Layout)
            {
                hasFramed = true;
                selectedId = flow.entry;
                Frame();
            }
            DrawToolbar();
            float toolbar = 23;
            Rect canvas = new(0, toolbar, position.width - InspectorWidth, position.height - toolbar - 24);
            Rect inspector = new(canvas.xMax, toolbar, InspectorWidth, canvas.height + 24);
            GUI.BeginGroup(canvas);
            DrawCanvas(canvas.size);
            GUI.EndGroup();
            DrawInspector(inspector);
            var footer = new Rect(0, canvas.yMax, canvas.width, 24);
            GUI.Box(footer, GUIContent.none, EditorStyles.toolbar);
            GUI.Label(new Rect(9, footer.y + 3, footer.width - 12, 18),
                message ?? "Drag an output to an input   ·   Middle drag to pan   ·   Scroll to zoom", EditorStyles.miniLabel);
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(23)))
            {
                EditorGUI.BeginChangeCheck();
                var chosen = (VfxFlow)EditorGUILayout.ObjectField(flow, typeof(VfxFlow), false, GUILayout.Width(194));
                if (EditorGUI.EndChangeCheck())
                {
                    flow = chosen;
                    selectedId = connectingId = movingId = null;
                    message = null;
                }
                if (GUILayout.Button("New Flow", EditorStyles.toolbarButton, GUILayout.Width(68))) CreateFlow();
                using (new EditorGUI.DisabledScope(flow == null))
                {
                    if (GUILayout.Button("+ Stage", EditorStyles.toolbarDropDown, GUILayout.Width(69))) StageMenu();
                    if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(48))) Frame();
                    using (new EditorGUI.DisabledScope(Application.isPlaying))
                        previewScene = (PreviewScene)EditorGUILayout.EnumPopup(previewScene, EditorStyles.toolbarPopup, GUILayout.Width(112));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Play Flow", EditorStyles.toolbarButton, GUILayout.Width(77))) Play(null);
                    using (new EditorGUI.DisabledScope(flow == null || flow.Find(selectedId) == null))
                        if (GUILayout.Button("Preview Stage", EditorStyles.toolbarButton, GUILayout.Width(96))) Play(selectedId);
                }
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(45)))
                    {
                        FindControls()?.StopWorkflow();
                        message = "Playback stopped.";
                    }
            }
        }

        void DrawCanvas(Vector2 size)
        {
            var area = new Rect(Vector2.zero, size);
            Event e = Event.current;
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, Canvas);
                var grid = new Color(0.106f, 0.139f, 0.178f);
                float spacing = 32 * zoom;
                for (float x = Mathf.Repeat(pan.x, spacing); x < size.x; x += spacing)
                    EditorGUI.DrawRect(new Rect(x, 0, 1, size.y), grid);
                for (float y = Mathf.Repeat(pan.y, spacing); y < size.y; y += spacing)
                    EditorGUI.DrawRect(new Rect(0, y, size.x, 1), grid);
            }
            if (flow == null)
            {
                GUI.Label(new Rect(20, 20, 500, 30), "Select or create a VFX Flow asset.");
                return;
            }

            var controls = Application.isPlaying ? FindControls() : null;
            string active = controls != null ? controls.Sequence?.ActiveNodeId : null;
            float progress = controls != null ? controls.Sequence?.Progress ?? 0 : 0;
            Handles.BeginGUI();
            foreach (var node in flow.nodes)
            {
                var next = flow.Find(node.next);
                if (next != null) DrawEdge(Output(node), Input(next), Edge);
            }
            var source = flow.Find(connectingId);
            if (source != null) DrawEdge(Output(source), e.mousePosition, Color.white);
            Handles.EndGUI();

            foreach (var node in flow.nodes) DrawNode(node, active == node.id, progress);
            if (draggedLayer.HasValue && area.Contains(e.mousePosition))
            {
                var tip = new Rect(e.mousePosition + new Vector2(11, 12), new Vector2(120, 22));
                GUI.Box(tip, LayerName(draggedLayer.Value), EditorStyles.miniButton);
            }
            if (flow.nodes.Count == 0)
                GUI.Label(new Rect(20, 20, 520, 30), "Add a stage to start the workflow.", EditorStyles.boldLabel);

            HandleCanvasInput(area);
        }

        void DrawNode(VfxFlow.Node node, bool running, float progress)
        {
            Rect rect = NodeRect(node);
            float titleHeight = 31 * zoom;
            Color highlight = StageColor(node.stage);
            bool selected = node.id == selectedId;
            EditorGUI.DrawRect(new Rect(rect.x + 3, rect.y + 4, rect.width, rect.height), new Color(0, 0, 0, 0.34f));
            EditorGUI.DrawRect(rect, running ? new Color(0.49f, 0.92f, 0.57f) : selected ? highlight : new Color(0.24f, 0.30f, 0.39f));
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), new Color(0.119f, 0.153f, 0.203f));
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, titleHeight), Color.Lerp(Canvas, highlight, 0.27f));
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, 3, titleHeight), highlight);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Ink } };
            GUI.Label(new Rect(rect.x + 10, rect.y + 3, rect.width - 41, titleHeight), node.stage.ToString(), titleStyle);
            var timeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = Ink } };
            GUI.Label(new Rect(rect.xMax - 43, rect.y + 2, 34, titleHeight), $"{node.duration:0.##}s", timeStyle);
            float y = rect.y + titleHeight + 7;
            if (flow.entry == node.id)
            {
                GUI.Label(new Rect(rect.x + 10, y, rect.width - 20, 15), "ENTRY", EditorStyles.miniBoldLabel);
                y += 16;
            }
            foreach (var layer in node.layers)
            {
                EditorGUI.DrawRect(new Rect(rect.x + 9, y + 1, rect.width - 18, 16 * zoom), new Color(0.18f, 0.23f, 0.30f));
                string timing = layer.delay > 0 ? $"  +{layer.delay:0.##}s" : "";
                GUI.Label(new Rect(rect.x + 13, y - 1, rect.width - 26, 19), LayerName(layer.effect) + timing, EditorStyles.miniLabel);
                y += 19 * zoom;
            }
            if (running) EditorGUI.DrawRect(new Rect(rect.x + 1, rect.yMax - 4, (rect.width - 2) * progress, 3), highlight);
            DrawPort(Input(node), highlight);
            DrawPort(Output(node), highlight);
        }

        void HandleCanvasInput(Rect area)
        {
            Event e = Event.current;
            Vector2 pointer = e.mousePosition;
            bool inside = area.Contains(pointer);
            var node = flow.nodes.LastOrDefault(n => NodeRect(n).Contains(pointer));
            if (draggedLayer.HasValue)
            {
                if (e.type == EventType.MouseUp)
                {
                    var effect = draggedLayer.Value;
                    draggedLayer = null;
                    GUIUtility.hotControl = 0;
                    if (inside && node != null)
                    {
                        selectedId = node.id;
                        AddLayer(node, effect);
                    }
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.MouseDrag)
                {
                    e.Use();
                    Repaint();
                }
                return;
            }
            if (inside && e.type == EventType.ScrollWheel)
            {
                Vector2 local = (pointer - pan) / zoom;
                zoom = Mathf.Clamp(zoom * Mathf.Pow(1.07f, -e.delta.y), 0.62f, 1.35f);
                pan = pointer - local * zoom;
                e.Use();
            }
            else if (inside && e.type == EventType.MouseDown && e.button == 2)
            {
                panning = true;
                e.Use();
            }
            else if (inside && e.type == EventType.MouseDown && e.button == 0)
            {
                var output = NearPort(pointer, Output);
                if (output != null)
                {
                    connectingId = output.id;
                    selectedId = output.id;
                }
                else
                {
                    selectedId = node?.id;
                    if (node != null)
                    {
                        Undo.RecordObject(flow, "Move VFX stage");
                        movingId = node.id;
                        grabOffset = pointer - NodeRect(node).position;
                    }
                }
                message = null;
                e.Use();
            }
            else if (inside && e.type == EventType.ContextClick)
            {
                var output = NearPort(pointer, Output);
                if (output != null && !string.IsNullOrEmpty(output.next))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Disconnect output"), false,
                        () => Change("Disconnect VFX stages", () => output.next = null));
                    menu.ShowAsContext();
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && panning)
            {
                pan += e.delta;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && connectingId != null)
            {
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && flow.Find(movingId) is { } moved)
            {
                moved.position = (pointer - grabOffset - pan) / zoom;
                EditorUtility.SetDirty(flow);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && (panning || movingId != null || connectingId != null))
            {
                if (connectingId != null && NearPort(pointer, Input) is { } destination)
                {
                    Undo.RecordObject(flow, "Connect VFX stages");
                    if (flow.TryConnect(flow.Find(connectingId), destination)) Save();
                    else message = "This connection would create a cycle.";
                }
                if (movingId != null) Save();
                panning = false;
                movingId = connectingId = null;
                e.Use();
            }
            RepaintIfInteracting();
        }

        void DrawInspector(Rect zone)
        {
            using var area = new GUILayout.AreaScope(zone, GUIContent.none, EditorStyles.helpBox);
            using var scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll);
            inspectorScroll = scroll.scrollPosition;
            using var width = new LabelWidthScope(100f);
            var node = flow != null ? flow.Find(selectedId) : null;
            if (node == null)
            {
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Stage details", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Select a stage to edit its duration and parallel layers.", MessageType.None);
            }
            else
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField(node.stage.ToString(), EditorStyles.boldLabel);
                DrawStageDuration(node);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(flow.entry == node.id))
                        if (GUILayout.Button(flow.entry == node.id ? "Entry stage" : "Set as entry"))
                            Change("Set VFX entry", () => flow.entry = node.id);
                    if (GUILayout.Button("Delete", GUILayout.Width(61)))
                    {
                        Change("Delete VFX stage", () => flow.Remove(node));
                        selectedId = null;
                        GUIUtility.ExitGUI();
                    }
                }

                if (node != null) DrawLayers(node);
            }
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Effect palette", EditorStyles.boldLabel);
            foreach (VfxFlow.Effect effect in Enum.GetValues(typeof(VfxFlow.Effect))) PaletteItem(node, effect);

        }

        void DrawStageDuration(VfxFlow.Node node)
        {
            if (durationNodeId != node.id || durationFlow != flow)
            {
                durationNodeId = node.id;
                durationFlow = flow;
                durationText = node.duration.ToString("0.###", CultureInfo.InvariantCulture);
            }

            Event input = Event.current;
            bool submit = input.type == EventType.KeyDown &&
                input.keyCode is KeyCode.Return or KeyCode.KeypadEnter &&
                GUI.GetNameOfFocusedControl() == DurationControl;
            using var row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField("Stage seconds", GUILayout.Width(82));
            GUI.SetNextControlName(DurationControl);
            durationText = EditorGUILayout.TextField(durationText, GUILayout.MinWidth(42));
            // The step buttons let the floating window edit timing with the mouse alone.
            bool down = GUILayout.Button("−", EditorStyles.miniButtonLeft, GUILayout.Width(19));
            bool up = GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(19));
            bool apply = GUILayout.Button("Apply", GUILayout.Width(47));
            if (!down && !up && !apply && !submit) return;

            if (down || up)
            {
                float stepped = Mathf.Max(0.05f, node.duration + (up ? 0.10f : -0.10f));
                durationText = stepped.ToString("0.###", CultureInfo.InvariantCulture);
            }
            if (TryApplyDuration(node, durationText))
            {
                durationText = node.duration.ToString("0.###", CultureInfo.InvariantCulture);
                message = $"Stage saved at {durationText}s. The next playback uses the new time.";
                GUI.FocusControl(null);
            }
            else message = "Enter a finite stage duration of at least 0.05 seconds.";
            if (submit && input.type != EventType.Used) input.Use();
        }

        internal bool TryApplyDuration(VfxFlow.Node node, string value)
        {
            if ((!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) &&
                !float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds)) ||
                !float.IsFinite(seconds) || seconds < 0.05f) return false;

            if (seconds != node.duration)
                Change("Set stage duration", () => node.duration = seconds);
            return true;
        }

        void DrawLayers(VfxFlow.Node node)
        {
            GUILayout.Space(12);
            EditorGUILayout.LabelField("Parallel layers", EditorStyles.boldLabel);
            foreach (var layer in node.layers.ToArray())
            {
                using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(LayerName(layer.effect), EditorStyles.miniBoldLabel, GUILayout.Width(112));
                    if (GUILayout.Button("Solo", EditorStyles.miniButton, GUILayout.Width(40))) Play(node.id, layer.effect);
                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(51)))
                    {
                        Change("Remove VFX layer", () => node.layers.Remove(layer));
                        GUIUtility.ExitGUI();
                    }
                }
                float delay = EditorGUILayout.DelayedFloatField("Delay (s)", layer.delay);
                float duration = EditorGUILayout.DelayedFloatField("Duration (s)", layer.duration);
                if (delay != layer.delay || duration != layer.duration)
                    Change("Set layer timing", () =>
                    {
                        layer.delay = Mathf.Max(0, delay);
                        layer.duration = Mathf.Max(0, duration);
                    });
            }
            EditorGUILayout.LabelField("Layer duration 0 uses the rest of the stage.", EditorStyles.wordWrappedMiniLabel);
        }

        void PaletteItem(VfxFlow.Node node, VfxFlow.Effect effect)
        {
            Rect row = GUILayoutUtility.GetRect(120, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(row, new Color(0.14f, 0.19f, 0.24f));
            GUI.Label(new Rect(row.x + 7, row.y + 2, row.width - 65, 19), LayerName(effect), EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(node == null || node.layers.Any(layer => layer.effect == effect)))
                if (GUI.Button(new Rect(row.xMax - 53, row.y + 2, 51, 19), "Add", EditorStyles.miniButton))
                    AddLayer(node, effect);
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive, row);
            var handle = new Rect(row.x, row.y, row.width - 56, row.height);
            if (flow != null && e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
            {
                draggedLayer = effect;
                GUIUtility.hotControl = control;
                e.Use();
            }
            GUILayout.Space(3);
        }

        void AddLayer(VfxFlow.Node node, VfxFlow.Effect effect)
        {
            if (node.layers.Any(layer => layer.effect == effect))
            {
                message = "That effect is already in this stage.";
                return;
            }
            Change("Add VFX layer", () => node.layers.Add(new VfxFlow.Layer { effect = effect }));
            message = null;
        }

        void StageMenu()
        {
            var menu = new GenericMenu();
            foreach (VfxFlow.Stage stage in Enum.GetValues(typeof(VfxFlow.Stage)))
                menu.AddItem(new GUIContent(stage.ToString()), false, () => AddStage(stage));
            menu.ShowAsContext();
        }

        void AddStage(VfxFlow.Stage stage)
        {
            var node = new VfxFlow.Node(stage, (new Vector2(100, 116) - pan) / zoom +
                Vector2.one * (flow.nodes.Count % 6) * 29f, 0.8f);
            Change("Add VFX stage", () =>
            {
                flow.nodes.Add(node);
                if (flow.Find(flow.entry) == null) flow.entry = node.id;
            });
            selectedId = node.id;
        }

        void CreateFlow()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create VFX Flow", "VFX Flow", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<VfxFlow>();
            asset.Seed();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            flow = asset;
            selectedId = flow.entry;
            Frame();
        }

        void Play(string nodeId, VfxFlow.Effect? solo = null)
        {
            if (nodeId == null && !flow.TryGetPath(playbackPath, out message)) return;
            Save();
            string scenePath = ResolvePreviewScene();
            if (WorkflowPreview.Request(flow, nodeId, solo, scenePath, out var error))
                message = solo.HasValue ? $"Previewing {LayerName(solo.Value)} in Game view." :
                    nodeId == null ? "Playing the connected flow in Game view." : "Previewing the selected stage in Game view.";
            else message = error;
        }

        string ResolvePreviewScene()
        {
            string current = EditorSceneManager.GetActiveScene().path;
            return previewScene switch
            {
                PreviewScene.TestRange => RangeScenePath,
                PreviewScene.LanternQuay => BattleScenePath,
                _ => current == BattleScenePath ? BattleScenePath : RangeScenePath
            };
        }

        void Frame()
        {
            if (flow == null || flow.nodes.Count == 0) return;
            float left = flow.nodes.Min(node => node.position.x);
            float top = flow.nodes.Min(node => node.position.y);
            float width = flow.nodes.Max(node => node.position.x + 214f) - left;
            float height = flow.nodes.Max(node => node.position.y + 72f + 19f * node.layers.Count) - top;
            zoom = Mathf.Clamp(Mathf.Min((position.width - InspectorWidth - 64) / width, (position.height - 100) / height), 0.62f, 1.25f);
            pan = new Vector2(32, 39) - new Vector2(left, top) * zoom;
            Repaint();
        }

        void RepaintIfInteracting()
        {
            if (movingId != null || connectingId != null || panning) Repaint();
        }

        void Change(string label, Action edit)
        {
            Undo.RecordObject(flow, label);
            edit();
            Save();
        }

        void Save()
        {
            EditorUtility.SetDirty(flow);
            AssetDatabase.SaveAssetIfDirty(flow);
            Repaint();
        }

        Rect NodeRect(VfxFlow.Node node) => new(pan + node.position * zoom, new Vector2(214, 70 + 19 * node.layers.Count) * zoom);
        Vector2 Input(VfxFlow.Node node) { var r = NodeRect(node); return new Vector2(r.xMin, r.y + 16 * zoom); }
        Vector2 Output(VfxFlow.Node node) { var r = NodeRect(node); return new Vector2(r.xMax, r.y + 16 * zoom); }

        VfxFlow.Node NearPort(Vector2 pos, Func<VfxFlow.Node, Vector2> port) =>
            flow.nodes.LastOrDefault(n => (pos - port(n)).sqrMagnitude <= 121f);

        static void DrawPort(Vector2 center, Color color)
        {
            EditorGUI.DrawRect(new Rect(center.x - 5, center.y - 5, 10, 10), Canvas);
            EditorGUI.DrawRect(new Rect(center.x - 3, center.y - 3, 6, 6), color);
        }

        static void DrawEdge(Vector2 start, Vector2 end, Color color)
        {
            float distance = Mathf.Max(30, Mathf.Abs(end.x - start.x) * 0.45f);
            Handles.DrawBezier(start, end, start + Vector2.right * distance, end + Vector2.left * distance, color, null, 2.5f);
        }

        static Color StageColor(VfxFlow.Stage stage) => stage switch
        {
            VfxFlow.Stage.Charge => new Color(0.43f, 0.69f, 1f),
            VfxFlow.Stage.Release => new Color(0.48f, 0.86f, 0.69f),
            _ => new Color(1f, 0.65f, 0.39f)
        };

        static string LayerName(VfxFlow.Effect effect) => effect switch
        {
            VfxFlow.Effect.Ring => "Rotating rings",
            VfxFlow.Effect.Arc => "Branching arcs",
            VfxFlow.Effect.Particles => "Particle sparks",
            VfxFlow.Effect.Bolt => "Discharge trail",
            _ => "Hit feedback"
        };

        readonly struct LabelWidthScope : IDisposable
        {
            readonly float previous;

            public LabelWidthScope(float width)
            {
                previous = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = width;
            }

            public void Dispose() => EditorGUIUtility.labelWidth = previous;
        }

        internal static StormControls FindControls() => UnityEngine.Object.FindAnyObjectByType<StormControls>();
    }

    [InitializeOnLoad]
    static class WorkflowPreview
    {
        const string AssetKey = "StormLab.WorkflowPreview.Asset";
        const string NodeKey = "StormLab.WorkflowPreview.Node";
        const string SoloKey = "StormLab.WorkflowPreview.Solo";

        static WorkflowPreview() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static bool Request(VfxFlow flow, string node, VfxFlow.Effect? solo, string scene, out string error)
        {
            error = null;
            if (Application.isPlaying)
            {
                if (EditorSceneManager.GetActiveScene().path != scene)
                {
                    error = "Exit Play Mode before switching the preview scene.";
                    return false;
                }
                var controls = VfxFlowWindow.FindControls();
                bool started = controls != null && controls.PreviewWorkflow(flow, node, solo);
                if (!started) error = "Open the Storm Lab range and check the test target.";
                return started;
            }

            if (EditorSceneManager.GetActiveScene().path != scene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    error = "Playback canceled.";
                    return false;
                }
                EditorSceneManager.OpenScene(scene);
            }

            SessionState.SetString(AssetKey, AssetDatabase.GetAssetPath(flow));
            SessionState.SetString(NodeKey, node ?? "");
            SessionState.SetInt(SoloKey, solo.HasValue ? (int)solo.Value : -1);
            EditorApplication.isPlaying = true;
            return true;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            string path = SessionState.GetString(AssetKey, "");
            if (string.IsNullOrEmpty(path)) return;
            string node = SessionState.GetString(NodeKey, "");
            int solo = SessionState.GetInt(SoloKey, -1);
            SessionState.EraseInt(SoloKey);
            SessionState.EraseString(AssetKey);
            SessionState.EraseString(NodeKey);
            EditorApplication.delayCall += () =>
            {
                var flow = AssetDatabase.LoadAssetAtPath<VfxFlow>(path);
                var controls = VfxFlowWindow.FindControls();
                if (controls == null || !controls.PreviewWorkflow(flow, string.IsNullOrEmpty(node) ? null : node, solo < 0 ? null : (VfxFlow.Effect)solo))
                    Debug.LogWarning("The VFX workflow could not start. Check the Storm Lab scene and test target.", flow);
            };
        }
    }
}
