#ifndef ART2_LIGHTING_INCLUDED
#define ART2_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Wrapped diffuse retains painted sprite detail without needing authored normal maps.
half3 Art2Light(half3 normalWS, float3 positionWS, half3 vertexLight)
{
    half3 lighting = _AmbientTint.rgb * _AmbientStrength;
    Light main = GetMainLight();
    lighting += main.color * main.distanceAttenuation *
        (0.3h + 0.7h * saturate(dot(normalWS, main.direction))) * _LightInfluence;
    #if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
    for (uint i = 0u; i < count; i++)
    {
        Light light = GetAdditionalLight(i, positionWS);
        lighting += light.color * light.distanceAttenuation *
            (0.3h + 0.7h * saturate(dot(normalWS, light.direction))) * _LightInfluence;
    }
    #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
    lighting += vertexLight * _LightInfluence;
    #endif
    return min(lighting, 2.0h);
}
#endif
