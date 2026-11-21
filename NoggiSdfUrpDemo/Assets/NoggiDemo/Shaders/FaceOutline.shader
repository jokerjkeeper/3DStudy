Shader "Noggi Demo/Face Outline (URP)"
{
    Properties
    {
        _OutlineColor ("Ink", Color) = (0.045, 0.035, 0.09, 1)
        _OutlineWidth ("Width in object units", Range(0, 0.1)) = 0.019
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "InvertedHull"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                return TransformObjectToHClip(input.positionOS + input.normalOS * _OutlineWidth);
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
