Shader "Body Current/Arc"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 5
        _FlowRange ("Flow range", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Intensity;
            float4 _FlowRange;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float center = saturate(1 - abs(input.uv.y * 2 - 1));
                float distanceAlongArm = lerp(_FlowRange.x, _FlowRange.y, input.uv.x);
                float head = frac(_Time.y * 2.6 - distanceAlongArm);
                float pulse = 1 - smoothstep(0.035, 0.40, head);
                float alpha = smoothstep(0, 0.48, center) * lerp(0.79, 1.0, input.color.a);
                float core = smoothstep(0.53, 0.95, center);
                float3 blue = lerp(float3(0.012, 0.27, 0.95), float3(0.08, 0.85, 1.9), pulse);
                float3 white = lerp(float3(0.10, 0.72, 1.5), float3(0.90, 2.0, 3.5), pulse);
                return half4(lerp(blue, white, core) * alpha * (_Intensity / 5), alpha);
            }
            ENDHLSL
        }
    }
}
