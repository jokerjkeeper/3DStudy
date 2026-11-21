Shader "Noggi Demo/Face SDF (URP)"
{
    Properties
    {
        [NoScaleOffset] _FaceSdf ("Face SDF (linear)", 2D) = "white" {}
        _LitColor ("Lit color", Color) = (1, 0.72, 0.60, 1)
        _ShadeColor ("Shade color", Color) = (0.72, 0.38, 0.45, 1)
        _Feather ("Shadow edge softness", Range(0, 0.08)) = 0.008
        [Toggle] _MirrorConvention ("Invert UV direction", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "FaceSdf"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_FaceSdf);
            SAMPLER(sampler_FaceSdf);
            CBUFFER_START(UnityPerMaterial)
                half4 _LitColor;
                half4 _ShadeColor;
                half _Feather;
                half _MirrorConvention;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The demo head's local forward is -Z; +X points to screen right.
                float3 lightOS = TransformWorldToObjectDir(GetMainLight().direction);
                float2 horizontal = float2(lightOS.x, -lightOS.z);
                float angle = dot(horizontal, horizontal) > 1e-6
                    ? atan2(horizontal.x, horizontal.y) : 0.0;
                half threshold = abs(angle) / PI;

                float2 uv = input.uv;
                if ((angle < 0.0) != (_MirrorConvention > 0.5))
                    uv.x = 1.0 - uv.x;

                half sdf = SAMPLE_TEXTURE2D(_FaceSdf, sampler_FaceSdf, uv).r;
                half feather = max(_Feather, 0.0001);
                half lit = smoothstep(threshold - feather, threshold + feather, sdf);
                return half4(lerp(_ShadeColor.rgb, _LitColor.rgb, lit), 1.0);
            }
            ENDHLSL
        }
    }
}
