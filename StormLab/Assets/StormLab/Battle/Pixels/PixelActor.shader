Shader "StormLab/Pixel Actor"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 positionWS : TEXCOORD1; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.uv;
            output.color = input.color;
            return output;
        }

        half4 SamplePixel(Varyings input)
        {
            half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            clip(pixel.a - 0.5h);
            return pixel;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                half4 pixel = SamplePixel(input);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half illumination = lerp(0.83h, 1.08h, sun.shadowAttenuation);
                half3 tint = lerp(half3(0.91h, 0.95h, 1.06h), half3(1.12h, 1.02h, 0.90h), sun.shadowAttenuation);
                float clipZ = TransformWorldToHClip(input.positionWS).z;
                return half4(MixFog(pixel.rgb * tint * illumination, ComputeFogFactor(clipZ)), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half FragDepth(Varyings input) : SV_Target
            {
                SamplePixel(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragNormal
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            half4 FragNormal(Varyings input) : SV_Target
            {
                SamplePixel(input);
                float3 normal = TransformObjectToWorldNormal(float3(0, 0, -1));
                #ifdef _GBUFFER_NORMALS_OCT
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
}

