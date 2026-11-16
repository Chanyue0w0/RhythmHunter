Shader "RhythmHunter/Art2/BossDoorFan"
{
    Properties { [HDR] _Color ("Light Color", Color) = (1,0.8,0.4,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _DoorLightPosition, _DoorLightForward, _DoorCone;
            CBUFFER_END
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            V Vert(A i) { V o; o.positionWS=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(o.positionWS); o.uv=i.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                float edges=smoothstep(0,.1,i.uv.x)*smoothstep(0,.1,1-i.uv.x);
                float endFade=1-smoothstep(.6,1,i.uv.y);
                float weight=edges*endFade;
                if(_DoorCone.w>.5)
                {
                    float3 ray=i.positionWS-_DoorLightPosition.xyz;
                    float distanceToLight=max(length(ray),.001);
                    weight=smoothstep(_DoorCone.y,_DoorCone.z,dot(ray/distanceToLight,_DoorLightForward.xyz));
                    weight*=1-smoothstep(_DoorCone.x*.7,_DoorCone.x,distanceToLight);
                }
                half shadow=1;
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    #if USE_CLUSTER_LIGHT_LOOP
                        int index=lightIndex;
                    #else
                        int index=GetPerObjectLightIndex(lightIndex);
                    #endif
                    #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                        float3 lightPosition=_AdditionalLightsBuffer[index].position.xyz;
                    #else
                        float3 lightPosition=_AdditionalLightsPosition[index].xyz;
                    #endif
                    if(dot(lightPosition-_DoorLightPosition.xyz,lightPosition-_DoorLightPosition.xyz)<.0001)
                    {
                        Light light=GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1));
                        shadow=light.shadowAttenuation;
                    }
                LIGHT_LOOP_END
                #endif
                return half4(_Color.rgb*weight*shadow,0);
            }
            ENDHLSL
        }
    }
}
