Shader "StormLab/Discharge Accents"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+25" }
        Pass
        {
            Blend One One
            ColorMask RGB
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 shape : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                nointerpolation half2 shape : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv * 2 - 1;
                output.shape = input.shape;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv;
                float radius = length(p);
                float mask;
                if (input.shape.x < 0.5)
                {
                    float falloff = saturate(1 - radius);
                    mask = falloff * falloff * (0.28 + 0.72 * falloff);
                }
                else if (input.shape.x < 1.5)
                {
                    float2 axis = saturate(1 - abs(p));
                    mask = axis.x * axis.x * axis.y * axis.y * axis.y * axis.y;
                }
                else
                {
                    float angle = atan2(p.y, p.x + 0.0001);
                    float ripple = 0.013 * sin(angle * 5 - input.shape.y)
                        + 0.008 * sin(angle * 11 + input.shape.y * 2);
                    float distanceToRing = abs(radius - (0.77 + ripple));
                    float width = max(fwidth(radius), 0.014);
                    float rim = 1 - smoothstep(0, width * 2.2, distanceToRing);
                    float breaks = smoothstep(-0.55, 0.2, sin(angle * 7 + input.shape.y)
                        + 0.35 * sin(angle * 13 - input.shape.y * 2));
                    mask = (rim * lerp(0.14, 1, breaks) + 0.16 * saturate(1 - distanceToRing * 11))
                        * (1 - smoothstep(0.91, 1, radius));
                }
                return half4(input.color.rgb * (input.color.a * mask), 0);
            }
            ENDHLSL
        }
    }
}
