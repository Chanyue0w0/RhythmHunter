Shader "RhythmHunter/Art2/GemBeamVolume"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-1" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Motion;
            CBUFFER_END
            struct A {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float facing=abs(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world))));
                float rim=pow(1-facing,2);
                float flow=.5+.5*sin(i.uv.y*12.5664-_Motion.x*4);
                float ends=smoothstep(0,.06,i.uv.y)*(1-smoothstep(.88,1,i.uv.y));
                half3 color=lerp(half3(.55,.82,1),half3(.92,.98,1),saturate(facing*.7+flow*.12));
                return half4(color,ends*_Motion.y*(.58+.2*facing+.04*flow));
            }
            ENDHLSL
        }
    }
}
