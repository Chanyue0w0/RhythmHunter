Shader "RhythmHunter/Art2/SpriteCardLit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ShadowCutoff ("Shadow Alpha Cutoff", Range(0.001,1)) = 0.1
        [Toggle] _UseNormalMap ("Use Surface Normal Map", Float) = 0
        [Normal] _NormalMap ("Surface Normal Map (same canvas as sprite)", 2D) = "bump" {}
        _NormalStrength ("Surface Normal Strength", Range(0,2)) = 1
        _DiffuseFloor ("Direct Light Wrap (lower = stronger face contrast)", Range(0,1)) = 0.3
        [HideInInspector] _SurfaceDebugMode ("Preview Only: 0 Final, 1 Artwork, 2 Gray, 3 World Normals", Float) = 0
        _AmbientTint ("Ambient Tint", Color) = (0.82,0.9,1,1)
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.8
        _LightInfluence ("Light Influence", Range(0,2)) = 0.65
        _EmissionStrength ("Emission Strength", Range(0,4)) = 0
        _BlurRadius ("Distant Blur (Source Pixels)", Range(0,2)) = 0
        _FocusSaturation ("Focus Saturation", Range(0,1)) = 1
        _FocusContrast ("Focus Contrast", Range(0,1)) = 1
        _AtmosphereTint ("Distant Atmosphere", Color) = (0.09,0.16,0.2,1)
        _AtmosphereMix ("Atmosphere Mix", Range(0,0.4)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Art2FocusWall.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientTint, _AtmosphereTint;
                half _AmbientStrength, _LightInfluence, _EmissionStrength, _BlurRadius, _AtmosphereMix, _ShadowCutoff;
                half _UseNormalMap, _NormalStrength, _DiffuseFloor, _SurfaceDebugMode;
                half _FocusSaturation, _FocusContrast;
            CBUFFER_END
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Art2Lighting.hlsl"
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 vertexLight : TEXCOORD3;
                half3 tangentWS : TEXCOORD4;
                half3 bitangentWS : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(float3(0,0,-1));
                output.tangentWS = TransformObjectToWorldDir(float3(unity_SpriteProps.x,0,0));
                output.bitangentWS = TransformObjectToWorldDir(float3(0,unity_SpriteProps.y,0));
                output.vertexLight = VertexLighting(output.positionWS, output.normalWS);
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                return output;
            }
            half4 PremultipliedSample(float2 uv)
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return half4(texel.rgb * texel.a, texel.a);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Material-uniform branch: sharp actors/foreground pay for one texture sample.
                half focusWeight = FocusWallWeight(input.positionWS);
                half blurRadius = max(_BlurRadius, FocusWallBlurAmount(input.positionWS));
                if (blurRadius > 0.001h)
                {
                    float2 stepUV = _MainTex_TexelSize.xy * blurRadius;
                    half4 sum = half4(color.rgb * color.a, color.a) * 4;
                    sum += PremultipliedSample(input.uv + float2(stepUV.x,0)) * 2;
                    sum += PremultipliedSample(input.uv - float2(stepUV.x,0)) * 2;
                    sum += PremultipliedSample(input.uv + float2(0,stepUV.y)) * 2;
                    sum += PremultipliedSample(input.uv - float2(0,stepUV.y)) * 2;
                    sum += PremultipliedSample(input.uv + stepUV);
                    sum += PremultipliedSample(input.uv - stepUV);
                    sum += PremultipliedSample(input.uv + float2(stepUV.x,-stepUV.y));
                    sum += PremultipliedSample(input.uv + float2(-stepUV.x,stepUV.y));
                    color = half4(sum.rgb / max(sum.a, 0.0001h), sum.a / 16);
                }
                color *= input.color;
                half luminance = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                color.rgb = lerp(luminance.xxx, color.rgb, _FocusSaturation);
                color.rgb = lerp(half3(0.18h, 0.18h, 0.18h), color.rgb, _FocusContrast);
                if (_SurfaceDebugMode > 0.5h && _SurfaceDebugMode < 1.5h)
                    return color;
                half3 normalWS = normalize(input.normalWS);
                if (_UseNormalMap > 0.5h && _NormalStrength > 0.001h)
                {
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalStrength);
                    normalWS = normalize(normalTS.x * normalize(input.tangentWS)
                        + normalTS.y * normalize(input.bitangentWS) + normalTS.z * normalWS);
                }
                if (_SurfaceDebugMode > 2.5h)
                    return half4(normalWS * 0.5h + 0.5h, color.a);
                if (_SurfaceDebugMode > 1.5h)
                    color.rgb = half3(0.5h, 0.5h, 0.5h);
                half3 rgb = color.rgb * (Art2Light(normalWS, input.positionWS, input.vertexLight, GetNormalizedScreenSpaceUV(input.positionCS), _DiffuseFloor) + _EmissionStrength);
                rgb = lerp(rgb, _AtmosphereTint.rgb, _AtmosphereMix);
                rgb = FocusWallFog(rgb, focusWeight);
                return half4(rgb, color.a);
            }
            ENDHLSL
        }

        // Only the opaque sprite silhouette casts; transparent canvas pixels are discarded.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowAttributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
            };
            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(float3(0,0,-1));
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                // A card has two faces: bias toward the light on either side.
                normalWS *= dot(normalWS, lightDirectionWS) < 0 ? -1 : 1;
                ShadowVaryings output;
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = input.uv;
                output.alpha = input.color.a * unity_SpriteColor.a;
                return output;
            }
            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.alpha;
                clip(alpha - max(_ShadowCutoff, 0.001h));
                return 0;
            }
            ENDHLSL
        }
    }
}
