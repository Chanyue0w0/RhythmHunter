Shader "RhythmHunter/Art2/GemEnergyLink"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float3 positionOS:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS);o.color=i.color;o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target {half4 c=i.color;c.a*=1-smoothstep(.2,.5,abs(i.uv.y-.5));return c;}
            ENDHLSL
        }
    }
}
