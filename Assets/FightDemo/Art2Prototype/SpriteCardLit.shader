Shader "RhythmHunter/Art2/SpriteCardLit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _AmbientTint ("Ambient Tint", Color) = (0.82,0.9,1,1)
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.8
        _LightInfluence ("Light Influence", Range(0,2)) = 0.65
        _EmissionStrength ("Emission Strength", Range(0,4)) = 0
        _BlurRadius ("Distant Blur (Source Pixels)", Range(0,2)) = 0
        _AtmosphereTint ("Distant Atmosphere", Color) = (0.09,0.16,0.2,1)
        _AtmosphereMix ("Atmosphere Mix", Range(0,0.4)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientTint, _AtmosphereTint;
                half _AmbientStrength, _LightInfluence, _EmissionStrength, _BlurRadius, _AtmosphereMix;
            CBUFFER_END
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
                if (_BlurRadius > 0.001h)
                {
                    float2 stepUV = _MainTex_TexelSize.xy * _BlurRadius;
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
                half3 rgb = color.rgb * (Art2Light(normalize(input.normalWS), input.positionWS, input.vertexLight) + _EmissionStrength);
                rgb = lerp(rgb, _AtmosphereTint.rgb, _AtmosphereMix);
                return half4(rgb, color.a);
            }
            ENDHLSL
        }
    }
}
