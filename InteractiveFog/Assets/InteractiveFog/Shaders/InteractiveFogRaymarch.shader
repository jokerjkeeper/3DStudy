// Full-screen raymarch of a terrain-following fog layer.
// Fog thickness and density come from the 2D fluid density texture; 3D value noise adds detail.
// Beer-Lambert extinction + two-tap self shadow towards the sun (simplified Schneider / Hillaire 2015).
Shader "Hidden/InteractiveFog/Raymarch"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "InteractiveFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "FogNoise.hlsl"

            TEXTURE2D(_FogDensityTex);
            TEXTURE2D(_FogHeightTex);
            SamplerState fog_linear_clamp_sampler;
            SamplerState fog_point_clamp_sampler;

            float4 _FogDomain;   // origin.x, origin.z, 1/worldSize, -
            float4 _FogShape;    // baseThickness, thicknessByDensity, extinction, steps
            float4 _FogBounds;   // yBottom, yTop
            float4 _FogGroundA;  // terrain pos.x, pos.z, 1/size.x, 1/size.z
            float4 _FogGroundB;  // terrain pos.y, height scale, hasTerrain, flat ground y
            float4 _FogGroundC;  // heightmap resolution
            float4 _FogLit;
            float4 _FogShadow;
            float4 _FogSunDir;
            float4 _FogSunColor;
            float4 _FogTime;

            float Ground(float2 xz)
            {
                if (_FogGroundB.z < 0.5) return _FogGroundB.w;
                float res = _FogGroundC.x;
                float2 uv = saturate((xz - _FogGroundA.xy) * _FogGroundA.zw);
                uv = uv * (res - 1.0) / res + 0.5 / res;
                return _FogGroundB.x + SAMPLE_TEXTURE2D_LOD(_FogHeightTex, fog_linear_clamp_sampler, uv, 0).r * _FogGroundB.y;
            }

            float FogDensity(float3 p)
            {
                float2 duv = (p.xz - _FogDomain.xy) * _FogDomain.z;
                float D = SAMPLE_TEXTURE2D_LOD(_FogDensityTex, fog_linear_clamp_sampler, duv, 0).r;
                float a = p.y - Ground(p.xz);
                float th = _FogShape.x + _FogShape.y * D;
                float sh = smoothstep(-0.45, 0.25, a) * (1.0 - smoothstep(th * 0.15, th, a));
                if (sh <= 0.0) return 0.0;
                float t = _FogTime.x;
                float n = vn3(p * float3(0.55, 0.35, 0.55) + float3(t * 0.06, -t * 0.035, 0.0)) * 0.7
                        + vn3(p * float3(1.3, 0.8, 1.3) + float3(0.0, t * 0.08, t * 0.03)) * 0.3;
                float x = D * sh * (0.25 + n * 1.1);
                return x * smoothstep(0.05, 0.3, x);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, fog_point_clamp_sampler, input.texcoord, 0).rgb;

                float2 suv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(suv);
                float3 cam = _WorldSpaceCameraPos;
                float3 wp  = ComputeWorldSpacePosition(suv, depth, UNITY_MATRIX_I_VP);
                float3 rd  = normalize(wp - cam);
                float tEnd = distance(wp, cam);
            #if UNITY_REVERSED_Z
                if (depth <= 1e-6) tEnd = 1e6;
            #else
                if (depth >= 1.0 - 1e-6) tEnd = 1e6;
            #endif

                float t0 = 0.0, t1 = tEnd;
                if (abs(rd.y) > 1e-4)
                {
                    float a = (_FogBounds.y - cam.y) / rd.y;
                    float b = (_FogBounds.x - cam.y) / rd.y;
                    t0 = max(t0, min(a, b));
                    t1 = min(t1, max(a, b));
                }
                else if (cam.y > _FogBounds.y || cam.y < _FogBounds.x) t1 = t0;

                float3 acc = 0;
                float T = 1.0;
                if (t1 > t0)
                {
                    int steps = (int)_FogShape.w;
                    float st = (t1 - t0) / steps;
                    float tt = t0 + st * h21(input.positionCS.xy);
                    float dk = _FogShape.z;
                    float3 L = _FogSunDir.xyz;

                    [loop] for (int k = 0; k < steps; k++)
                    {
                        float3 p = cam + rd * tt;
                        float d = FogDensity(p);
                        if (d > 0.002)
                        {
                            float od = FogDensity(p + L * 0.55) + FogDensity(p + L * 1.5);
                            float lt = exp(-od * dk * 0.55);
                            float a = p.y - Ground(p.xz);
                            float3 c = lerp(_FogShadow.rgb, _FogLit.rgb * _FogSunColor.rgb, lt)
                                     * (0.65 + 0.35 * smoothstep(0.0, 2.6, a));
                            float e = exp(-d * dk * st);
                            acc += T * (1.0 - e) * c;
                            T *= e;
                            if (T < 0.01) break;
                        }
                        tt += st;
                    }
                }
                return half4(scene * T + acc, 1.0);
            }
            ENDHLSL
        }
    }
}
