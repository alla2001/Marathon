Shader "Custom/AmazingWater"
{
    Properties
    {
        [Header(Water Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.325, 0.807, 0.971, 0.725)
        _DeepColor ("Deep Color", Color) = (0.086, 0.407, 1, 0.749)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _DepthMaxDistance ("Depth Max Distance", Range(0, 50)) = 10

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0

        [Header(Waves)]
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 1
        _WaveAmplitude ("Wave Amplitude", Range(0, 2)) = 0.2
        _WaveFrequency ("Wave Frequency", Range(0, 10)) = 1
        _WaveDirection ("Wave Direction", Vector) = (1, 0, 0.5, 0)

        [Header(Normal Maps)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalMap2 ("Normal Map 2", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _NormalTiling ("Normal Tiling", Range(0.01, 10)) = 1
        _NormalSpeed ("Normal Animation Speed", Range(0, 2)) = 0.5

        [Header(Foam)]
        _FoamDistance ("Foam Distance", Range(0, 5)) = 1
        _FoamIntensity ("Foam Intensity", Range(0, 3)) = 1
        _FoamNoise ("Foam Noise", 2D) = "white" {}
        _FoamNoiseScale ("Foam Noise Scale", Range(0.1, 10)) = 2
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.5

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 4
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.5

        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.1

        [Header(Caustics)]
        _CausticsTexture ("Caustics Texture", 2D) = "black" {}
        _CausticsScale ("Caustics Scale", Range(0.1, 10)) = 1
        _CausticsSpeed ("Caustics Speed", Range(0, 2)) = 0.5
        _CausticsIntensity ("Caustics Intensity", Range(0, 2)) = 0.5

        [Header(Subsurface Scattering)]
        _SubsurfaceColor ("Subsurface Color", Color) = (0.2, 0.8, 0.5, 1)
        _SubsurfaceIntensity ("Subsurface Intensity", Range(0, 2)) = 0.5
        _SubsurfaceDistortion ("Subsurface Distortion", Range(0, 1)) = 0.5

        [Header(Sparkle)]
        _SparkleIntensity ("Sparkle Intensity", Range(0, 5)) = 1
        _SparkleScale ("Sparkle Scale", Range(1, 100)) = 30
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                float3 viewDirWS : TEXCOORD6;
            };

            TEXTURE2D(_NormalMap);
            TEXTURE2D(_NormalMap2);
            TEXTURE2D(_FoamNoise);
            TEXTURE2D(_CausticsTexture);
            SAMPLER(sampler_NormalMap);
            SAMPLER(sampler_NormalMap2);
            SAMPLER(sampler_FoamNoise);
            SAMPLER(sampler_CausticsTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _DepthMaxDistance;
                float _Smoothness;
                float _Metallic;

                float _WaveSpeed;
                float _WaveAmplitude;
                float _WaveFrequency;
                float4 _WaveDirection;

                float4 _NormalMap_ST;
                float4 _NormalMap2_ST;
                float _NormalStrength;
                float _NormalTiling;
                float _NormalSpeed;

                float _FoamDistance;
                float _FoamIntensity;
                float _FoamNoiseScale;
                float _FoamCutoff;

                float _FresnelPower;
                float _FresnelIntensity;

                float _RefractionStrength;

                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsIntensity;

                float4 _SubsurfaceColor;
                float _SubsurfaceIntensity;
                float _SubsurfaceDistortion;

                float _SparkleIntensity;
                float _SparkleScale;
            CBUFFER_END

            // Gerstner wave function for realistic ocean waves
            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z;
                float wavelength = wave.w;
                float k = 2 * PI / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * _Time.y * _WaveSpeed);
                float a = steepness / k;

                tangent += float3(
                    -d.x * d.x * (steepness * sin(f)),
                    d.x * (steepness * cos(f)),
                    -d.x * d.y * (steepness * sin(f))
                );
                binormal += float3(
                    -d.x * d.y * (steepness * sin(f)),
                    d.y * (steepness * cos(f)),
                    -d.y * d.y * (steepness * sin(f))
                );

                return float3(
                    d.x * (a * cos(f)),
                    a * sin(f),
                    d.y * (a * cos(f))
                );
            }

            // Simple noise function for foam
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Voronoi for sparkles
            float2 voronoi(float2 x)
            {
                float2 p = floor(x);
                float2 f = frac(x);

                float res = 8.0;
                float2 mr;

                for(int j = -1; j <= 1; j++)
                {
                    for(int i = -1; i <= 1; i++)
                    {
                        float2 b = float2(i, j);
                        float2 r = b - f + hash(p + b);
                        float d = dot(r, r);

                        if(d < res)
                        {
                            res = d;
                            mr = r;
                        }
                    }
                }

                return float2(sqrt(res), 0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz;
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);

                // Apply Gerstner waves
                float4 wave1 = float4(_WaveDirection.x, _WaveDirection.z, _WaveAmplitude * 0.5, _WaveFrequency * 10);
                float4 wave2 = float4(_WaveDirection.x * 0.7, _WaveDirection.z * 1.3, _WaveAmplitude * 0.3, _WaveFrequency * 7);
                float4 wave3 = float4(_WaveDirection.x * 1.2, _WaveDirection.z * 0.8, _WaveAmplitude * 0.2, _WaveFrequency * 5);

                float3 gridPoint = posOS;
                float3 p = gridPoint;
                p += GerstnerWave(wave1, gridPoint, tangent, binormal);
                p += GerstnerWave(wave2, gridPoint, tangent, binormal);
                p += GerstnerWave(wave3, gridPoint, tangent, binormal);

                posOS = p;

                // Calculate new normal from waves
                float3 waveNormal = normalize(cross(binormal, tangent));

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(waveNormal, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.tangentWS = float4(normInputs.tangentWS, input.tangentOS.w);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Screen UV for depth and refraction
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // Sample scene depth
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceDepth = input.screenPos.w;
                float depthDifference = sceneDepth - surfaceDepth;

                // Animated UV for normals
                float2 uv1 = input.positionWS.xz * _NormalTiling + _Time.y * _NormalSpeed * float2(0.5, 0.3);
                float2 uv2 = input.positionWS.xz * _NormalTiling * 1.3 - _Time.y * _NormalSpeed * float2(0.3, 0.5);

                // Sample and blend normal maps
                float3 normal1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                float3 normal2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap2, sampler_NormalMap2, uv2), _NormalStrength * 0.5);
                float3 normalTS = normalize(float3(normal1.xy + normal2.xy, normal1.z * normal2.z));

                // Transform normal to world space
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 TBN = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // View direction
                float3 viewDirWS = normalize(input.viewDirWS);

                // Fresnel effect
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                fresnel = lerp(0, 1, fresnel * _FresnelIntensity);

                // Depth-based water color
                float depthFactor = saturate(depthDifference / _DepthMaxDistance);
                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                // Refraction
                float2 refractionOffset = normalTS.xy * _RefractionStrength;
                float2 refractionUV = screenUV + refractionOffset;

                // Make sure refraction doesn't sample above water
                float refractionDepth = SampleSceneDepth(refractionUV);
                float refractionLinearDepth = LinearEyeDepth(refractionDepth, _ZBufferParams);
                refractionUV = refractionLinearDepth < surfaceDepth ? screenUV : refractionUV;

                float3 sceneColor = SampleSceneColor(refractionUV);

                // Caustics
                float2 causticsUV1 = input.positionWS.xz * _CausticsScale + _Time.y * _CausticsSpeed * float2(1, 0.5);
                float2 causticsUV2 = input.positionWS.xz * _CausticsScale * 1.2 - _Time.y * _CausticsSpeed * float2(0.5, 1);
                float caustics1 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, causticsUV1).r;
                float caustics2 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, causticsUV2).r;
                float caustics = min(caustics1, caustics2) * _CausticsIntensity;
                caustics *= saturate(1.0 - depthFactor * 2); // Fade caustics with depth

                // Foam
                float foamDepth = saturate(depthDifference / _FoamDistance);
                float2 foamUV = input.positionWS.xz * _FoamNoiseScale + _Time.y * 0.1;
                float foamNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV).r;
                foamNoise += noise(input.positionWS.xz * _FoamNoiseScale * 2 + _Time.y * 0.2) * 0.5;
                float foam = saturate((1.0 - foamDepth) * foamNoise * _FoamIntensity);
                foam = step(_FoamCutoff, foam) * foam;

                // Wave crest foam
                float waveCrestFoam = saturate(input.positionWS.y * 2 - 0.3) * 0.5;
                foam = max(foam, waveCrestFoam * foamNoise);

                // Sparkles
                float2 sparkleUV = input.positionWS.xz * _SparkleScale + normalTS.xy * 2;
                float sparkle = 1.0 - voronoi(sparkleUV + _Time.y * 0.5).x;
                sparkle = pow(sparkle, 10) * _SparkleIntensity;
                sparkle *= saturate(dot(normalWS, normalize(float3(1, 1, 0.5)))); // Sun direction

                // Subsurface scattering approximation
                Light mainLight = GetMainLight();
                float3 H = normalize(mainLight.direction + normalWS * _SubsurfaceDistortion);
                float VdotH = pow(saturate(dot(viewDirWS, -H)), 3);
                float3 subsurface = _SubsurfaceColor.rgb * VdotH * _SubsurfaceIntensity;
                subsurface *= saturate(1.0 - depthFactor);

                // Combine colors
                float3 finalColor = lerp(sceneColor, waterColor.rgb, waterColor.a * (1.0 - fresnel * 0.5));
                finalColor += caustics * _ShallowColor.rgb;
                finalColor += subsurface;
                finalColor = lerp(finalColor, _FoamColor.rgb, foam);
                finalColor += sparkle;

                // Lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = screenUV;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness * (1.0 - foam * 0.5);
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = saturate(waterColor.a + foam + fresnel * 0.3);

                float4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
