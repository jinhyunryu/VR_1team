Shader "Vefects/SH_Vefects_BIRP_Unlit_Combat_Flipbook_Advanced_01"
{
    Properties
    {
        [Space(33)][Header(Flipbook Frames)][Space(13)]_FlipbookX("Flipbook X", Float) = 4
        _FlipbookY("Flipbook Y", Float) = 4
        [Space(33)][Header(Mask Texture)][Space(13)]_MaskTexture("Mask Texture", 2D) = "white" {}
        _MaskUVScale("Mask UV Scale", Vector) = (1,1,0,0)
        _MaskUVPan("Mask UV Pan", Vector) = (0,0,0,0)
        [HDR]_R("R", Color) = (1,0.9719134,0.5896226,0)
        [HDR]_G("G", Color) = (1,0.7230805,0.25,0)
        [HDR]_B("B", Color) = (0.5943396,0.259371,0.09812209,0)
        [HDR]_Outline("Outline", Color) = (0.2169811,0.03320287,0.02354041,0)
        _FlatColor("Flat Color", Range(0,1)) = 0
        _Emissive("Emissive", Float) = 1
        [Space(33)][Header(Alpha Texture)][Space(13)]_AlphaTexture("Alpha Texture", 2D) = "white" {}
        _AlphaGlow("Alpha Glow", Float) = 0
        [Space(33)][Header(Erosion Noise)][Space(13)]_ErosionNoise("Erosion Noise", 2D) = "white" {}
        _ErosionNoiseUVScaleOverall("Erosion Noise UV Scale Overall", Float) = 1
        _ErosionNoiseUVPan("Erosion Noise UV Pan", Vector) = (0,0,0,0)
        _ErosionIntensity("Erosion Intensity", Float) = 0
        _ErosionSmoothness("Erosion Smoothness", Float) = 1
        _DepthFade("Depth Fade", Float) = 0.1
        [Space(33)][Header(Distortion)][Space(13)]_DistortionTexture("Distortion Texture", 2D) = "white" {}
        _DistortionLerp("Distortion Lerp", Range(0,0.1)) = 0
        _DistortionUVScale("Distortion UV Scale", Vector) = (1,1,0,0)
        _DistortionUVPan("Distortion UV Pan", Vector) = (0.1,-0.2,0,0)
        [Space(33)][Header(Pixelate)][Space(13)][Toggle(_PIXELATE_ON)] _Pixelate("Pixelate", Float) = 0
        _PixelsMultiplier("Pixels Multiplier", Float) = 1
        _PixelsX("Pixels X", Float) = 32
        _PixelsY("Pixels Y", Float) = 32
        [Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
        _Src("Src", Float) = 5
        _Dst("Dst", Float) = 10
        _ZWrite("ZWrite", Float) = 0
        _ZTest("ZTest", Float) = 2
        [HideInInspector] _texcoord2("", 2D) = "white" {}
        [HideInInspector] _texcoord("", 2D) = "white" {}
        [HideInInspector] __dirty("", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "IsEmissive" = "true"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_Src] [_Dst]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _PIXELATE_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_MaskTexture);
            SAMPLER(sampler_MaskTexture);
            TEXTURE2D(_AlphaTexture);
            SAMPLER(sampler_AlphaTexture);
            TEXTURE2D(_ErosionNoise);
            SAMPLER(sampler_ErosionNoise);
            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            CBUFFER_START(UnityPerMaterial)
                float _FlipbookX;
                float _FlipbookY;
                float4 _MaskUVScale;
                float4 _MaskUVPan;
                float4 _R;
                float4 _G;
                float4 _B;
                float4 _Outline;
                float _FlatColor;
                float _Emissive;
                float _AlphaGlow;
                float _ErosionNoiseUVScaleOverall;
                float4 _ErosionNoiseUVPan;
                float _ErosionIntensity;
                float _ErosionSmoothness;
                float _DepthFade;
                float _DistortionLerp;
                float4 _DistortionUVScale;
                float4 _DistortionUVPan;
                float _Pixelate;
                float _PixelsMultiplier;
                float _PixelsX;
                float _PixelsY;
                float _Cull;
                float _Src;
                float _Dst;
                float _ZWrite;
                float _ZTest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 projectedPosition : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.projectedPosition = vertexInput.positionNDC;
                output.color = input.color;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                return output;
            }

            float2 PixelateUV(float2 uv)
            {
                float2 pixelCount = max(float2(_PixelsX, _PixelsY) * max(_PixelsMultiplier, 0.0001), 1.0);
                float2 pixelSize = 1.0 / pixelCount;
                return floor(uv / pixelSize) * pixelSize;
            }

            float DepthFade(float4 projectedPosition)
            {
                float fadeDistance = max(_DepthFade, 0.0001);
                float2 screenUV = projectedPosition.xy / max(projectedPosition.w, 0.000001);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = (unity_OrthoParams.w == 0.0) ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
                float thisZ = LinearEyeDepth(projectedPosition.z / projectedPosition.w, _ZBufferParams);
                return saturate((sceneZ - thisZ) / fadeDistance);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 flipbookFrames = float2(_FlipbookX, _FlipbookY);
                float randomOffset = input.uv1.x;

                float2 maskUV = input.uv0.xy * _MaskUVScale.xy + _Time.y * _MaskUVPan.xy;
                float2 distortionBaseUV = (input.uv0.xy * _DistortionUVScale.xy) * flipbookFrames + _Time.y * _DistortionUVPan.xy;
                float2 distortionSampleUV = distortionBaseUV + randomOffset;
                float2 distortion = (SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture, distortionSampleUV).rg - 0.5) * 2.0;
                float2 distortionOffset = lerp(float2(0.0, 0.0), distortion, _DistortionLerp);

                float2 finalUV = maskUV + distortionOffset;
                #if defined(_PIXELATE_ON)
                    finalUV = PixelateUV(finalUV);
                #endif

                float4 mask = SAMPLE_TEXTURE2D(_MaskTexture, sampler_MaskTexture, finalUV);
                float4 maskColor = lerp(lerp(lerp(_Outline, _B, mask.b), _G, mask.g), _R, mask.r);
                float4 tintedColor = lerp(input.color * maskColor, input.color, _FlatColor);
                float3 emission = tintedColor.rgb * _Emissive;

                float4 alphaTex = SAMPLE_TEXTURE2D(_AlphaTexture, sampler_AlphaTexture, finalUV);
                float alphaFromTexture = saturate(lerp(alphaTex.g, alphaTex.r, saturate(_AlphaGlow)));

                float erosionCutoff = saturate(input.uv0.z);
                float2 erosionUV = (input.uv0.xy * flipbookFrames) * _ErosionNoiseUVScaleOverall + _Time.y * _ErosionNoiseUVPan.xy;
                float erosionNoise = SAMPLE_TEXTURE2D(_ErosionNoise, sampler_ErosionNoise, erosionUV + randomOffset + distortionOffset).g;
                float erosionMask = smoothstep(erosionCutoff, erosionCutoff + _ErosionSmoothness, erosionNoise);
                float erodedAlpha = lerp(alphaFromTexture, saturate(alphaFromTexture * saturate(erosionMask)), _ErosionIntensity);

                float alpha = saturate(erodedAlpha * input.color.a) * DepthFade(input.projectedPosition);
                return half4(emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Particles/Unlit"
}
