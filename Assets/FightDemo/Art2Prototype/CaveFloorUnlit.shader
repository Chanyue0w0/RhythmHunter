Shader "RhythmHunter/Art2/CaveFloorUnlit"
{
    Properties
    {
        _BaseMap ("Original cave_08", 2D) = "white" {}
        _FloorTopV ("Floor top in source texture (UV)", Range(0,1)) = 0.6388889
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
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _FloorTopV;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                // Unity's built-in Plane is ten units wide/deep. Use local coordinates
                // explicitly so the original artwork cannot be mirrored by its mesh UVs.
                output.uv = float2(input.positionOS.x * 0.1 + 0.5,
                    (input.positionOS.z * 0.1 + 0.5) * _FloorTopV);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
