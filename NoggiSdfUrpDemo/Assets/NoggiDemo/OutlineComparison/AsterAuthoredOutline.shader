Shader "Noggi Demo/Authored Outline (URP)"
{
    Properties
    {
        _OutlineColor ("Ink", Color) = (0.045, 0.035, 0.09, 1)
        _OutlineWidth ("Width in object units", Range(0, 0.1)) = 0.019
        _WidthVariation ("Vertex width influence", Range(0, 1)) = 1
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
                float _WidthVariation;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 outlineNormalOS : TEXCOORD1;
                float4 color : COLOR;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                // UV1 stores a separate, smoothed direction. Red controls line width.
                float paintedWidth = lerp(0.35, 1.35, input.color.r);
                float width = _OutlineWidth * lerp(1.0, paintedWidth, _WidthVariation);
                return TransformObjectToHClip(input.positionOS + SafeNormalize(input.outlineNormalOS) * width);
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
