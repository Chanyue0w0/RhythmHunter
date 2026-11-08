Shader "RhythmHunter/Art2/TorchHalo"
{
    Properties { _HaloColor ("Halo Color", Color) = (1,0.08,0.02,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Art2FocusWall.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HaloColor;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2 - 1);
                // Broad round glow with a smooth zero at the edge; no texture/canvas seam.
                float fade = 1 - smoothstep(0.05, 1.0, radius);
                half weight = FocusWallWeight(input.positionWS);
                return half4(_HaloColor.rgb * fade * fade * _HaloColor.a * (1 - weight * _FocusWallSettings.w), 0);
            }
            ENDHLSL
        }
    }
}
