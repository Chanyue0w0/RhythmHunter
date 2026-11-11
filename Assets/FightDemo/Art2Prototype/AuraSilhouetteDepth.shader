Shader "RhythmHunter/VFX/Aura Silhouette Depth"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} _Cutoff ("Alpha Cutoff", Range(0,1)) = .15 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+49" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Cutoff;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.uv=a.uv; o.color=a.color; return o; }
            half4 frag(V i):SV_Target { clip(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*i.color.a-_Cutoff); return 0; }
            ENDHLSL
        }
    }
}
