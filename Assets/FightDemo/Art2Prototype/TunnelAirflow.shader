Shader "RhythmHunter/Art2/TunnelAirflow"
{
    Properties { [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test",Float)=4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off ZTest [_ZTest] Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Protection, _Door;
            float _PixelSnap;
            struct Attributes {float3 positionOS:POSITION;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float4 screen:TEXCOORD0;float4 color:COLOR;};
            Varyings Vert(Attributes input)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS);
                if(_PixelSnap>0) o.positionCS.xy=round(o.positionCS.xy/o.positionCS.w*_ScaledScreenParams.xy*.5)*2/_ScaledScreenParams.xy*o.positionCS.w;
                o.screen=ComputeScreenPos(o.positionCS);o.color=input.color;return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.screen.xy/input.screen.w;
                float t=saturate((uv.y-.1)/max(.1,_Door.y-.1));
                float center=lerp(_Protection.y,_Door.x,t);
                float width=lerp(_Protection.z,_Door.z,t);
                float distance=abs(uv.x-center)-width*.5;
                float mask=_Protection.x>0 ? (_Protection.w>0 ? smoothstep(0,_Protection.w,distance) : step(.00001,distance)) : 1;
                return half4(input.color.rgb,input.color.a*mask);
            }
            ENDHLSL
        }
    }
}
