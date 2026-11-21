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
        [SerializeField] BattlePose battlePose;
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

        const float ShotInterval = 0.64f;
        static readonly Color Cyan = new(0.08f, 0.71f, 1f);
        static readonly Color Red = new(1f, 0.14f, 0.075f);
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        Bloom bloomOverride;
        MaterialPropertyBlock emitterProperties;
        Vector3[] startPositions;
        Quaternion[] startRotations;
        GUIStyle title, subtitle, caption, mono, button, panel;
        bool red, hasAim;
        bool useGraphArc, useGraphWave;
        float nextShot;
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
            emitterProperties.SetColor(EmissionColor, (red ? Red : Cyan) * 3f);
            emitter.SetPropertyBlock(emitterProperties);

            Vector3 pointer = Input.mousePosition;
            bool inside = pointer.x >= 0 && pointer.x < Screen.width && pointer.y >= 0 && pointer.y < Screen.height;
            Vector2 hudPointer = new Vector2(pointer.x, Screen.height - pointer.y) / HudScale;
            bool overControls = PanelRect.Contains(hudPointer) ||
                (chargeSequence != null && ChargeRect.Contains(hudPointer));
            Ray ray = view.ScreenPointToRay(pointer);
            RaycastHit hit = default;
            hasAim = inside && !overControls && Physics.Raycast(ray, out hit, 50f);
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
            if (TryGetTestHit(out var hit))
                chargeSequence.Play(hit);
        }

        public bool PreviewWorkflow(VfxFlow workflow, string nodeId = null, VfxFlow.Effect? solo = null)
        {
            if (chargeSequence == null || workflow == null || !TryGetTestHit(out var hit)) return false;
            StopWorkflow();
            return chargeSequence.Play(hit, workflow, nodeId, solo);
        }

        public void StopWorkflow()
        {
            if (chargeSequence == null) return;
            chargeSequence.Cancel();
            impact.ClearTransient();
            if (battlePose != null) battlePose.ResetPose();
        }

        void PreviewImpact(RaycastHit hit)
        {
            Color color = red ? Red : Cyan;
            impact.Fire(hit, color, refraction, false, false, false);
        }

        void Shoot(RaycastHit hit)
        {
            Color color = red ? Red : Cyan;
            arc.Fire(muzzle.position, hit.point + hit.normal * 0.024f, color, branches);
            if (battlePose != null) battlePose.React(hit);
            RegisterImpact(hit);
        }

        void RegisterImpact(RaycastHit hit)
        {
            Color color = red ? Red : Cyan;
            impact.Fire(hit, color, refraction, physicsImpulse, cameraShake, scorch);
            nextShot = Time.time + ShotInterval;
            shots++;
            surface = hit.collider.name.ToUpperInvariant();
        }

        public void ResetRange()
        {
            if (chargeSequence != null) chargeSequence.Cancel();
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

        float HudScale => Mathf.Clamp(Screen.height / 790f, 0.72f, 1.45f);
        static Rect ChargeRect => new(23, 153, 264, 236);
        Rect PanelRect => new(Screen.width / HudScale - 256, 24, 233, 421);

        void OnGUI()
        {
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
            if (GUI.Button(new Rect(x, y + 269, 201, 35), "RESET RANGE   [R]", button))
                ResetRange();

            DrawPanel(new Rect(23, height - 68, width - 46, 45), accent);
            GUI.Label(new Rect(39, height - 57, 410, 20), "LMB  Fire     SPACE  Hit test bay     R  Reset", caption);
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
            GUI.Label(new Rect(x, y + 198, 225, 22), phase, mono);
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
