Shader "RhythmHunter/Art2/CaveFloorLit"
{
    Properties
    {
        _BaseMap ("Original cave_08", 2D) = "white" {}
        _FloorTopV ("Floor top in source texture (UV)", Range(0,1)) = 0.6388889
        _AmbientTint ("Ambient Tint", Color) = (0.82,0.9,1,1)
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.85
        _LightInfluence ("Light Influence", Range(0,2)) = 0.65
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _FloorTopV;
                half4 _AmbientTint;
                half _AmbientStrength, _LightInfluence;
            CBUFFER_END
            #include "Art2Lighting.hlsl"
            #include "Art2FocusWall.hlsl"
            float4 _BaseMap_TexelSize;
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 vertexLight : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.vertexLight = VertexLighting(output.positionWS, half3(0,1,0));
                // Unity's built-in Plane is ten units wide/deep. Use local coordinates
                // explicitly so the original artwork cannot be mirrored by its mesh UVs.
                output.uv = float2(input.positionOS.x * 0.1 + 0.5,
                    (input.positionOS.z * 0.1 + 0.5) * _FloorTopV);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half weight = FocusWallWeight(input.positionWS);
                float2 stepUV = _BaseMap_TexelSize.xy * weight * _FocusWallSettings.z;
                if (weight * _FocusWallSettings.z > .001)
                {
                    half3 sum = baseColor * 4;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + float2(stepUV.x,0)).rgb * 2;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv - float2(stepUV.x,0)).rgb * 2;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + float2(0,stepUV.y)).rgb * 2;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv - float2(0,stepUV.y)).rgb * 2;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + stepUV).rgb;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv - stepUV).rgb;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + float2(stepUV.x,-stepUV.y)).rgb;
                    sum += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + float2(-stepUV.x,stepUV.y)).rgb;
                    baseColor = sum / 16;
                }
                return half4(FocusWallFog(baseColor * Art2Light(half3(0,1,0), input.positionWS, input.vertexLight, GetNormalizedScreenSpaceUV(input.positionCS)), weight), 1);
            }
            ENDHLSL
        }
    }
}
