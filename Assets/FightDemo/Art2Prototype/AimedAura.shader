Shader "RhythmHunter/VFX/Aimed Aura"
{
    Properties
    {
        [HDR] _Color ("Aura Color", Color) = (1,.65,.12,.3)
        _Bands ("Spiral Bands", Range(1,8)) = 3
        _Turns ("Spiral Turns", Range(0,4)) = 1
        _Brightness ("Brightness", Range(0,4)) = 1.2
        _Fill ("Soft Fill", Range(0,1)) = .15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Bands, _Turns, _Brightness, _Fill;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.uv=a.uv; return o; }
            half4 frag(V i):SV_Target
            {
                float stripe=pow(saturate(.5+.5*cos(6.2831853*(_Bands*i.uv.x-_Turns*i.uv.y))),14);
                float edge=smoothstep(0,.1,i.uv.y)*(1-smoothstep(.75,1,i.uv.y));
                return half4(_Color.rgb*_Brightness,_Color.a*edge*lerp(_Fill,1,stripe));
            }
            ENDHLSL
        }
    }
}
