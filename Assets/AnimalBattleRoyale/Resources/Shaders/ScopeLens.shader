Shader "Hidden/AnimalBattleRoyale/ScopeLens"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Blend ("Blend", Range(0, 1)) = 0
        _Aspect ("Aspect", Float) = 1.777778
        _LensRadius ("Lens Radius", Range(0.28, 0.48)) = 0.41
        _Distortion ("Distortion", Range(0, 0.35)) = 0.2
        _EdgeBlurPixels ("Edge Blur", Range(0, 14)) = 8.5
        _OutsideBrightness ("Outside Brightness", Range(0, 0.5)) = 0.13
        _ReticleColor ("Reticle Color", Color) = (0.18, 1, 0.08, 1)
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Blend;
            float _Aspect;
            float _LensRadius;
            float _Distortion;
            float _EdgeBlurPixels;
            float _OutsideBrightness;
            half4 _ReticleColor;

            float AntialiasedLine(float distanceToLine, float width)
            {
                return 1.0 - smoothstep(width, width * 1.85, distanceToLine);
            }

            float Ring(float radius, float center, float width)
            {
                return AntialiasedLine(abs(radius - center), width);
            }

            half4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                half4 original = tex2D(_MainTex, uv);

                float2 centered = uv - 0.5;
                float2 aspectPoint = float2(centered.x * _Aspect, centered.y);
                float radius = length(aspectPoint);
                float normalizedRadius = radius / max(_LensRadius, 0.001);
                float insideMask = 1.0 - smoothstep(0.995, 1.008, normalizedRadius);
                float outsideDistance = max(0.0, normalizedRadius - 1.0);
                float outsideRimInfluence = 1.0 - smoothstep(0.0, 0.28, outsideDistance);
                float rimInfluence = 1.0 - smoothstep(0.015, 0.24, abs(normalizedRadius - 1.0));

                // The center is almost untouched. Distortion grows cubically toward the
                // rim and continues briefly outside it so both sides read as thick glass.
                float innerDistortion = pow(saturate(normalizedRadius), 3.0) * insideMask;
                float outerDistortion = pow(saturate(outsideRimInfluence), 2.0) * (1.0 - insideMask);
                float distortionEnvelope = innerDistortion + outerDistortion * 0.85;
                float radialScale = 1.0 + _Distortion * distortionEnvelope
                                  * (0.62 + rimInfluence * 0.78);
                float2 warpedAspectPoint = aspectPoint * radialScale;
                float2 warpedUv = 0.5 + float2(warpedAspectPoint.x / _Aspect,
                                               warpedAspectPoint.y);
                warpedUv = clamp(warpedUv, _MainTex_TexelSize.xy,
                    1.0 - _MainTex_TexelSize.xy);

                float2 radialDirection = radius > 0.0001
                    ? float2(aspectPoint.x / _Aspect, aspectPoint.y) / radius
                    : float2(0.0, 1.0);
                float blurEnvelope = saturate(pow(saturate(normalizedRadius), 3.2) * insideMask
                                             + rimInfluence * 0.85);
                float2 blurStep = radialDirection * _MainTex_TexelSize.y
                                * _EdgeBlurPixels * blurEnvelope;
                float2 tangentStep = float2(-blurStep.y, blurStep.x) * 0.62;

                half3 centerSample = tex2D(_MainTex, warpedUv).rgb;
                half3 blurred = centerSample * 0.34h;
                blurred += tex2D(_MainTex, warpedUv + blurStep).rgb * 0.165h;
                blurred += tex2D(_MainTex, warpedUv - blurStep).rgb * 0.165h;
                blurred += tex2D(_MainTex, warpedUv + tangentStep).rgb * 0.165h;
                blurred += tex2D(_MainTex, warpedUv - tangentStep).rgb * 0.165h;

                float2 chromaticOffset = blurStep * 0.82;
                half3 chromatic;
                chromatic.r = tex2D(_MainTex, warpedUv + chromaticOffset).r;
                chromatic.g = centerSample.g;
                chromatic.b = tex2D(_MainTex, warpedUv - chromaticOffset).b;
                half3 lensColor = lerp(centerSample, blurred, blurEnvelope * 0.82h);
                lensColor = lerp(lensColor, chromatic, blurEnvelope * 0.3h);

                half3 outsideColor = lensColor * _OutsideBrightness;
                half3 scopedColor = lerp(outsideColor, lensColor, insideMask);

                // Multiple green glass rings give the border depth without a bitmap asset.
                float broadGlow = Ring(normalizedRadius, 1.0, 0.075);
                float darkOuterRing = Ring(normalizedRadius, 1.105, 0.042);
                float brightOuterRing = Ring(normalizedRadius, 1.0, 0.008);
                float brightInnerRing = Ring(normalizedRadius, 0.89, 0.0055);
                scopedColor = lerp(scopedColor, _ReticleColor.rgb * 0.18h,
                    broadGlow * 0.48h);
                scopedColor *= 1.0h - darkOuterRing * 0.55h;
                scopedColor = lerp(scopedColor, _ReticleColor.rgb * 1.35h,
                    saturate(brightOuterRing + brightInnerRing * 0.78));

                // Aspect-correct procedural reticle, inspired by the supplied green scope.
                float2 scopePoint = aspectPoint / max(_LensRadius, 0.001);
                float scopeRadius = length(scopePoint);
                float pixelWidth = max(_MainTex_TexelSize.y / _LensRadius, 0.0012);
                float horizontal = AntialiasedLine(abs(scopePoint.y), pixelWidth * 0.8)
                                 * (1.0 - smoothstep(0.86, 0.89, abs(scopePoint.x)));
                float vertical = AntialiasedLine(abs(scopePoint.x), pixelWidth * 0.8)
                               * (1.0 - smoothstep(0.86, 0.89, abs(scopePoint.y)));
                float centerRing = Ring(scopeRadius, 0.065, pixelWidth * 0.92);
                float centerDot = 1.0 - smoothstep(pixelWidth * 0.6,
                    pixelWidth * 1.7, scopeRadius);

                float horizontalBar = step(0.5, abs(scopePoint.x))
                                    * (1.0 - step(0.86, abs(scopePoint.x)))
                                    * AntialiasedLine(abs(scopePoint.y), 0.012);
                float verticalBar = step(0.5, abs(scopePoint.y))
                                  * (1.0 - step(0.86, abs(scopePoint.y)))
                                  * AntialiasedLine(abs(scopePoint.x), 0.012);

                float reticle = saturate(horizontal + vertical + centerRing + centerDot
                                        + horizontalBar + verticalBar);
                reticle *= insideMask;
                float reticleShadow = saturate(
                    AntialiasedLine(abs(scopePoint.y), pixelWidth * 2.2)
                    + AntialiasedLine(abs(scopePoint.x), pixelWidth * 2.2)) * 0.22;
                reticleShadow *= insideMask;
                scopedColor *= 1.0h - reticleShadow;
                scopedColor = lerp(scopedColor, _ReticleColor.rgb * 1.25h, reticle);

                return half4(lerp(original.rgb, scopedColor, saturate(_Blend)), original.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
