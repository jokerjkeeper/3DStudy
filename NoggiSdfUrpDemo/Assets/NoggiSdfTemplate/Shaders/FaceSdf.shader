Shader "Noggi SDF Toolkit/Face (URP)"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base color (UV0)", 2D) = "white" {}
        [MainColor] _LitColor ("Lit tint", Color) = (1,1,1,1)
        _ShadeColor ("Shade tint", Color) = (0.62,0.46,0.54,1)
        [NoScaleOffset] _LipGlossMap ("Optional lip gloss (base UV, alpha mask)", 2D) = "black" {}
        _LipGlossStrength ("Lip gloss strength", Range(0,1)) = 0
        [HideInInspector] _FaceSdfDisabled ("Face SDF bypass", Float) = 0
        _FaceSdf ("SDF mask", 2D) = "white" {}
        [Enum(Red,0,Green,1,Blue,2,Alpha,3,Packed B to R by G,4)] _MaskSource ("Mask channel", Float) = 0
        [Enum(UV0,0,UV1,1)] _MaskUV ("SDF UV channel", Float) = 0
        [ToggleUI] _MirrorConvention ("Reverse left/right convention", Float) = 0
        [ToggleUI] _LinearizeMaskRgb ("Decode sRGB-encoded mask RGB", Float) = 0
        [ToggleUI] _InvertMask ("Invert mask values", Float) = 0
        [Enum(Linear azimuth,0,Cosine azimuth,1,Quick azimuth,2)] _ThresholdMode ("Threshold response", Float) = 0
        _ThresholdRange ("Front and back thresholds (X / Y)", Vector) = (0,1,0,0)
        _Feather ("Shadow edge softness", Range(0,0.12)) = 0.009
        [Enum(Shaded,0,Mask,1,Unmirrored UV,2)] _DebugView ("Preview", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "Face SDF"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BangsProjection.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LipGlossMap);
            SAMPLER(sampler_LipGlossMap);
            TEXTURE2D(_FaceSdf);
            SAMPLER(sampler_FaceSdf);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _FaceSdf_ST;
                half4 _LitColor;
                half4 _ShadeColor;
                half _LipGlossStrength;
                half _FaceSdfDisabled;
                half _MaskSource;
                half _MaskUV;
                half _MirrorConvention;
                half _InvertMask;
                half _LinearizeMaskRgb;
                half _ThresholdMode;
                float4 _ThresholdRange;
                half _Feather;
                half _DebugView;
                float4 _HeadRightWS;
                float4 _HeadForwardWS;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 baseUV : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.baseUV = TRANSFORM_TEX(input.uv0, _BaseMap);
                output.maskUV = (lerp(input.uv0, input.uv1, _MaskUV) * _FaceSdf_ST.xy) + _FaceSdf_ST.zw;
                return output;
            }

            half DecodeMask(half4 mask)
            {
                if (_MaskSource < 0.5) return mask.r;
                if (_MaskSource < 1.5) return mask.g;
                if (_MaskSource < 2.5) return mask.b;
                if (_MaskSource < 3.5) return mask.a;
                return lerp(mask.b, mask.r, mask.g);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 lightWS = GetMainLight().direction;
                float3 rightWS = _HeadRightWS.w > 0.5 ? _HeadRightWS.xyz : TransformObjectToWorldDir(float3(1,0,0));
                float3 forwardWS = _HeadForwardWS.w > 0.5 ? _HeadForwardWS.xyz : TransformObjectToWorldDir(float3(0,0,-1));
                float2 lightPlane = float2(dot(lightWS, rightWS), dot(lightWS, forwardWS));
                float lengthSquared = dot(lightPlane, lightPlane);
                float angle = lengthSquared > 1e-6 ? atan2(lightPlane.x, lightPlane.y) : 0;
                float forward = lengthSquared > 1e-6 ? lightPlane.y * rsqrt(lengthSquared) : 1;
                float fraction = abs(angle) / PI;
                float response = _ThresholdMode > 1.5 ? sqrt(fraction)
                    : (_ThresholdMode > 0.5 ? (1 - forward) * 0.5 : fraction);
                float threshold = lerp(_ThresholdRange.x, _ThresholdRange.y, response);

                if (_DebugView > 1.5)
                    return half4(frac(input.maskUV), 0, 1);

                float2 maskUV = input.maskUV;
                if ((angle < 0) != (_MirrorConvention > 0.5))
                    maskUV.x = 1 - maskUV.x;

                half4 mask = SAMPLE_TEXTURE2D(_FaceSdf, sampler_FaceSdf, maskUV);
                mask.rgb = lerp(mask.rgb, SRGBToLinear(mask.rgb), _LinearizeMaskRgb);
                half value = DecodeMask(mask);
                value = lerp(value, 1 - value, _InvertMask);
                if (_DebugView > 0.5)
                    return half4(value, value, value, 1);

                half softness = max(_Feather, 0.0001);
                half lit = smoothstep(threshold - softness, threshold + softness, value);
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV).rgb;
                half3 diffuse = lerp(_ShadeColor.rgb, _LitColor.rgb, max(lit, _FaceSdfDisabled));
                half3 color = baseColor * diffuse;
                if (_LipGlossStrength > 0.0)
                {
                    half4 lips = SAMPLE_TEXTURE2D(_LipGlossMap, sampler_LipGlossMap, input.baseUV);
                    half coverage = lips.a * _LipGlossStrength;
                    half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half3 halfDirection = SafeNormalize(lightWS + viewWS);
                    half highlight = pow(saturate(dot(normalize(input.normalWS), halfDirection)), 16.0);
                    color = lerp(color, lips.rgb * diffuse, coverage * 0.55) + coverage * highlight * half3(.13,.10,.09);
                }
                return half4(ApplyBangsProjection(color, input.positionWS), 1);
            }
            ENDHLSL
        }
    }
}


