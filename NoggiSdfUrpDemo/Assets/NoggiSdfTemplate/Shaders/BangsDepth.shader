Shader "Hidden/Noggi SDF Toolkit/Bangs Depth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4x4 _BangsVP;
            float4 _BangsOrigin, _BangsDirection;
            float4 _FringeCenter, _FringeUp, _FringeFront, _FringeRight, _FringeBounds;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = mul(_BangsVP, float4(output.positionWS, 1));
                return output;
            }

            float Frag(Varyings input) : SV_Target
            {
                float3 offset = input.positionWS - _FringeCenter.xyz;
                clip(_FringeBounds.x - abs(dot(offset, _FringeRight.xyz)));
                clip(dot(offset, _FringeUp.xyz) - _FringeBounds.y);
                clip(dot(offset, _FringeFront.xyz) - _FringeBounds.z);
                return saturate(dot(input.positionWS - _BangsOrigin.xyz, _BangsDirection.xyz) / _BangsDirection.w);
            }
            ENDHLSL
        }
    }
}
