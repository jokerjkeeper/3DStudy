Shader "Noggi Demo/Perlica Authored Outline (URP)"
{
    Properties
    {
        [MainColor] _OutlineColor ("Ink color", Color) = (0.045, 0.035, 0.09, 1)
        _OutlineWidth ("Width in world units", Range(0, 0.03)) = 0.002
        _WidthVariation ("Vertex width influence", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+1" }

        Pass
        {
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
                // This mesh is only used for the hull, so skinning can use its NORMAL stream.
                float3 outlineNormalOS : NORMAL;
                float4 color : COLOR;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 position = TransformObjectToWorld(input.positionOS);
                float3 normal = TransformObjectToWorldNormal(input.outlineNormalOS);
                float paintedWidth = lerp(0.45, 1.45, input.color.r);
                float width = _OutlineWidth * lerp(1.0, paintedWidth, _WidthVariation);
                return TransformWorldToHClip(position + normal * width);
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
