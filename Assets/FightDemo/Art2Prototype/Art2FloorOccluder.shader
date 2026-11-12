Shader "Hidden/RhythmHunter/Art2FloorOccluder"
{
    Properties { _MainTex ("Sprite", 2D) = "white" {} _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Tint;
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V Vert(A input)
            {
                V o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv; o.color = _Tint;
                return o;
            }
            half4 Frag(V input):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a * input.color.a - .1);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vert(float3 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS); }
            half4 Frag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
