Shader "RhythmHunter/Aquarium Water Overlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Water Mask", 2D) = "black" {}
        _NoiseTex ("Flow Noise", 2D) = "gray" {}
        _WaterTint ("Water Tint", Color) = (0.25, 0.95, 1.0, 0.15)
        _FoamTint ("Shore Foam Tint", Color) = (0.82, 1.0, 1.0, 0.3)
        _Intensity ("Flow Intensity", Range(0, 2)) = 0.8
        _NoiseScaleA ("Noise Scale A", Float) = 3.2
        _NoiseScaleB ("Noise Scale B", Float) = 5.1
        _FlowA ("Flow A", Vector) = (0.025, 0.012, 0, 0)
        _FlowB ("Flow B", Vector) = (-0.018, 0.022, 0, 0)
        _EdgeWidth ("Shore Edge Width", Range(0.0005, 0.02)) = 0.004
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _NoiseTex;
            fixed4 _WaterTint;
            fixed4 _FoamTint;
            float _Intensity;
            float _NoiseScaleA;
            float _NoiseScaleB;
            float4 _FlowA;
            float4 _FlowB;
            float _EdgeWidth;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color;
                return output;
            }

            float AmbientRing(float2 uv, float2 center, float phase)
            {
                float cycle = frac(_Time.y * 0.085 + phase);
                float radius = cycle * 0.22;
                float distanceToCenter = length((uv - center) * float2(1.3333, 1.0));
                float ring = 1.0 - smoothstep(0.006, 0.016, abs(distanceToCenter - radius));
                return ring * (1.0 - cycle);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float mask = tex2D(_MaskTex, input.uv).r;
                float spriteAlpha = tex2D(_MainTex, input.uv).a;
                float2 uvA = input.uv * _NoiseScaleA + _Time.y * _FlowA.xy;
                float2 uvB = input.uv.yx * _NoiseScaleB + _Time.y * _FlowB.xy;
                float noiseA = tex2D(_NoiseTex, uvA).r;
                float noiseB = tex2D(_NoiseTex, uvB).r;
                float caustic = smoothstep(0.48, 0.76, 1.0 - abs(noiseA - noiseB));
                float pulse = 0.72 + 0.28 * sin(_Time.y * 0.85 + input.uv.x * 13.0 + input.uv.y * 9.0);

                float2 edgeOffset = float2(_EdgeWidth, _EdgeWidth);
                float neighbor = min(
                    min(tex2D(_MaskTex, input.uv + float2(edgeOffset.x, 0)).r,
                        tex2D(_MaskTex, input.uv - float2(edgeOffset.x, 0)).r),
                    min(tex2D(_MaskTex, input.uv + float2(0, edgeOffset.y)).r,
                        tex2D(_MaskTex, input.uv - float2(0, edgeOffset.y)).r));
                float shore = saturate(mask - neighbor) * 4.0;
                float ambientRipple = max(
                    AmbientRing(input.uv, float2(0.31, 0.58), 0.08),
                    max(AmbientRing(input.uv, float2(0.58, 0.34), 0.43),
                        AmbientRing(input.uv, float2(0.76, 0.61), 0.77)));

                fixed4 color = lerp(_WaterTint, _FoamTint, shore);
                color.rgb = lerp(color.rgb, _FoamTint.rgb, ambientRipple * 0.45);
                float flowAlpha = _WaterTint.a * (0.22 + caustic * pulse * _Intensity)
                    + ambientRipple * 0.11;
                color.a = saturate(flowAlpha + shore * _FoamTint.a) * mask * spriteAlpha;
                color.rgb *= input.color.rgb;
                color.a *= input.color.a;
                return color;
            }
            ENDCG
        }
    }
}
