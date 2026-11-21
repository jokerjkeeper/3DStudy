Shader "StormLab/Perlica Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_PERLICA_FACE)] _Face("Use Face SDF", Float) = 0
        [HideInInspector] _FaceSdfDisabled ("Face SDF bypass", Float) = 0
        [NoScaleOffset] _FaceSdf("Face SDF", 2D) = "gray" {}
        [ToggleUI] _CleanFaceSdf("Use Clean Red SDF", Float) = 0
        [ToggleUI] _ReverseCleanSdf("Reverse Clean SDF", Float) = 0
        [NoScaleOffset] _LipGlossMap("Lips Gloss (Alpha Mask)", 2D) = "black" {}
        _LipGlossStrength("Lip Gloss Strength", Range(0, 1)) = 0
        _FaceForward("Face Forward (Object Space)", Vector) = (0, 0, 1, 0)
        _FaceRight("Face Right (Object Space)", Vector) = (1, 0, 0, 0)
        _FaceShadowBias("Face Shadow Bias", Range(-0.2, 0.25)) = 0.035
        _FaceShadowStrength("Face Shadow Strength", Range(0, 1)) = 1
        _FaceShadowSoftness("Face Shadow Softness", Range(0.01, 0.18)) = 0.065
        _FaceShadowTint("Face Shadow Tint", Color) = (0.91, 0.86, 0.87, 1)
        [Enum(Shaded,0,Light Mask,1,Distance Field,2)] _FaceView("Face Preview", Float) = 0
        [NoScaleOffset] _HairMask("Hair Mask", 2D) = "white" {}
        [NoScaleOffset] _HairSphere("Hair Sphere Texture", 2D) = "black" {}
        _HighlightStrength("Hair Highlight Strength", Range(0, 3)) = 0.75
        _HairSpecStrength("Directional Hair Glint", Range(0, 0.5)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _PERLICA_FACE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BangsProjection.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FaceSdf); SAMPLER(sampler_FaceSdf);
            TEXTURE2D(_HairMask); SAMPLER(sampler_HairMask);
            TEXTURE2D(_HairSphere); SAMPLER(sampler_HairSphere);
            TEXTURE2D(_LipGlossMap); SAMPLER(sampler_LipGlossMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _FaceForward;
            float4 _FaceRight;
            half _Face;
            half _FaceShadowBias;
            half _FaceShadowStrength;
            half _FaceShadowSoftness;
            half4 _FaceShadowTint;
            half _FaceView;
            half _FaceSdfDisabled;
            half _HighlightStrength;
            half _HairSpecStrength;
            half _CleanFaceSdf;
            half _ReverseCleanSdf;
            half _LipGlossStrength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 screenPosition : TEXCOORD4;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(position.positionCS.z);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                output.screenPosition = ComputeScreenPos(position.positionCS);
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                Light light = GetMainLight(input.screenPosition);
                #else
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #endif
                half3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                    TRANSFORM_TEX(input.uv, _BaseMap)).rgb * _BaseColor.rgb;
                half3 diffuse;
                half3 highlight = 0;

                #if defined(_PERLICA_FACE)
                float3 front = TransformObjectToWorldDir(_FaceForward.xyz);
                float3 right = TransformObjectToWorldDir(_FaceRight.xyz);
                float2 lightOnFace = float2(dot(light.direction, right), dot(light.direction, front));
                lightOnFace *= rsqrt(max(dot(lightOnFace, lightOnFace), 0.0001));
                half3 sdfLeft = SAMPLE_TEXTURE2D(_FaceSdf, sampler_FaceSdf, input.uv).rgb;
                half3 sdfRight = SAMPLE_TEXTURE2D(_FaceSdf, sampler_FaceSdf, float2(1 - input.uv.x, input.uv.y)).rgb;
                bool clean = _CleanFaceSdf > 0.5h;
                half fieldRight = clean ? sdfRight.r : saturate(sdfRight.b + sdfRight.g * sdfRight.r);
                half fieldLeft = clean ? sdfLeft.r : saturate(sdfLeft.b + sdfLeft.g * sdfLeft.r);
                float side = clean && _ReverseCleanSdf > 0.5h ? -lightOnFace.x : lightOnFace.x;
                half field = lerp(fieldLeft, fieldRight, smoothstep(-0.055, 0.055, side));
                float turn = acos(clamp(lightOnFace.y, -1, 1)) / PI;
                half threshold = (clean ? lerp(0.035, 0.98, sqrt(turn)) : lerp(-0.045, 1.045, turn))
                    + _FaceShadowBias * sin(PI * turn);
                half edge = max(_FaceShadowSoftness, fwidth(field) * 1.25h);
                half faceLight = smoothstep(threshold - edge, threshold + edge, field);
                if (_FaceView >= 0.5h)
                {
                    half preview = _FaceView >= 1.5h ? field : faceLight;
                    return half4(preview, preview, preview, 1);
                }
                half cheek = 0.94h + 0.06h * saturate(dot(normal, front));
                half sdfShadow = (1 - faceLight) * _FaceShadowStrength * (1 - _FaceSdfDisabled);
                diffuse = lerp(half3(1.03h, 1.0h, 0.99h), _FaceShadowTint.rgb, sdfShadow) * cheek;
                if (_LipGlossStrength > 0)
                {
                    half4 lips = SAMPLE_TEXTURE2D(_LipGlossMap, sampler_LipGlossMap, input.uv);
                    half coverage = lips.a * _LipGlossStrength;
                    baseColor = lerp(baseColor, lips.rgb, coverage * 0.55h);
                    half gloss = pow(saturate(dot(normal, SafeNormalize(light.direction + view))), 16);
                    highlight = coverage * gloss * half3(0.11h, 0.08h, 0.07h);
                }
                #else
                half diffuseBand = smoothstep(-0.16h, 0.25h, dot(normal, light.direction));
                diffuse = lerp(half3(0.48h, 0.49h, 0.60h), half3(1.03h, 0.98h, 0.94h), diffuseBand);
                float2 sphereUV = normalize(TransformWorldToViewDir(normal)).xy * 0.5 + 0.5;
                half3 sphere = SAMPLE_TEXTURE2D(_HairSphere, sampler_HairSphere, sphereUV).rgb;
                half3 mask = SAMPLE_TEXTURE2D(_HairMask, sampler_HairMask, input.uv).rgb;
                half glint = pow(saturate(dot(normal, SafeNormalize(light.direction + view))), 24);
                half sphereMask = _HairSpecStrength > 0 ? mask.g : lerp(0.25h, 1.0h, mask.g);
                highlight = sphere * sphereMask * _HighlightStrength
                    + glint * mask.g * _HairSpecStrength * half3(1.0h, 0.92h, 0.78h);
                #endif

                half3 lighting = lerp(half3(0.70h, 0.74h, 0.83h), max(light.color, half3(0.52h, 0.52h, 0.52h)),
                    0.55h * light.shadowAttenuation);
                half3 color = baseColor * diffuse * lighting + highlight * (0.45h + 0.55h * light.shadowAttenuation);
                #if defined(_PERLICA_FACE)
                color = ApplyBangsProjection(color, input.positionWS);
                #endif
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
