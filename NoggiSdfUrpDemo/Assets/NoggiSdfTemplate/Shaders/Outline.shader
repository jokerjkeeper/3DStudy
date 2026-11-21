Shader "Noggi SDF Toolkit/Outline (URP)"
{
    Properties
    {
        [MainColor] _OutlineColor ("Ink color", Color) = (0.045,0.035,0.09,1)
        _OutlineWidth ("Width in world units", Range(0,0.03)) = 0.002
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
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
                float3 position = TransformObjectToWorld(input.positionOS);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                return TransformWorldToHClip(position + normal * _OutlineWidth);
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
