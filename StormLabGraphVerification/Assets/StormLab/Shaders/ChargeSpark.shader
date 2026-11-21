Shader "StormLab/Charge Sparks"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+15" }
        Pass
        {
            Blend SrcAlpha One
            ColorMask RGB
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2 - 1);
                float mask = 1 - smoothstep(0.02, 0.96, radius);
                float core = 1 - smoothstep(0.04, 0.45, radius);
                return half4(input.color.rgb * (1.25 + core * 4.5), input.color.a * mask);
            }
            ENDHLSL
        }
    }
}

