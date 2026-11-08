Shader "RhythmHunter/Art2/TorchParticleFocus"
{
    Properties
    {
        _MainTexture ("Fire Flipbook", 2D) = "white" {}
        _OverallTint ("Tint", Color) = (1,1,1,1)
        _HueShift ("Hue Shift", Float) = 0
        _SaturationMultiply ("Saturation", Float) = 1
        _Emissive ("Emission", Float) = 2
        _FlipbookX ("Flipbook Columns", Float) = 7
        _FlipbookY ("Flipbook Rows", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_Src] [_Dst]
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Art2FocusWall.hlsl"
            TEXTURE2D(_MainTexture); SAMPLER(sampler_MainTexture);
            float4 _MainTexture_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _OverallTint;
                float _HueShift, _SaturationMultiply, _Emissive, _FlipbookX, _FlipbookY;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float3 positionWS:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=v.uv; o.color=v.color; return o;
            }
            half4 SampleFire(float2 uv, float2 lo, float2 hi)
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTexture,sampler_MainTexture,clamp(uv,lo,hi));
                return half4(c.rgb*c.a,c.a);
            }
            half4 Frag(Varyings i):SV_Target
            {
                half weight=FocusWallWeight(i.positionWS);
                float2 grid=max(float2(_FlipbookX,_FlipbookY),1);
                float2 cell=floor(min(i.uv, .999999)*grid);
                float2 lo=cell/grid+_MainTexture_TexelSize.xy*.5;
                float2 hi=(cell+1)/grid-_MainTexture_TexelSize.xy*.5;
                half blur=FocusWallBlurAmount(i.positionWS);
                float2 d=_MainTexture_TexelSize.xy*blur;
                half4 c=SAMPLE_TEXTURE2D(_MainTexture,sampler_MainTexture,i.uv);
                if(blur>.001)
                {
                    half4 sum=SampleFire(i.uv,lo,hi)*4;
                    sum+=SampleFire(i.uv+float2(d.x,0),lo,hi)*2+SampleFire(i.uv-float2(d.x,0),lo,hi)*2;
                    sum+=SampleFire(i.uv+float2(0,d.y),lo,hi)*2+SampleFire(i.uv-float2(0,d.y),lo,hi)*2;
                    sum+=SampleFire(i.uv+d,lo,hi)+SampleFire(i.uv-d,lo,hi);
                    sum+=SampleFire(i.uv+float2(d.x,-d.y),lo,hi)+SampleFire(i.uv+float2(-d.x,d.y),lo,hi);
                    c=half4(sum.rgb/max(sum.a,.0001),sum.a/16);
                }
                half3 hsv=RgbToHsv(c.rgb); hsv.x+=_HueShift; hsv.y*=_SaturationMultiply;
                // Preserve the source shader's double vertex-color modulation.
                half3 rgb=HsvToRgb(hsv)*_OverallTint.rgb*_Emissive*i.color.rgb*i.color.rgb;
                return half4(FocusWallFog(rgb,weight),c.a*i.color.a*i.color.a);
            }
            ENDHLSL
        }
    }
}
