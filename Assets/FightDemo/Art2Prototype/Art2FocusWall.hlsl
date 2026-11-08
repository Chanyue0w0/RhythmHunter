#ifndef ART2_FOCUS_WALL_INCLUDED
#define ART2_FOCUS_WALL_INCLUDED
float4 _FocusWallPlane;
float4 _FocusWallSettings;
half4 _FocusWallColor;
float4 _FocusWallBlurProfile;
half FocusWallBlurAmount(float3 positionWS)
{
    float distance = dot(_FocusWallPlane.xyz, positionWS) + _FocusWallPlane.w;
    float scale = _FocusWallBlurProfile.y > 0 ? _FocusWallBlurProfile.y : 1;
    float multiplier = _FocusWallBlurProfile.x > 0 ? _FocusWallBlurProfile.x : 1;
    return _FocusWallSettings.x * _FocusWallSettings.z * multiplier *
        smoothstep(0, max(.01, _FocusWallSettings.y * scale), distance);
}
half FocusWallWeight(float3 positionWS)
{
    float distance = dot(_FocusWallPlane.xyz, positionWS) + _FocusWallPlane.w;
    return _FocusWallSettings.x * smoothstep(0, max(.01, _FocusWallSettings.y), distance);
}
half3 FocusWallFog(half3 color, half weight)
{
    return lerp(color, _FocusWallColor.rgb, saturate(weight * _FocusWallSettings.w));
}
#endif
