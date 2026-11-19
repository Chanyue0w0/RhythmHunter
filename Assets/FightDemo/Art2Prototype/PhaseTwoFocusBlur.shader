Shader "Hidden/RhythmHunter/PhaseTwoFocusBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _FocusShape, _FocusBlur;
            float _OutsideDarkness;
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                half4 original=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,uv,0);
                float t=saturate((uv.y-_FocusBlur.y)/max(.001,_FocusBlur.x-_FocusBlur.y));
                float width=lerp(_FocusShape.z,_FocusShape.y,t);
                float outside=smoothstep(0,max(.001,_FocusShape.w),abs(uv.x-_FocusShape.x)-width*.5);
                float mask=outside*_FocusBlur.w;
                if(outside<=0)return original;
                float2 d=_BlitTexture_TexelSize.xy*_FocusBlur.z;
                half4 blur=original*4;
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,0),0)*2;
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv-float2(d.x,0),0)*2;
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv+float2(0,d.y),0)*2;
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv-float2(0,d.y),0)*2;
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv+d,0);
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv-d,0);
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,-d.y),0);
                blur+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv+float2(-d.x,d.y),0);
                half4 result=lerp(original,blur/16,mask);
                result.rgb*=1-outside*_OutsideDarkness;
                return result;
            }
            ENDHLSL
        }
    }
}
