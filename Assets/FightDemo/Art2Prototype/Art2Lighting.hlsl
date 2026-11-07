#ifndef ART2_LIGHTING_INCLUDED
#define ART2_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Wrapped diffuse retains painted sprite detail without needing authored normal maps.
half3 Art2Light(half3 normalWS, float3 positionWS, half3 vertexLight, float2 screenUV, half diffuseFloor)
{
    half3 lighting = _AmbientTint.rgb * _AmbientStrength;
    Light main = GetMainLight(TransformWorldToShadowCoord(positionWS));
    lighting += main.color * main.distanceAttenuation * main.shadowAttenuation *
        lerp(diffuseFloor, 1.0h, saturate(dot(normalWS, main.direction))) * _LightInfluence;
    #if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = screenUV;
    #if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        Light light = GetAdditionalLight(lightIndex, positionWS);
        lighting += light.color * light.distanceAttenuation *
            lerp(diffuseFloor, 1.0h, saturate(dot(normalWS, light.direction))) * _LightInfluence;
    }
    #endif
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light light = GetAdditionalLight(lightIndex, positionWS);
        lighting += light.color * light.distanceAttenuation *
            lerp(diffuseFloor, 1.0h, saturate(dot(normalWS, light.direction))) * _LightInfluence;
    LIGHT_LOOP_END
    #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
    lighting += vertexLight * _LightInfluence;
    #endif
    return min(lighting, 2.0h);
}

// Preserve existing floor/other material lighting unless it explicitly opts in.
half3 Art2Light(half3 normalWS, float3 positionWS, half3 vertexLight, float2 screenUV)
{
    return Art2Light(normalWS, positionWS, vertexLight, screenUV, 0.3h);
}
#endif
