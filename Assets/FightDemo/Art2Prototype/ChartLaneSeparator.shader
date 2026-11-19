Shader "RhythmHunter/Art2/ChartLaneSeparator"
{
    Properties { _Color ("Color", Color) = (0,0,0,0.45) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-9" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
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
            V Vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS);o.uv=i.uv;return o; }
            half4 Frag(V i):SV_Target
            {
                float edge=smoothstep(0,.15,i.uv.x)*smoothstep(0,.15,1-i.uv.x);
                float fade=smoothstep(0,.02,i.uv.y)*(1-smoothstep(.9,1,i.uv.y));
                return half4(_Color.rgb,_Color.a*edge*fade);
            }
            ENDHLSL
        }
    }
}
