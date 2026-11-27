using UnityEngine;

namespace InteractiveFog
{
    /// <summary>
    /// Runs the 2D Stable Fluids simulation on the GPU and publishes the global shader values
    /// that InteractiveFogFeature's raymarch pass reads. The domain is centred on this transform.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FogSimulation : MonoBehaviour
    {
        public static FogSimulation Active { get; private set; }
        const int MaxInteractors = 16;

        [Header("Resources")]
        public ComputeShader compute;

        [Header("Domain")]
        [Min(32)] public int resolution = 256;
        [Min(1f)] public float worldSize = 40f;

        [Header("Simulation")]
        public bool stir = true;
        [Range(0f, 12f)] public float vorticity = 3.4f;
        public bool curlWind = true;
        [Range(0f, 20f)] public float windStrength = 7f;
        public Vector2 windDrift = new Vector2(0.22f, 0.08f);
        [Range(0f, 1f)] public float regenRate = 0.1f;
        [Range(0f, 2f)] public float velocityDamping = 0.22f;
        [Range(4, 60)] public int pressureIterations = 24;

        [Header("Look")]
        [Tooltip("Fog follows this terrain's surface. Leave empty for a flat ground at Flat Ground Y")]
        public Terrain terrain;
        public float flatGroundY = 0f;
        [Tooltip("Defaults to RenderSettings.sun")]
        public Light sun;
        [Range(0.2f, 6f)] public float density = 1.8f;
        public float baseThickness = 0.7f;
        public float thicknessByDensity = 3.2f;
        [Range(16, 128)] public int raySteps = 56;
        public Color litColor = new Color(1f, 0.97f, 0.91f);
        public Color shadowColor = new Color(0.08f, 0.08f, 0.09f);

        public RenderTexture DensityTexture => _denA;

        RenderTexture _velA, _velB, _prsA, _prsB, _denA, _denB, _curl, _div;
        int _n, _groups;
        float _simTime;
        readonly Vector4[] _ipos = new Vector4[MaxInteractors];
        readonly Vector4[] _ivel = new Vector4[MaxInteractors];
        readonly Vector4[] _iext = new Vector4[MaxInteractors];

        int kClearVel, kClearScalar, kInit, kCurl, kVort, kForces, kDiv, kScale, kJacobi, kGrad, kAdvVel, kAdvDen;

        static readonly int VelIn = Shader.PropertyToID("_VelIn"), VelOut = Shader.PropertyToID("_VelOut"),
            SIn = Shader.PropertyToID("_SIn"), SOut = Shader.PropertyToID("_SOut"),
            CurlTex = Shader.PropertyToID("_CurlTex"), DivTex = Shader.PropertyToID("_DivTex");

        void OnEnable()
        {
            if (compute == null) { Debug.LogError("[InteractiveFog] FogSimulation needs the FluidSim compute shader.", this); enabled = false; return; }
            if (!SystemInfo.supportsComputeShaders) { Debug.LogError("[InteractiveFog] Compute shaders not supported."); enabled = false; return; }

            kClearVel = compute.FindKernel("ClearVel");
            kClearScalar = compute.FindKernel("ClearScalar");
            kInit = compute.FindKernel("InitDensity");
            kCurl = compute.FindKernel("Curl");
            kVort = compute.FindKernel("Vorticity");
            kForces = compute.FindKernel("Forces");
            kDiv = compute.FindKernel("Divergence");
            kScale = compute.FindKernel("ScaleScalar");
            kJacobi = compute.FindKernel("Jacobi");
            kGrad = compute.FindKernel("Gradient");
            kAdvVel = compute.FindKernel("AdvectVelocity");
            kAdvDen = compute.FindKernel("AdvectDensity");

            _n = resolution;
            _groups = Mathf.CeilToInt(_n / 8f);
            _velA = Make(RenderTextureFormat.RGHalf, "FogVelA");
            _velB = Make(RenderTextureFormat.RGHalf, "FogVelB");
            _prsA = Make(RenderTextureFormat.RHalf, "FogPrsA");
            _prsB = Make(RenderTextureFormat.RHalf, "FogPrsB");
            _denA = Make(RenderTextureFormat.RHalf, "FogDenA");
            _denB = Make(RenderTextureFormat.RHalf, "FogDenB");
            _curl = Make(RenderTextureFormat.RHalf, "FogCurl");
            _div = Make(RenderTextureFormat.RHalf, "FogDiv");

            Active = this;
            ResetFields();
            PublishGlobals();
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            foreach (var rt in new[] { _velA, _velB, _prsA, _prsB, _denA, _denB, _curl, _div })
                if (rt != null) { rt.Release(); Destroy(rt); }
            _velA = _velB = _prsA = _prsB = _denA = _denB = _curl = _div = null;
        }

        RenderTexture Make(RenderTextureFormat f, string n)
        {
            var rt = new RenderTexture(_n, _n, 0, f, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = n
            };
            rt.Create();
            return rt;
        }

        Vector2 Origin => new Vector2(transform.position.x - worldSize * 0.5f, transform.position.z - worldSize * 0.5f);

        void SetCommon(float dt)
        {
            compute.SetInt("_N", _n);
            compute.SetFloat("_H", worldSize / _n);
            compute.SetFloat("_W", worldSize);
            compute.SetVector("_Origin", Origin);
            compute.SetFloat("_Dt", dt);
            compute.SetFloat("_SimTime", _simTime);
        }

        void Run(int k) => compute.Dispatch(k, _groups, _groups, 1);
        static void Swap(ref RenderTexture a, ref RenderTexture b) { var t = a; a = b; b = t; }

        /// <summary>Clears velocity/pressure and refills the density field.</summary>
        public void ResetFields()
        {
            if (_velA == null) return;
            SetCommon(0f);
            foreach (var rt in new[] { _velA, _velB }) { compute.SetTexture(kClearVel, VelOut, rt); Run(kClearVel); }
            foreach (var rt in new[] { _prsA, _prsB }) { compute.SetTexture(kClearScalar, SOut, rt); Run(kClearScalar); }
            compute.SetTexture(kInit, SOut, _denA); Run(kInit);
        }

        void Update()
        {
            float realDt = Time.deltaTime;
            if (realDt <= 0f) return;
            float dt = Mathf.Min(realDt, 1f / 30f);
            _simTime += dt;

            // gather interactors
            int count = 0;
            foreach (var it in FogInteractor.All)
            {
                if (count >= MaxInteractors) break;
                Vector3 p = it.transform.position;
                Vector2 v = it.SampleVelocity(realDt);
                _ipos[count] = new Vector4(p.x, p.z, it.radius, it.carve);
                _ivel[count] = new Vector4(v.x, v.y, it.push, it.follow);
                _iext[count] = new Vector4(it.pushBySpeed, 0, 0, 0);
                count++;
            }
            for (int i = count; i < MaxInteractors; i++) { _ipos[i] = _ivel[i] = _iext[i] = Vector4.zero; }

            SetCommon(dt);
            compute.SetInt("_ICount", count);
            compute.SetVectorArray("_IPos", _ipos);
            compute.SetVectorArray("_IVel", _ivel);
            compute.SetVectorArray("_IExtra", _iext);
            compute.SetFloat("_Eps", vorticity * (worldSize / _n));
            compute.SetFloat("_Wind", curlWind ? 1f : 0f);
            compute.SetFloat("_WindStrength", windStrength);
            compute.SetVector("_WindDrift", windDrift);
            compute.SetFloat("_Regen", regenRate);
            compute.SetFloat("_VelDiss", Mathf.Exp(-dt * velocityDamping));

            // 1. curl
            compute.SetTexture(kCurl, VelIn, _velA); compute.SetTexture(kCurl, SOut, _curl); Run(kCurl);
            // 2. vorticity confinement ("Stir")
            if (stir)
            {
                compute.SetTexture(kVort, VelIn, _velA); compute.SetTexture(kVort, CurlTex, _curl);
                compute.SetTexture(kVort, VelOut, _velB); Run(kVort); Swap(ref _velA, ref _velB);
            }
            // 3. external forces
            compute.SetTexture(kForces, VelIn, _velA); compute.SetTexture(kForces, VelOut, _velB);
            Run(kForces); Swap(ref _velA, ref _velB);
            // 4. projection
            compute.SetTexture(kDiv, VelIn, _velA); compute.SetTexture(kDiv, SOut, _div); Run(kDiv);
            compute.SetFloat("_Scale", 0.8f);
            compute.SetTexture(kScale, SIn, _prsA); compute.SetTexture(kScale, SOut, _prsB); Run(kScale); Swap(ref _prsA, ref _prsB);
            compute.SetTexture(kJacobi, DivTex, _div);
            for (int i = 0; i < pressureIterations; i++)
            {
                compute.SetTexture(kJacobi, SIn, _prsA); compute.SetTexture(kJacobi, SOut, _prsB);
                Run(kJacobi); Swap(ref _prsA, ref _prsB);
            }
            compute.SetTexture(kGrad, SIn, _prsA); compute.SetTexture(kGrad, VelIn, _velA); compute.SetTexture(kGrad, VelOut, _velB);
            Run(kGrad); Swap(ref _velA, ref _velB);
            // 5. advection
            compute.SetTexture(kAdvVel, VelIn, _velA); compute.SetTexture(kAdvVel, VelOut, _velB);
            Run(kAdvVel); Swap(ref _velA, ref _velB);
            compute.SetTexture(kAdvDen, VelIn, _velA); compute.SetTexture(kAdvDen, SIn, _denA); compute.SetTexture(kAdvDen, SOut, _denB);
            Run(kAdvDen); Swap(ref _denA, ref _denB);

            PublishGlobals();
        }

        void PublishGlobals()
        {
            Vector2 o = Origin;
            Shader.SetGlobalTexture("_FogDensityTex", _denA);
            Shader.SetGlobalVector("_FogDomain", new Vector4(o.x, o.y, 1f / worldSize, 0f));
            Shader.SetGlobalVector("_FogShape", new Vector4(baseThickness, thicknessByDensity, density, raySteps));

            float gMin, gMax;
            if (terrain != null && terrain.terrainData != null)
            {
                var td = terrain.terrainData;
                Vector3 tp = terrain.transform.position;
                gMin = tp.y;
                gMax = tp.y + td.size.y;
                Shader.SetGlobalTexture("_FogHeightTex", td.heightmapTexture);
                Shader.SetGlobalVector("_FogGroundA", new Vector4(tp.x, tp.z, 1f / td.size.x, 1f / td.size.z));
                // heightmap stores normalized height in [0, 32766/65535]
                Shader.SetGlobalVector("_FogGroundB", new Vector4(tp.y, td.size.y / (32766f / 65535f), 1f, flatGroundY));
                Shader.SetGlobalVector("_FogGroundC", new Vector4(td.heightmapResolution, 0, 0, 0));
            }
            else
            {
                gMin = gMax = flatGroundY;
                Shader.SetGlobalTexture("_FogHeightTex", Texture2D.blackTexture);
                Shader.SetGlobalVector("_FogGroundB", new Vector4(0, 0, 0, flatGroundY));
                Shader.SetGlobalVector("_FogGroundC", new Vector4(4, 0, 0, 0));
            }
            Shader.SetGlobalVector("_FogBounds", new Vector4(gMin - 0.6f, gMax + baseThickness + thicknessByDensity * 1.3f + 0.5f, 0, 0));
            Shader.SetGlobalVector("_FogLit", litColor.linear);
            Shader.SetGlobalVector("_FogShadow", shadowColor.linear);

            Light l = sun != null ? sun : RenderSettings.sun;
            Vector3 dir = l != null ? -l.transform.forward : new Vector3(0.45f, 0.8f, 0.35f).normalized;
            Color sc = l != null ? l.color.linear * l.intensity : Color.white;
            Shader.SetGlobalVector("_FogSunDir", dir);
            Shader.SetGlobalVector("_FogSunColor", sc);
            Shader.SetGlobalVector("_FogTime", new Vector4(_simTime, 0, 0, 0));
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(worldSize, 0.1f, worldSize));
        }
    }
}
