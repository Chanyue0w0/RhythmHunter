Shader "Hidden/RhythmHunter/RoarDistortion"
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
            float4 _RoarCenterRadius; // viewport center, radius and width in screen heights
            float4 _RoarWave; // strength in render pixels, pixel step
            float4 _RoarResolution;
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 resolution = max(_RoarResolution.xy, 1);
                float2 deltaPixels = (input.texcoord - _RoarCenterRadius.xy) * resolution;
                float distancePixels = length(deltaPixels);
                float radius = _RoarCenterRadius.z * resolution.y;
                float width = max(1, _RoarCenterRadius.w * resolution.y);
                float signedBand = (distancePixels - radius) / width;
                float mask = 1 - smoothstep(0, 1, abs(signedBand));
                float ripple = cos(signedBand * 3.14159265);
                float2 displacement = deltaPixels / max(1, distancePixels) * mask * ripple * _RoarWave.x;
                if (_RoarWave.y > 0) displacement = round(displacement / _RoarWave.y) * _RoarWave.y;
                float2 uv = clamp(input.texcoord + displacement / resolution, .5 / resolution, 1 - .5 / resolution);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            }
            ENDHLSL
        }
    }
}
