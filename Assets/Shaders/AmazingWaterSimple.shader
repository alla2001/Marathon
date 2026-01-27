Shader "Custom/AmazingWaterSimple"
{
    Properties
    {
        [Header(Water Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.2, 0.7, 0.9, 0.8)
        _DeepColor ("Deep Color", Color) = (0.05, 0.2, 0.6, 0.95)

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0

        [Header(Waves)]
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1
        _WaveAmplitude ("Wave Amplitude", Range(0, 1)) = 0.15
        _WaveFrequency ("Wave Frequency", Range(0, 10)) = 2

        [Header(Normals)]
        _NormalMap ("Normal Map (Optional)", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.8
        _NormalTiling ("Normal Tiling", Range(0.01, 5)) = 0.5
        _NormalSpeed ("Normal Speed", Range(0, 1)) = 0.3

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3
        _FresnelColor ("Fresnel Color", Color) = (0.8, 0.95, 1, 1)

        [Header(Foam)]
        [Toggle] _EnableFoam ("Enable Foam", Float) = 1
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamScale ("Foam Scale", Range(1, 50)) = 15
        _FoamIntensity ("Foam Intensity", Range(0, 2)) = 0.5
        _FoamSpeed ("Foam Speed", Range(0, 2)) = 0.5

        [Header(Sparkle)]
        _SparkleIntensity ("Sparkle Intensity", Range(0, 3)) = 1
        _SparkleScale ("Sparkle Scale", Range(10, 100)) = 40

        [Header(Emission)]
        _EmissionColor ("Emission Color", Color) = (0, 0.1, 0.2, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 1)) = 0.1
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
            Name "WaterForwardSimple"
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float fogFactor : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
                float waveHeight : TEXCOORD6;
            };

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _Smoothness;
                float _Metallic;

                float _WaveSpeed;
                float _WaveAmplitude;
                float _WaveFrequency;

                float4 _NormalMap_ST;
                float _NormalStrength;
                float _NormalTiling;
                float _NormalSpeed;

                float _FresnelPower;
                float4 _FresnelColor;

                float _EnableFoam;
                float4 _FoamColor;
                float _FoamScale;
                float _FoamIntensity;
                float _FoamSpeed;

                float _SparkleIntensity;
                float _SparkleScale;

                float4 _EmissionColor;
                float _EmissionIntensity;
            CBUFFER_END

            // Hash functions for procedural noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float hash3(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // FBM
            float fbm(float2 p, int octaves)
            {
                float value = 0;
                float amplitude = 0.5;
                float frequency = 1;

                for (int i = 0; i < octaves; i++)
                {
                    value += noise(p * frequency) * amplitude;
                    amplitude *= 0.5;
                    frequency *= 2;
                }
                return value;
            }

            // Voronoi for sparkles
            float voronoi(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);

                float minDist = 1.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 point = hash(ip + neighbor) * 0.5 + 0.25;
                        float2 diff = neighbor + point - fp;
                        float dist = dot(diff, diff);
                        minDist = min(minDist, dist);
                    }
                }

                return sqrt(minDist);
            }

            // Procedural normal
            float3 proceduralNormal(float2 uv, float time)
            {
                float2 uv1 = uv * _NormalTiling + time * _NormalSpeed * float2(1, 0.7);
                float2 uv2 = uv * _NormalTiling * 1.3 - time * _NormalSpeed * float2(0.7, 1);

                float h1 = fbm(uv1, 4);
                float h2 = fbm(uv2, 4);

                float eps = 0.01;
                float h1x = fbm(uv1 + float2(eps, 0), 4);
                float h1y = fbm(uv1 + float2(0, eps), 4);
                float h2x = fbm(uv2 + float2(eps, 0), 4);
                float h2y = fbm(uv2 + float2(0, eps), 4);

                float3 n1 = normalize(float3((h1 - h1x) / eps, (h1 - h1y) / eps, 1));
                float3 n2 = normalize(float3((h2 - h2x) / eps, (h2 - h2y) / eps, 1));

                float3 n = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
                n.xy *= _NormalStrength;
                return normalize(n);
            }

            // Wave function
            float3 wave(float3 pos, float time)
            {
                float wave1 = sin(pos.x * _WaveFrequency + time * _WaveSpeed) *
                             cos(pos.z * _WaveFrequency * 0.7 + time * _WaveSpeed * 0.8);

                float wave2 = sin(pos.x * _WaveFrequency * 1.3 - time * _WaveSpeed * 0.9) *
                             cos(pos.z * _WaveFrequency * 1.1 + time * _WaveSpeed * 1.1);

                float wave3 = sin((pos.x + pos.z) * _WaveFrequency * 0.8 + time * _WaveSpeed * 1.2);

                float height = (wave1 * 0.5 + wave2 * 0.3 + wave3 * 0.2) * _WaveAmplitude;

                return float3(0, height, 0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz;
                float time = _Time.y;

                // Apply waves
                float3 waveOffset = wave(posOS, time);
                posOS += waveOffset;

                // Calculate wave-influenced normal
                float eps = 0.1;
                float3 posR = input.positionOS.xyz + float3(eps, 0, 0);
                float3 posF = input.positionOS.xyz + float3(0, 0, eps);
                posR += wave(posR, time);
                posF += wave(posF, time);

                float3 tangent = normalize(posR - posOS);
                float3 bitangent = normalize(posF - posOS);
                float3 waveNormal = normalize(cross(bitangent, tangent));

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(waveNormal, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.tangentWS = float4(normInputs.tangentWS, input.tangentOS.w);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                output.waveHeight = waveOffset.y;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float3 viewDirWS = normalize(input.viewDirWS);

                // Get normal (from texture or procedural)
                float3 normalTS;
                float2 worldUV = input.positionWS.xz;

                #if defined(_NORMALMAP)
                    float2 uv1 = worldUV * _NormalTiling + time * _NormalSpeed * float2(1, 0.7);
                    float2 uv2 = worldUV * _NormalTiling * 1.3 - time * _NormalSpeed * float2(0.7, 1);
                    float3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                    float3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength * 0.5);
                    normalTS = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
                #else
                    normalTS = proceduralNormal(worldUV, time);
                #endif

                // Transform normal to world space
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 TBN = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // Base water color - use wave height for shallow/deep blend
                float heightFactor = saturate((input.waveHeight / _WaveAmplitude + 1) * 0.5);
                float4 waterColor = lerp(_DeepColor, _ShallowColor, heightFactor * 0.5 + 0.25);

                // Fresnel color blend
                float3 finalColor = lerp(waterColor.rgb, _FresnelColor.rgb, fresnel * 0.6);

                // Foam
                if (_EnableFoam > 0.5)
                {
                    float2 foamUV = worldUV * _FoamScale / 10.0;
                    float foamNoise1 = fbm(foamUV + time * _FoamSpeed * float2(1, 0.5), 4);
                    float foamNoise2 = fbm(foamUV * 1.5 - time * _FoamSpeed * float2(0.5, 1), 4);
                    float foam = foamNoise1 * foamNoise2;

                    // Wave crest foam
                    float crestFoam = saturate(input.waveHeight / _WaveAmplitude * 2);
                    foam = foam * crestFoam * _FoamIntensity * 2;
                    foam = saturate(foam);

                    finalColor = lerp(finalColor, _FoamColor.rgb, foam);
                }

                // Sparkles
                if (_SparkleIntensity > 0)
                {
                    float2 sparkleUV = worldUV * _SparkleScale + normalTS.xy;
                    float sparkle = voronoi(sparkleUV + time * 0.3);
                    sparkle = 1.0 - sparkle;
                    sparkle = pow(saturate(sparkle), 15) * _SparkleIntensity;

                    // Only show sparkles where light hits
                    Light mainLight = GetMainLight();
                    float sparkleLight = saturate(dot(normalWS, mainLight.direction));
                    sparkle *= sparkleLight;

                    finalColor += sparkle * mainLight.color;
                }

                // Emission
                finalColor += _EmissionColor.rgb * _EmissionIntensity;

                // Lighting
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Simple PBR-ish lighting
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float NdotH = saturate(dot(normalWS, halfDir));

                float3 diffuse = finalColor * NdotL * mainLight.color * mainLight.shadowAttenuation;
                float3 specular = pow(NdotH, _Smoothness * 128) * mainLight.color * mainLight.shadowAttenuation;

                // Ambient
                float3 ambient = SampleSH(normalWS) * finalColor;

                float3 litColor = ambient + diffuse + specular * (1 - waterColor.a * 0.5);

                // Alpha
                float alpha = saturate(waterColor.a + fresnel * 0.3);

                // Fog
                litColor = MixFog(litColor, input.fogFactor);

                return half4(litColor, alpha);
            }
            ENDHLSL
        }

        // Shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
