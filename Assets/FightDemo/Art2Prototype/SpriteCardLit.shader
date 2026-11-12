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
        [PerRendererData] _CharacterFillColor ("Character Fill (RGB color, A intensity)", Color) = (0,0,0,0)
        [PerRendererData] _CharacterFillDirection ("Character Fill Direction and Softness", Vector) = (0,1,-1,0.6)
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
                half4 _CharacterFillColor, _CharacterFillDirection;
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
            #include "Art2SpriteCardForward.hlsl"
            ENDHLSL
        }

        // Only the scene opt-in feature draws this pass; regular geometry and shadows are unchanged.
        Pass
        {
            Name "FloorRecovery"
            Tags { "LightMode"="Art2FloorRecovery" }
            Cull Off
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragFloorRecovery
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Art2SpriteCardForward.hlsl"
            TEXTURE2D_X_FLOAT(_Art2FloorOnlyDepth);
            TEXTURE2D_X_FLOAT(_Art2NonFloorDepth);
            half4 FragFloorRecovery(Varyings input) : SV_Target
            {
                float floorDepth = LOAD_TEXTURE2D_X(_Art2FloorOnlyDepth, uint2(input.positionCS.xy)).r;
                float otherDepth = LOAD_TEXTURE2D_X(_Art2NonFloorDepth, uint2(input.positionCS.xy)).r;
                #if UNITY_REVERSED_Z
                    clip(floorDepth - 0.000001);
                    clip(floorDepth - input.positionCS.z - 0.000001);
                    clip(input.positionCS.z - otherDepth + 0.000001);
                #else
                    clip(0.999999 - floorDepth);
                    clip(input.positionCS.z - floorDepth - 0.000001);
                    clip(otherDepth - input.positionCS.z + 0.000001);
                #endif
                return Frag(input);
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
