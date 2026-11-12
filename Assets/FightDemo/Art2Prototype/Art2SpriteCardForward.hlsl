
            #include "Art2Lighting.hlsl"
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 vertexLight : TEXCOORD3;
                half3 tangentWS : TEXCOORD4;
                half3 bitangentWS : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(float3(0,0,-1));
                output.tangentWS = TransformObjectToWorldDir(float3(unity_SpriteProps.x,0,0));
                output.bitangentWS = TransformObjectToWorldDir(float3(0,unity_SpriteProps.y,0));
                output.vertexLight = VertexLighting(output.positionWS, output.normalWS);
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                return output;
            }
            half4 PremultipliedSample(float2 uv)
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return half4(texel.rgb * texel.a, texel.a);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Material-uniform branch: sharp actors/foreground pay for one texture sample.
                half focusWeight = FocusWallWeight(input.positionWS);
                half blurRadius = max(_BlurRadius, FocusWallBlurAmount(input.positionWS));
                if (blurRadius > 0.001h)
                {
                    float2 stepUV = _MainTex_TexelSize.xy * blurRadius;
                    half4 sum = half4(color.rgb * color.a, color.a) * 4;
                    sum += PremultipliedSample(input.uv + float2(stepUV.x,0)) * 2;
                    sum += PremultipliedSample(input.uv - float2(stepUV.x,0)) * 2;
                    sum += PremultipliedSample(input.uv + float2(0,stepUV.y)) * 2;
                    sum += PremultipliedSample(input.uv - float2(0,stepUV.y)) * 2;
                    sum += PremultipliedSample(input.uv + stepUV);
                    sum += PremultipliedSample(input.uv - stepUV);
                    sum += PremultipliedSample(input.uv + float2(stepUV.x,-stepUV.y));
                    sum += PremultipliedSample(input.uv + float2(-stepUV.x,stepUV.y));
                    color = half4(sum.rgb / max(sum.a, 0.0001h), sum.a / 16);
                }
                color *= input.color;
                half luminance = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                color.rgb = lerp(luminance.xxx, color.rgb, _FocusSaturation);
                color.rgb = lerp(half3(0.18h, 0.18h, 0.18h), color.rgb, _FocusContrast);
                if (_SurfaceDebugMode > 0.5h && _SurfaceDebugMode < 1.5h)
                    return color;
                half3 normalWS = normalize(input.normalWS);
                if (_UseNormalMap > 0.5h && _NormalStrength > 0.001h)
                {
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalStrength);
                    normalWS = normalize(normalTS.x * normalize(input.tangentWS)
                        + normalTS.y * normalize(input.bitangentWS) + normalTS.z * normalWS);
                }
                if (_SurfaceDebugMode > 2.5h)
                    return half4(normalWS * 0.5h + 0.5h, color.a);
                if (_SurfaceDebugMode > 1.5h)
                    color.rgb = half3(0.5h, 0.5h, 0.5h);
                half3 rgb = color.rgb * (Art2Light(normalWS, input.positionWS, input.vertexLight, GetNormalizedScreenSpaceUV(input.positionCS), _DiffuseFloor) + _EmissionStrength);
                rgb = lerp(rgb, _AtmosphereTint.rgb, _AtmosphereMix);
                rgb = FocusWallFog(rgb, focusWeight);
                return half4(rgb, color.a);
            }
