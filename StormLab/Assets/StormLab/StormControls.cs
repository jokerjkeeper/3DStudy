using NoggiSdfToolkit;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StormLab
{
    public sealed class StormControls : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform testTarget;
        [SerializeField] ArcFlash arc;
        [SerializeField] ImpactFeedback impact;
        [SerializeField] Volume volume;
        [SerializeField] Renderer emitter;
        [SerializeField] Rigidbody[] bodies;

        [SerializeField] ChargeSequence chargeSequence;
        [SerializeField] BattleActorPose battlePose;
        [SerializeField] bool allowHideHud;
        [SerializeField] GroveBodyCurrent bodyCurrent;
        [SerializeField] bool allowCameraZoom;
        [SerializeField] Light[] adjustableLights = System.Array.Empty<Light>();
        [SerializeField] string sceneTitle = "Storm Lab";
        [SerializeField] string sceneDescription = "Electrical discharge";

        [Header("Shader Graph comparison")]
        [SerializeField] Material graphArcMaterial;
        [SerializeField] Material graphWaveMaterial;

        [Header("Effect switches")]
        [SerializeField] bool bloom = true;
        [SerializeField] bool branches = true;
        [SerializeField] bool refraction = true;
        [SerializeField] bool physicsImpulse = true;
        [SerializeField] bool cameraShake = true;
        [SerializeField] bool scorch = true;
        [SerializeField] CityTargetMotion faceSdfTarget;
        [SerializeField] BangsShadowProjection bangsTarget;

        const float ShotInterval = 0.64f;
        const float NearFieldOfView = 32f;
        const float WideFieldOfView = 68f;
        const float CameraMoveSpeed = 4f;
        const float CameraLookSpeed = 3f;
        const float LightMoveSpeed = 3f;
        const float LightTurnSpeed = 65f;
        static readonly Color Cyan = new(0.08f, 0.71f, 1f);
        static readonly Color Red = new(1f, 0.14f, 0.075f);
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        Bloom bloomOverride;
        MaterialPropertyBlock emitterProperties;
        Vector3[] startPositions;
        Quaternion[] startRotations;
        GUIStyle title, subtitle, caption, mono, button, panel;
        bool red, hasAim, hideHud;
        bool looking;
        bool useGraphArc, useGraphWave;
        float nextShot, defaultFieldOfView;
        float cameraYaw, cameraPitch;
        Vector3 defaultCameraPosition;
        Quaternion defaultCameraRotation;
        Pose[] initialLightPoses;
        int lightIndex;
        bool lightMode;
        int shots;
        string surface = "READY";

        public void Configure(Camera camera, Transform origin, Transform target, ArcFlash arcFlash,
            ImpactFeedback feedback, Volume globalVolume, Renderer emitterRenderer, Rigidbody[] resetBodies)
        {
            view = camera;
            muzzle = origin;
            testTarget = target;
            arc = arcFlash;
            impact = feedback;
            volume = globalVolume;
            emitter = emitterRenderer;
            bodies = resetBodies;
        }

        void Awake()
        {
            defaultFieldOfView = view.fieldOfView;
            defaultCameraPosition = view.transform.position;
            defaultCameraRotation = view.transform.rotation;
            initialLightPoses = new Pose[adjustableLights.Length];
            for (int i = 0; i < adjustableLights.Length; i++)
            {
                if (adjustableLights[i] == null) continue;
                Transform light = adjustableLights[i].transform;
                initialLightPoses[i] = new Pose(light.localPosition, light.localRotation);
            }
            volume.profile.TryGet(out bloomOverride);
            emitterProperties = new MaterialPropertyBlock();
            startPositions = new Vector3[bodies.Length];
            startRotations = new Quaternion[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                startPositions[i] = bodies[i].position;
                startRotations[i] = bodies[i].rotation;
            }
        }

        public ChargeSequence Sequence => chargeSequence;

        void OnEnable()
        {
            if (chargeSequence == null) return;
            chargeSequence.Arrived += RegisterImpact;
            chargeSequence.PreviewHit += PreviewImpact;
        }

        void OnDisable()
        {
            if (chargeSequence == null) return;
            chargeSequence.Arrived -= RegisterImpact;
            chargeSequence.PreviewHit -= PreviewImpact;
        }

        void Start()
        {
            SetGraphArc(true);
            SetGraphWave(true);
        }

        void SetGraphArc(bool useGraph)
        {
            useGraphArc = useGraph && graphArcMaterial != null;
            arc.UseMaterialOverride(useGraphArc ? graphArcMaterial : null);
        }

        void SetGraphWave(bool useGraph)
        {
            useGraphWave = useGraph && graphWaveMaterial != null;
            impact.UseShockMaterialOverride(useGraphWave ? graphWaveMaterial : null);
        }

        void Update()
        {
            if (allowHideHud && Input.GetKeyDown(KeyCode.Tab))
            {
                hideHud = !hideHud;
                if (hideHud) lightMode = false;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) red = false;
            if (Input.GetKeyDown(KeyCode.Alpha2)) red = true;
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetGraphArc(!useGraphArc);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetGraphWave(!useGraphWave);
            if (Input.GetKeyDown(KeyCode.R)) ResetRange();
            if (chargeSequence != null)
            {
                if (Input.GetKeyDown(KeyCode.C)) chargeSequence.TogglePreview();
                if (Input.GetKeyDown(KeyCode.F)) PlayChargeSequence();
            }

            if (bloomOverride != null)
                bloomOverride.active = bloom;
            if (emitter != null)
            {
                emitterProperties.SetColor(EmissionColor, (red ? Red : Cyan) * 3f);
                emitter.SetPropertyBlock(emitterProperties);
            }

            Vector3 pointer = Input.mousePosition;
            bool inside = pointer.x >= 0 && pointer.x < Screen.width && pointer.y >= 0 && pointer.y < Screen.height;
            Vector2 hudPointer = new Vector2(pointer.x, Screen.height - pointer.y) / HudScale;
            bool overControls = !hideHud && (PanelRect.Contains(hudPointer) ||
                (chargeSequence != null && ChargeRect.Contains(hudPointer)) ||
                (allowCameraZoom && CameraRect.Contains(hudPointer)) ||
                (adjustableLights.Length > 0 && LightingRect.Contains(hudPointer)));
            float scroll = Input.mouseScrollDelta.y;
            if (allowCameraZoom && inside && !overControls && scroll != 0f)
                SetCameraFieldOfView(view.fieldOfView - scroll * 2f);
            if (adjustableLights.Length > 0)
            {
                if (!hideHud && Input.GetKeyDown(KeyCode.T)) lightMode = !lightMode;
                if (Input.GetKeyDown(KeyCode.L)) SelectNextLight();
                if (Input.GetKeyDown(KeyCode.Backspace)) ResetSelectedLight();
                if (lightMode) MoveSelectedLight(ReadMovement());
            }
            if (allowCameraZoom)
            {
                if (lightMode) looking = false;
                UpdateCameraLook(inside && !overControls && !lightMode);
                if (!lightMode) MoveCamera(ReadMovement());
            }
            Ray ray = view.ScreenPointToRay(pointer);
            RaycastHit hit = default;
            hasAim = inside && !overControls && !looking && !lightMode && Physics.Raycast(ray, out hit, 50f);
            if (Time.time < nextShot || (chargeSequence != null && chargeSequence.Busy))
                return;

            if (Input.GetKey(KeyCode.Space))
                FireTestShot();
            else if (hasAim && Input.GetMouseButton(0))
                Shoot(hit);
        }

        public bool FireTestShot()
        {
            if (!TryGetTestHit(out var hit))
                return false;
            Shoot(hit);
            return true;
        }

        bool TryGetTestHit(out RaycastHit hit) => Physics.Raycast(
            new Ray(view.transform.position, testTarget.position - view.transform.position), out hit, 50f);

        void PlayChargeSequence()
        {
            if (chargeSequence.Busy || !TryGetTestHit(out var hit)) return;
            chargeSequence.SetAccentTint(red ? Red : Cyan);
            chargeSequence.Play(hit);
        }

        public bool PreviewWorkflow(VfxFlow workflow, string nodeId = null, VfxFlow.Effect? solo = null)
        {
            if (chargeSequence == null || workflow == null || !TryGetTestHit(out var hit)) return false;
            StopWorkflow();
            chargeSequence.SetAccentTint(red ? Red : Cyan);
            return chargeSequence.Play(hit, workflow, nodeId, solo);
        }

        public void StopWorkflow()
        {
            if (chargeSequence == null) return;
            chargeSequence.Cancel();
            if (bodyCurrent != null) bodyCurrent.ClearShotPulse();
            impact.ClearTransient();
            if (battlePose != null) battlePose.ResetPose();
        }

        void PreviewImpact(RaycastHit hit)
        {
            Color color = red ? Red : Cyan;
            impact.Fire(hit, color, refraction, false, false, false, SequenceWaveScale);
        }

        void Shoot(RaycastHit hit)
        {
            Color color = red ? Red : Cyan;
            if (battlePose != null) battlePose.PrepareShot(hit.point);
            arc.Fire(muzzle.position, hit.point + hit.normal * 0.024f, color, branches);
            if (bodyCurrent != null) bodyCurrent.PulseShot();
            if (battlePose != null) battlePose.React(hit);
            RegisterImpact(hit);
        }

        void RegisterImpact(RaycastHit hit)
        {
            Color color = red ? Red : Cyan;
            impact.Fire(hit, color, refraction, physicsImpulse, cameraShake, scorch, SequenceWaveScale);
            nextShot = Time.time + ShotInterval;
            shots++;
            surface = hit.collider.name.ToUpperInvariant();
        }

        float SequenceWaveScale => chargeSequence != null && chargeSequence.Current == ChargeSequence.Phase.Impact ? 0.67f : 1f;

        public void ResetRange()
        {
            if (chargeSequence != null) chargeSequence.Cancel();
            if (bodyCurrent != null) bodyCurrent.ClearShotPulse();
            if (battlePose != null) battlePose.ResetPose();
            arc.Clear();
            impact.Clear();
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = startPositions[i];
                body.rotation = startRotations[i];
                body.Sleep();
            }
            Physics.SyncTransforms();
            nextShot = 0;
            shots = 0;
            surface = "READY";
        }

        float HudScale => Mathf.Clamp(Screen.height / (790f + CharacterEffectRows * 32f),
            CharacterEffectRows > 0 ? 0.56f : 0.72f, 1.45f);
        Rect ChargeRect => new(23, 153, 264, bodyCurrent != null ? 268 : 236);
        Rect CameraRect => new(23, ChargeRect.yMax + 13, 264, 113);
        Rect LightingRect => new(PanelRect.x, PanelRect.yMax + 12, PanelRect.width, 87);
        Light ActiveLight => adjustableLights.Length == 0 ? null : adjustableLights[lightIndex];

        void UpdateCameraLook(bool overScene)
        {
            if (Input.GetKeyDown(KeyCode.Home))
            {
                impact.ResetCameraPose(defaultCameraPosition, defaultCameraRotation);
                SetCameraFieldOfView(defaultFieldOfView);
                looking = false;
            }

            if (overScene && Input.GetMouseButtonDown(1))
            {
                Vector3 angles = view.transform.eulerAngles;
                cameraYaw = angles.y;
                cameraPitch = Mathf.DeltaAngle(0f, angles.x);
                looking = true;
            }
            if (!Input.GetMouseButton(1)) looking = false;
            if (!looking) return;

            float yawDelta = Input.GetAxisRaw("Mouse X") * CameraLookSpeed;
            float pitchDelta = Input.GetAxisRaw("Mouse Y") * CameraLookSpeed;
            if (yawDelta == 0f && pitchDelta == 0f) return;
            cameraYaw = Mathf.Repeat(cameraYaw + yawDelta, 360f);
            cameraPitch = Mathf.Clamp(cameraPitch - pitchDelta, -85f, 85f);
            impact.SetCameraRotation(Quaternion.Euler(cameraPitch, cameraYaw, 0f));
        }

        static Vector3 ReadMovement()
        {
            float side = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float depth = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float height = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            return new Vector3(side, height, depth);
        }

        void MoveCamera(Vector3 input)
        {
            if (input == Vector3.zero) return;
            Transform cameraTransform = view.transform;
            Vector3 direction = Vector3.ClampMagnitude(
                cameraTransform.right * input.x + cameraTransform.forward * input.z + Vector3.up * input.y, 1f);
            impact.MoveCamera(direction * (CameraMoveSpeed * Time.unscaledDeltaTime));
        }

        void SelectNextLight() => lightIndex = (lightIndex + 1) % adjustableLights.Length;

        void ResetSelectedLight()
        {
            if (ActiveLight == null) return;
            Pose pose = initialLightPoses[lightIndex];
            ActiveLight.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
        }

        void MoveSelectedLight(Vector3 input)
        {
            Light light = ActiveLight;
            if (light == null || input == Vector3.zero) return;
            Transform lamp = light.transform;
            float deltaTime = Time.unscaledDeltaTime;
            if (light.type == LightType.Directional)
            {
                Vector3 angles = lamp.eulerAngles;
                float yaw = angles.y + input.x * LightTurnSpeed * deltaTime;
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x) + input.z * LightTurnSpeed * deltaTime, -85f, 85f);
                lamp.rotation = Quaternion.Euler(pitch, yaw, 0f);
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = Vector3.ClampMagnitude(right * input.x + forward * input.z + Vector3.up * input.y, 1f);
            lamp.position += direction * (LightMoveSpeed * deltaTime);
        }

        void SetCameraFieldOfView(float degrees)
        {
            view.fieldOfView = Mathf.Clamp(degrees, NearFieldOfView, WideFieldOfView);
        }
        const float CharacterEffectHeight = 69f;
        int CharacterEffectRows => (faceSdfTarget ? 1 : 0) + (bangsTarget ? 1 : 0);
        Rect PanelRect => new(Screen.width / HudScale - 256, 24, 233, 421 + CharacterEffectHeight * CharacterEffectRows);

        void OnGUI()
        {
            if (hideHud) return;
            if (title == null)
                CreateStyles();
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(HudScale, HudScale, 1f));
            var accent = red ? new Color(1, 0.47f, 0.37f) : new Color(0.36f, 0.88f, 1);
            float width = Screen.width / HudScale;
            float height = Screen.height / HudScale;
            var lab = new Rect(23, 24, 264, 112);
            DrawPanel(lab, accent);
            GUI.Label(new Rect(40, 36, 233, 20), "URP  /  EFFECTS TEST RANGE", mono);
            GUI.Label(new Rect(39, 52, 235, 43), sceneTitle, title);
            GUI.Label(new Rect(40, 96, 235, 22), sceneDescription, subtitle);

            if (chargeSequence != null) DrawChargeControls(accent);
            if (allowCameraZoom) DrawCameraControls(accent);
            if (ActiveLight != null) DrawLightingControls(accent);

            Rect box = PanelRect;
            DrawPanel(box, accent);
            float x = box.x + 16;
            float y = box.y + 13;
            GUI.Label(new Rect(x, y, 200, 22), "EFFECT CONTROLS", mono);
            y += 30;
            if (Choice(new Rect(x, y, 96, 30), "1   ION BLUE", !red, accent))
                red = false;
            if (Choice(new Rect(x + 105, y, 96, 30), "2   RED ARC", red, accent))
                red = true;
            y += 43;
            bloom = Effect(x, y, "HDR bloom", bloom, accent);
            branches = Effect(x, y + 32, "Branch arcs", branches, accent);
            refraction = Effect(x, y + 64, "Scene refraction", refraction, accent);
            physicsImpulse = Effect(x, y + 96, "Physics impulse", physicsImpulse, accent);
            cameraShake = Effect(x, y + 128, "Camera shake", cameraShake, accent);
            scorch = Effect(x, y + 160, "Scorch decals", scorch, accent);
            if (ShaderChoice(x, y + 196, "Arc shader  [3]", useGraphArc, graphArcMaterial, accent))
                SetGraphArc(!useGraphArc);
            if (ShaderChoice(x, y + 228, "Wave shader  [4]", useGraphWave, graphWaveMaterial, accent))
                SetGraphWave(!useGraphWave);
            float characterY = y + 264;
            if (faceSdfTarget)
            {
                faceSdfTarget.FaceSdfEnabled = Effect(x, characterY, "Face SDF", faceSdfTarget.FaceSdfEnabled, accent);
                float strength = ShadowSlider(x, characterY, "Strength", faceSdfTarget.SdfStrength);
                float softness = ShadowSlider(x + 105, characterY, "Softness", faceSdfTarget.SdfSoftness);
                faceSdfTarget.SetSdfTuning(strength, softness);
                characterY += CharacterEffectHeight;
            }
            if (bangsTarget)
            {
                bool visible = Effect(x, characterY, "Bangs shadow", bangsTarget.ProjectionEnabled, accent);
                if (visible != bangsTarget.ProjectionEnabled) bangsTarget.ProjectionEnabled = visible;
                bangsTarget.Strength = ShadowSlider(x, characterY, "Strength", bangsTarget.Strength);
                bangsTarget.Softness = ShadowSlider(x + 105, characterY, "Softness", bangsTarget.Softness);
            }
            if (GUI.Button(new Rect(x, y + 269 + CharacterEffectHeight * CharacterEffectRows, 201, 35), "RESET RANGE   [R]", button))
                ResetRange();

            DrawPanel(new Rect(23, height - 68, width - 46, 45), accent);
            GUI.Label(new Rect(39, height - 57, 410, 20), allowHideHud ? "LMB  Fire     SPACE  Hit target     R  Reset     TAB  Hide controls" :
                "LMB  Fire     SPACE  Hit test bay     R  Reset", caption);
            if (width >= 730)
                GUI.Label(new Rect(width - 306, height - 57, 263, 21), $"{shots:000} SHOTS  /  {surface}", mono);

            if (hasAim && Event.current.type == EventType.Repaint)
            {
                Vector2 pointer = Event.current.mousePosition;
                Fill(new Rect(pointer.x - 9, pointer.y, 6, 1), accent);
                Fill(new Rect(pointer.x + 4, pointer.y, 6, 1), accent);
                Fill(new Rect(pointer.x, pointer.y - 9, 1, 6), accent);
                Fill(new Rect(pointer.x, pointer.y + 4, 1, 6), accent);
            }

            GUI.matrix = previous;
        }

        float ShadowSlider(float x, float y, string label, float value)
        {
            GUI.Label(new Rect(x, y + 32, 98, 17), $"{label} {value:P0}", caption);
            return GUI.HorizontalSlider(new Rect(x, y + 51, 96, 15), value, 0f, 1f);
        }

        void DrawChargeControls(Color accent)
        {
            Rect box = ChargeRect;
            DrawPanel(box, accent);
            float x = box.x + 16;
            float y = box.y;
            GUI.Label(new Rect(x, y + 9, 225, 22), "HAND CHARGE", mono);
            var hand = chargeSequence.Hand;
            hand.ShowArcs = Effect(x, y + 34, "White branch arcs", hand.ShowArcs, accent, 232);
            hand.ShowSparks = Effect(x, y + 65, "Particle sparks", hand.ShowSparks, accent, 232);

            if (bodyCurrent != null)
            {
                bodyCurrent.ShowCurrent = Effect(x, y + 96, "Body current", bodyCurrent.ShowCurrent, accent, 232);
                y += 32;
            }

            bool preview = chargeSequence.Current == ChargeSequence.Phase.Preview;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !chargeSequence.Busy;
            if (Choice(new Rect(x, y + 104, 112, 31), "PREVIEW  [C]", preview, accent))
                chargeSequence.TogglePreview();
            if (Choice(new Rect(x + 120, y + 104, 112, 31), "PLAY CHAIN  [F]", false, accent))
                PlayChargeSequence();
            GUI.enabled = enabled;

            var current = chargeSequence.Current;
            DrawPhase(x, y + 150, "CHARGE", current is ChargeSequence.Phase.Charge or ChargeSequence.Phase.Preview, accent);
            DrawPhase(x + 80, y + 150, "TRAVEL", current == ChargeSequence.Phase.Travel, accent);
            DrawPhase(x + 160, y + 150, "IMPACT", current == ChargeSequence.Phase.Impact, accent);
            Fill(new Rect(x, y + 184, 232, 3), new Color(0.24f, 0.34f, 0.41f));
            Fill(new Rect(x, y + 184, 232 * chargeSequence.Progress, 3), accent);
            string phase = current.ToString().ToUpperInvariant();
            if (bodyCurrent != null && bodyCurrent.IsVisible)
                phase = current == ChargeSequence.Phase.Idle ? "SHOT  /  BODY CURRENT" : phase + "  /  BODY CURRENT";
            GUI.Label(new Rect(x, y + 198, 225, 22), phase, mono);
        }

        void DrawCameraControls(Color accent)
        {
            Rect box = CameraRect;
            DrawPanel(box, accent);
            float x = box.x + 16;
            GUI.Label(new Rect(x, box.y + 7, 158, 22), $"CAMERA FOV  {view.fieldOfView:0}°", mono);
            if (GUI.Button(new Rect(x + 160, box.y + 7, 72, 23), "DEFAULT", button))
                SetCameraFieldOfView(defaultFieldOfView);

            GUI.Label(new Rect(x, box.y + 40, 46, 20), "Close", caption);
            GUI.Label(new Rect(x + 204, box.y + 40, 38, 20), "Wide", caption);
            float current = view.fieldOfView;
            float zoom = GUI.HorizontalSlider(new Rect(x + 44, box.y + 44, 151, 18), current,
                NearFieldOfView, WideFieldOfView);
            if (!Mathf.Approximately(current, zoom)) SetCameraFieldOfView(zoom);
            GUI.Label(new Rect(x, box.y + 65, 232, 20), "WASD  Move     Q/E  Height", caption);
            GUI.Label(new Rect(x, box.y + 85, 236, 20), "RMB Look   Wheel Zoom   Home Reset", caption);
        }

        void DrawLightingControls(Color accent)
        {
            Rect box = LightingRect;
            DrawPanel(box, accent);
            float x = box.x + 16;
            float y = box.y + 8;
            if (Choice(new Rect(x, y, 112, 24), lightMode ? "LIGHT MODE [T]" : "EDIT LIGHT [T]", lightMode, accent))
                lightMode = !lightMode;
            if (GUI.Button(new Rect(x + 121, y, 80, 24), "NEXT [L]", button)) SelectNextLight();
            Light light = ActiveLight;
            if (light == null) return;
            GUI.Label(new Rect(x, y + 29, 148, 20), light.name, caption);
            if (GUI.Button(new Rect(x + 151, y + 28, 50, 22), "RESET", button)) ResetSelectedLight();
            string controls = light.type == LightType.Directional ? "WASD   Aim light" : "WASD   Move     Q/E   Height";
            if (!lightMode) controls = "Press T to edit lights.";
            GUI.Label(new Rect(x, y + 47, 207, 20), controls, caption);
        }

        void DrawPhase(float x, float y, string text, bool active, Color accent)
        {
            Fill(new Rect(x, y, 72, 27), active ? new Color(accent.r, accent.g, accent.b, 0.24f) :
                new Color(0.14f, 0.20f, 0.25f, 0.82f));
            GUI.Label(new Rect(x + 5, y + 3, 67, 20), text, mono);
        }

        void CreateStyles()
        {
            var ink = new Color(0.86f, 0.93f, 0.98f);
            var muted = new Color(0.59f, 0.72f, 0.80f);
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = ink } };
            float titleWidth = title.CalcSize(new GUIContent(sceneTitle)).x;
            if (titleWidth > 235f)
                title.fontSize = Mathf.FloorToInt(title.fontSize * 235f / titleWidth);
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = ink } };
            caption = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = ink } };
            mono = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = muted } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            panel = new GUIStyle(GUI.skin.box);
        }

        void DrawPanel(Rect rect, Color accent)
        {
            Fill(rect, new Color(0.035f, 0.060f, 0.084f, 0.94f));
            GUI.Box(rect, GUIContent.none, panel);
            Fill(new Rect(rect.x, rect.y, 2, rect.height), accent * new Color(1, 1, 1, 0.78f));
        }

        bool Choice(Rect rect, string text, bool selected, Color accent)
        {
            Color original = GUI.backgroundColor;
            GUI.backgroundColor = selected ? Color.Lerp(Color.white, accent, 0.72f) : new Color(0.51f, 0.59f, 0.66f);
            bool clicked = GUI.Button(rect, text, button);
            GUI.backgroundColor = original;
            return clicked;
        }

        bool Effect(float x, float y, string label, bool value, Color accent, float rowWidth = 201)
        {
            Fill(new Rect(x, y + 28, rowWidth, 1), new Color(0.27f, 0.39f, 0.46f, 0.42f));
            GUI.Label(new Rect(x, y + 2, 146, 25), label, caption);
            return Choice(new Rect(x + rowWidth - 50, y + 1, 50, 24), value ? "ON" : "OFF", value, accent) ? !value : value;
        }

        bool ShaderChoice(float x, float y, string label, bool graph, Material graphMaterial, Color accent)
        {
            GUI.Label(new Rect(x, y + 2, 128, 25), label, caption);
            bool previous = GUI.enabled;
            GUI.enabled = previous && graphMaterial != null;
            bool clicked = Choice(new Rect(x + 129, y + 1, 72, 24), graph ? "GRAPH" : "HLSL", graph, accent);
            GUI.enabled = previous;
            return clicked;
        }

        static void Fill(Rect rect, Color color)
        {
            Color original = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = original;
        }
    }
}

