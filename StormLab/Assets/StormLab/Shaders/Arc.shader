Shader "StormLab/Arc"
{
    Properties
    {
        [HDR] _Tint ("Outer color", Color) = (0.1, 0.7, 1, 1)
        _Strength ("Strength", Float) = 1
        _Erosion ("Erosion threshold", Range(0, 1)) = 0
        _Seed ("Noise phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Strength, _Erosion, _Seed;
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

            float Noise(float x)
            {
                float cell = floor(x);
                float t = frac(x);
                return lerp(frac(sin(cell * 127.1) * 43758.5453),
                            frac(sin((cell + 1.0) * 127.1) * 43758.5453),
                            t * t * (3.0 - 2.0 * t));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float fast = Noise(input.uv.x * 37 + t * 23 + _Seed);
                float slow = Noise(input.uv.x * 12 - t * 7 + _Seed * 2.14);
                float flow = (0.36 + fast * 0.64) * (0.38 + slow * 0.62);
                float across = 1.0 - abs(input.uv.y * 2.0 - 1.0);
                float veil = smoothstep(0.0, 0.85, across);
                float core = smoothstep(0.72, 1.0, across);
                float mask = smoothstep(_Erosion - 0.09, _Erosion + 0.09, flow * veil);
                float3 glow = (_Tint.rgb * veil * 1.85 + core * 4.5) * (0.48 + flow * 0.52);
                return half4(glow * mask * _Strength, 0);
            }
            ENDHLSL
        }
    }
}

