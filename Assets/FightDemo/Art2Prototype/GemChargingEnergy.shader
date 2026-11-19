Shader "RhythmHunter/Art2/GemChargingEnergy"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Charge;
            CBUFFER_END
            struct A {float3 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 local:TEXCOORD2;};
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.local=i.positionOS;return o;}
            half4 Frag(V i):SV_Target
            {
                float rim=pow(1-saturate(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world)))),2);
                float flow=.5+.5*sin(i.local.y*22-_Charge.x*3.14159);
                float alpha=_Charge.y*(.18+.75*rim+.08*flow)*(1+.35*_Charge.z);
                return half4(.12,.56,1,alpha);
            }
            ENDHLSL
        }
    }
}
