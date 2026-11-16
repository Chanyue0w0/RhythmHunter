Shader "RhythmHunter/Art2/BossDoorFan"
{
    Properties { [HDR] _Color ("Light Color", Color) = (1,0.8,0.4,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
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
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS); o.uv=i.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                float edges=smoothstep(0,.1,i.uv.x)*smoothstep(0,.1,1-i.uv.x);
                float endFade=1-smoothstep(.6,1,i.uv.y);
                return half4(_Color.rgb*edges*endFade,0);
            }
            ENDHLSL
        }
    }
}
