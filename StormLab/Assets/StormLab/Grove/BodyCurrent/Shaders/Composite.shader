Shader "Body Current/Composite"
{
    Properties
    {
        _OutlinePixels ("Outline in pixels", Float) = 2.4
        _FresnelStrength ("Fresnel strength", Float) = 0.38
        _MaskPixel ("Mask pixel size", Vector) = (0.001, 0.001, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _OutlinePixels;
            float _FresnelStrength;
            float4 _MaskPixel;

            float MaskAt(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            }

            float ExpandMask(float2 uv, float2 radius)
            {
                float2 diagonal = radius * 0.7071;
                float coverage = max(MaskAt(uv + float2(radius.x, 0)), MaskAt(uv - float2(radius.x, 0)));
                coverage = max(coverage, max(MaskAt(uv + float2(0, radius.y)), MaskAt(uv - float2(0, radius.y))));
                coverage = max(coverage, max(MaskAt(uv + diagonal), MaskAt(uv - diagonal)));
                diagonal.y = -diagonal.y;
                return max(coverage, max(MaskAt(uv + diagonal), MaskAt(uv - diagonal)));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rg;
                float2 radius = _MaskPixel.xy * _OutlinePixels;
                float core = saturate(ExpandMask(uv, radius) - center.r);
                float halo = saturate(ExpandMask(uv, radius * 2.3) - center.r);
                float outlineOn = step(0.01, _OutlinePixels);
                float3 outline = (core * float3(0.19, 0.86, 2.6) + halo * float3(0.024, 0.12, 0.32)) * outlineOn;
                float3 rim = center.g * _FresnelStrength * float3(0.12, 0.76, 1.9);
                return half4(outline + rim, 0);
            }
            ENDHLSL
        }
    }
}
