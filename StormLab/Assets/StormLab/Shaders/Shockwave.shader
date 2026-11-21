Shader "StormLab/Shockwave"
{
    Properties
    {
        [HDR] _Tint ("Ring color", Color) = (0.1, 0.7, 1, 1)
        _Age ("Normalized age", Range(0, 1)) = 0
        _Refraction ("Refraction (screen UV)", Range(0, 0.06)) = 0.028
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Age, _Refraction;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 delta = input.uv * 2.0 - 1.0;
                float distance = length(delta);
                float2 radial = delta / max(distance, 0.001);
                float spread = 1.0 - pow(1.0 - _Age, 2.0);
                float ring = distance - lerp(0.10, 0.87, spread);
                float fade = 1.0 - smoothstep(0.10, 1.0, _Age);
                float band = 1.0 - smoothstep(0.005, 0.11, abs(ring));
                float edge = 1.0 - smoothstep(0.002, 0.025, abs(ring));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float ripple = sin(ring * 49 + radial.x * 0.7) * band;
                float2 aspect = float2(_ScaledScreenParams.y / _ScaledScreenParams.x, 1.0);
                float3 background = SampleSceneColor(saturate(screenUV + radial * aspect * ripple * _Refraction * fade));
                return half4(background + _Tint.rgb * edge * fade * 1.15, band * fade);
            }
            ENDHLSL
        }
    }
}

