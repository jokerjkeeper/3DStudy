using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StormLab.Benchmark
{
    public sealed partial class HouseBenchmark
    {
        static readonly string[] GroupNames =
        {
            "A1  Whole mesh", "A2  Four parts", "A3  Static Batching",
            "A4  Forward+", "A5  Resident Drawer", "A6  GPU Instancing"
        };

        bool previewMode;
        bool preview2K;
        int selectedGroup = 1;
        int selectedCount = 64;
        int lab;
        float distance;
        bool forceEffectsLast;
        bool closeTexture = true;
        Transform marker;
        Transform effect;
        Material markerMaterial;
        Material glassMaterial;
        Material effectMaterial;
        RenderPipelineAsset previousQuality;
        RenderPipelineAsset previousDefault;
        Vector3 previewHomePosition;
        Quaternion previewHomeRotation;
        float previewHomeZoom;
        bool previewLooking;

        void StartPreview()
        {
            previousQuality = QualitySettings.renderPipeline;
            previousDefault = GraphicsSettings.defaultRenderPipeline;
            previewMode = true;
            Configure(selectedGroup, selectedCount);
        }

        void OnDestroy()
        {
            StopCityBattle();
            if (previewMode)
            {
                QualitySettings.renderPipeline = previousQuality;
                GraphicsSettings.defaultRenderPipeline = previousDefault;
                if (block) Destroy(block);
                if (floor) Destroy(floor);
            }
            if (instancingMaterial) Destroy(instancingMaterial);
            if (instancingPipeline) Destroy(instancingPipeline);
            if (markerMaterial) Destroy(markerMaterial);
            if (glassMaterial) Destroy(glassMaterial);
            if (effectMaterial) Destroy(effectMaterial);
            if (battleGlassMaterial) Destroy(battleGlassMaterial);
        }

        void Update()
        {
            if (!previewMode || lab == 4) return;
            if (Input.GetKeyDown(KeyCode.Home))
            {
                view.transform.SetPositionAndRotation(previewHomePosition, previewHomeRotation);
                if (view.orthographic) view.orthographicSize = previewHomeZoom;
                else view.fieldOfView = previewHomeZoom;
                previewLooking = false;
            }

            float horizontal = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float vertical = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            float depth = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            var move = view.transform.right * horizontal + Vector3.up * vertical + forward * depth;
            float speed = view.orthographic ? Mathf.Max(4f, view.orthographicSize * .65f) : 5f;
            view.transform.position += Vector3.ClampMagnitude(move, 1f) * (Time.unscaledDeltaTime * speed);

            Vector3 mouse = Input.mousePosition;
            bool overPanel = new Rect(8f, 8f, Mathf.Min(Screen.width - 16f, 510f), 218f)
                .Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            if (Input.GetMouseButtonDown(1) && !overPanel)
                previewLooking = true;
            if (!Input.GetMouseButton(1))
                previewLooking = false;
            if (previewLooking)
            {
                Vector3 euler = view.transform.eulerAngles;
                var pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x) - Input.GetAxisRaw("Mouse Y") * 3f, -85f, 85f);
                view.transform.rotation = Quaternion.Euler(pitch, euler.y + Input.GetAxisRaw("Mouse X") * 3f, 0f);
            }
            if (overPanel) return;
            float scroll = Input.mouseScrollDelta.y;
            if (scroll == 0f) return;
            if (view.orthographic)
                view.orthographicSize = Mathf.Clamp(view.orthographicSize * Mathf.Pow(.88f, scroll), 2f, 230f);
            else
                view.fieldOfView = Mathf.Clamp(view.fieldOfView - scroll * 2f, 25f, 75f);
        }

        void SavePreviewCamera()
        {
            previewHomePosition = view.transform.position;
            previewHomeRotation = view.transform.rotation;
            previewHomeZoom = view.orthographic ? view.orthographicSize : view.fieldOfView;
            previewLooking = false;
        }

        void OnGUI()
        {
            if (!previewMode) return;
            if (lab == 4)
            {
                DrawCityBattle();
                return;
            }
            float width = Mathf.Min(Screen.width - 16f, 510f);
            GUILayout.BeginArea(new Rect(8, 8, width, lab == 0 ? 185 : 218), GUI.skin.box);
            GUILayout.BeginHorizontal();
            SelectLab("House batches", 0);
            SelectLab("B1 Depth", 1);
            SelectLab("B2 Transparency", 2);
            SelectLab("C Textures", 3);
            if (cityBattleRig) SelectLab("D City battle", 4);
            GUILayout.EndHorizontal();
            if (lab == 0)
            {
                int group = GUILayout.SelectionGrid(selectedGroup - 1, GroupNames, 3) + 1;
                GUILayout.BeginHorizontal();
                int count = GUILayout.SelectionGrid(selectedCount == 64 ? 0 : 1, new[] { "64 houses", "256 houses" }, 2);
                bool use2K = GUILayout.Toggle(preview2K, "2K textures");
                GUILayout.EndHorizontal();
                int newCount = count == 0 ? 64 : 256;
                if (group != selectedGroup || newCount != selectedCount || use2K != preview2K)
                {
                    selectedGroup = group;
                    selectedCount = newCount;
                    preview2K = use2K;
                    Configure(selectedGroup, selectedCount);
                }
                GUILayout.Label(group == 6 ? "Forward · GPU Instancing on · SRP Batcher off"
                    : group == 5 ? "Forward+ · Resident Drawer initialized: " + ResidentDrawerState() : GroupNames[group - 1]);
            }
            else if (lab == 3)
            {
                bool use2K = GUILayout.Toggle(preview2K, "2K textures (off = 4K)");
                bool close = GUILayout.Toggle(closeTexture, "Close view");
                if (use2K != preview2K || close != closeTexture)
                {
                    preview2K = use2K;
                    closeTexture = close;
                    ConfigureTexturePreview();
                }
                GUILayout.Label("Switch 2K / 4K without changing the camera or lighting.");
            }
            else
            {
                GUILayout.Label($"Character / effect depth: {distance:+0.0;-0.0;0.0}");
                distance = GUILayout.HorizontalSlider(distance, lab == 1 ? -9f : -3f, lab == 1 ? 9f : 3f);
                if (lab == 2)
                    forceEffectsLast = GUILayout.Toggle(forceEffectsLast, "Force FX after glass");
                GUILayout.Label(lab == 1 ? "Move through the gap. The front house should hide the marker and pulse."
                    : "Move FX across the glass. Force FX after glass to compare blend order.");
                UpdateLab();
            }
            GUILayout.Label("WASD  Move   Q/E  Height   RMB  Look   Wheel  Zoom   Home  Reset view");
            GUILayout.EndArea();
        }

        void SelectLab(string label, int mode)
        {
            if (!GUILayout.Button(label) || lab == mode) return;
            StopCityBattle();
            lab = mode;
            distance = 0f;
            if (lab == 0) Configure(selectedGroup, selectedCount);
            else if (lab == 3) ConfigureTexturePreview();
            else if (lab == 4) StartCityBattle();
            else ConfigureLab(mode);
            GUIUtility.ExitGUI();
        }

        void ConfigureTexturePreview()
        {
            ClearLayout();
            SetPipeline(forward);
            Floor(32);
            PutMesh(block.transform, whole, preview2K ? material2K : material4K);
            view.orthographic = false;
            view.fieldOfView = 51f;
            view.ResetAspect();
            var focus = closeTexture ? new Vector3(-.3f, 3.5f, -2.7f) : new Vector3(0f, 3.4f, 0f);
            var offset = closeTexture ? new Vector3(-3.3f, 2.3f, -6.6f) : new Vector3(-9.4f, 9.2f, -17.4f);
            view.transform.position = focus + offset;
            view.transform.LookAt(focus);
            SavePreviewCamera();
        }

        void ConfigureLab(int mode)
        {
            ClearLayout();
            SetPipeline(forward);
            Floor(50);
            view.orthographic = false;
            view.fieldOfView = 36f;
            view.ResetAspect();
            view.transform.position = new Vector3(0, 4.3f, -23f);
            view.transform.LookAt(new Vector3(0, 2f, 0));
            SavePreviewCamera();
            markerMaterial ??= new Material(ground);
            markerMaterial.color = new Color(.25f, .82f, .40f);
            effectMaterial ??= Transparent(new Color(1f, .32f, .12f, .55f));
            if (mode == 1)
            {
                // Both houses share this same static batch, while the character and FX are separate.
                PutMesh(block.transform, whole, material4K).transform.localPosition = new Vector3(-3.9f, 0, -4.5f);
                PutMesh(block.transform, whole, material4K).transform.localPosition = new Vector3(2.3f, 0, 5f);
                StaticBatchingUtility.Combine(block);
            }
            else
            {
                PutMesh(block.transform, whole, material4K).transform.localPosition = new Vector3(1.8f, 0, 6f);
                glassMaterial ??= Transparent(new Color(.20f, .58f, 1f, .56f));
                glassMaterial.renderQueue = (int)RenderQueue.Transparent;
                Primitive(PrimitiveType.Quad, "Glass", glassMaterial, new Vector3(-.25f, 2.75f, 0),
                    new Vector3(5f, 4.4f, 1f));
            }
            marker = Primitive(PrimitiveType.Capsule, "Character marker", markerMaterial,
                new Vector3(0, 1.2f, 0), new Vector3(1.05f, 1.2f, 1.05f));
            effect = Primitive(PrimitiveType.Quad, "Transparent pulse", effectMaterial,
                new Vector3(0, 2.1f, 0), new Vector3(3f, 2.7f, 1f));
            UpdateLab();
        }

        Transform Primitive(PrimitiveType kind, string label, Material material, Vector3 pos, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(kind);
            obj.name = label;
            obj.transform.SetParent(block.transform, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(obj.GetComponent<Collider>());
            return obj.transform;
        }

        static Material Transparent(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        void UpdateLab()
        {
            marker.localPosition = new Vector3(-.6f, 1.2f, distance);
            effect.localPosition = new Vector3(.45f, 2.4f, distance - .1f);
            effectMaterial.renderQueue = (int)RenderQueue.Transparent + (lab == 2 && forceEffectsLast ? 15 : 0);
        }
    }
}
