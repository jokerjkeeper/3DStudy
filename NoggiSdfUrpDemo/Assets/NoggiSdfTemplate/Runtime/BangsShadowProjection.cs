using UnityEngine;
using UnityEngine.Rendering;

namespace NoggiSdfToolkit
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Bangs Shadow Projection")]
    public sealed class BangsShadowProjection : MonoBehaviour
    {
        static readonly int Depth = Shader.PropertyToID("_BangsDepth");
        static readonly int ViewProjection = Shader.PropertyToID("_BangsVP");
        static readonly int Origin = Shader.PropertyToID("_BangsOrigin");
        static readonly int Direction = Shader.PropertyToID("_BangsDirection");
        static readonly int Parameters = Shader.PropertyToID("_BangsParams");
        static readonly int SoftnessId = Shader.PropertyToID("_BangsSoftness");

        [SerializeField] Renderer caster;
        [SerializeField, Min(0)] int casterSlot;
        [SerializeField] Renderer receiver;
        [SerializeField, Min(0)] int receiverSlot;
        [SerializeField] Transform headBasis;
        [SerializeField] Vector3 localForward = Vector3.back;
        [SerializeField] Light key;
        [SerializeField] Shader depthShader;
        [SerializeField] bool projectionEnabled = true;

        [Header("Fringe region (world units, relative to face center)")]
        [SerializeField, Min(.01f)] float halfWidth = .087f;
        [SerializeField] float minimumHeight = -.028f;
        [SerializeField] float minimumForward = .025f;
        [SerializeField] float minimumReceiverHeight = -.063f;
        [SerializeField, Min(.05f)] float captureRadius = .125f;
        [SerializeField, Range(0f, 1f)] float strength = .72f;
        [SerializeField, Range(0f, 1f)] float softness = .21666667f;
        [SerializeField, Min(0f)] float depthBias = .0028f;

        const int Resolution = 512;
        RenderTexture depth;
        Material depthMaterial;
        MaterialPropertyBlock properties;

        public float Strength
        {
            get => strength;
            set => strength = Mathf.Clamp01(value);
        }

        public float Softness
        {
            get => softness;
            set => softness = Mathf.Clamp01(value);
        }

        public bool ProjectionEnabled
        {
            get => projectionEnabled;
            set
            {
                projectionEnabled = value;
                if (!value) ClearProjection();
            }
        }

        public void Configure(Renderer hair, int hairSlot, Renderer face, int faceSlot, Transform basis,
            Vector3 forward, Light mainLight, Shader shader)
        {
            caster = hair;
            casterSlot = hairSlot;
            receiver = face;
            receiverSlot = faceSlot;
            headBasis = basis;
            localForward = forward;
            key = mainLight;
            depthShader = shader;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Capture;

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Capture;
            ClearProjection();
            if (depth) { depth.Release(); Release(depth); }
            if (depthMaterial) Release(depthMaterial);
            depth = null;
            depthMaterial = null;
        }

        static void Release(Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }

        void Capture(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
                return;

            var light = key ? key : RenderSettings.sun;
            if (!projectionEnabled || !caster || !caster.enabled || !receiver || !receiver.enabled ||
                !caster.gameObject.activeInHierarchy || !light || !light.isActiveAndEnabled ||
                light.type != LightType.Directional || !depthShader || !depthShader.isSupported ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            {
                ClearProjection();
                return;
            }

            Transform basis = headBasis ? headBasis : receiver.transform;
            Vector3 front = basis.TransformDirection(localForward).normalized;
            Vector3 up = basis.up;
            Vector3 towardLight = -light.transform.forward;
            if (Vector3.Dot(towardLight, front) < .08f)
            {
                ClearProjection();
                return;
            }

            if (!depth)
            {
                depth = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                {
                    name = "Fringe projection linear depth", filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
                };
                depth.Create();
                depthMaterial = new Material(depthShader) { hideFlags = HideFlags.HideAndDontSave };
            }

            Vector3 center = receiver.bounds.center;
            Vector3 lightForward = -towardLight;
            Vector3 origin = center + front * .025f + up * .027f + towardLight * captureRadius * 2.4f;
            const float near = .01f;
            float far = captureRadius * 5f;
            Vector3 captureUp = Mathf.Abs(Vector3.Dot(lightForward, up)) > .97f ? front : up;
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                Matrix4x4.TRS(origin, Quaternion.LookRotation(lightForward, captureUp), Vector3.one).inverse;
            Matrix4x4 projection = Matrix4x4.Ortho(-captureRadius, captureRadius, -captureRadius, captureRadius, near, far);
            Matrix4x4 vp = GL.GetGPUProjectionMatrix(projection, true) * view;
            Vector4 direction = new(lightForward.x, lightForward.y, lightForward.z, far);

            depthMaterial.SetMatrix(ViewProjection, vp);
            depthMaterial.SetVector(Origin, origin);
            depthMaterial.SetVector(Direction, direction);
            depthMaterial.SetVector("_FringeCenter", center);
            depthMaterial.SetVector("_FringeUp", up);
            depthMaterial.SetVector("_FringeFront", front);
            depthMaterial.SetVector("_FringeRight", Vector3.Cross(up, front).normalized);
            depthMaterial.SetVector("_FringeBounds", new Vector4(halfWidth, minimumHeight, minimumForward, 0));

            var commands = CommandBufferPool.Get("Bangs-only light depth");
            commands.SetRenderTarget(depth);
            commands.ClearRenderTarget(true, true, Color.white);
            commands.DrawRenderer(caster, depthMaterial, casterSlot, 0);
            context.ExecuteCommandBuffer(commands);
            CommandBufferPool.Release(commands);

            properties ??= new MaterialPropertyBlock();
            receiver.GetPropertyBlock(properties, receiverSlot);
            properties.SetTexture(Depth, depth);
            properties.SetMatrix(ViewProjection, vp);
            properties.SetVector(Origin, origin);
            properties.SetVector(Direction, direction);
            properties.SetVector("_BangsFaceCenter", center);
            properties.SetVector("_BangsReceiverUp", new Vector4(up.x, up.y, up.z, minimumReceiverHeight));
            properties.SetFloat(SoftnessId, softness);
            properties.SetVector(Parameters, new Vector4(strength, 1f / Resolution, depthBias, 1));
            receiver.SetPropertyBlock(properties, receiverSlot);
        }

        void ClearProjection()
        {
            if (!receiver || properties == null) return;
            receiver.GetPropertyBlock(properties, receiverSlot);
            Vector4 status = properties.GetVector(Parameters);
            status.w = 0;
            properties.SetVector(Parameters, status);
            properties.SetTexture(Depth, Texture2D.whiteTexture);
            receiver.SetPropertyBlock(properties, receiverSlot);
        }
    }
}
