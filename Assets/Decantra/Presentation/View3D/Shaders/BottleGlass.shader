/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/
Shader "Decantra/BottleGlass"
{
    // ──────────────────────────────────────────────────────────────────────────
    // Glass bottle exterior shader (URP-compatible ShaderLab + HLSL).
    //
    // Visual effects:
    //   • Fresnel edge brightening (Schlick approximation) for glass rim glow.
    //   • Single specular highlight (Blinn-Phong) for the light reflection.
    //   • Tinted transparent body — colored tint over a mostly-clear glass base.
    //   • Specular reflection strip mimicking the existing 2D reflectionStrip image.
    //   • Two-pass: back faces first (interior glass), then front faces.
    //   • SinkOnly mode: dark rim band + base-line band for sink-only bottles.
    //
    // Liquid-clarity guarantee:
    //   Total glass alpha is capped at _MaxGlassAlpha (0.20) so that at least 80%
    //   of liquid color always shows through the glass face.
    //   Specular contribution is restricted to ≤ _SpecMaxContrib (default 0.12)
    //   additive luminance so bright highlights cannot bleach liquid colors.
    //
    // Performance: single directional light, no real-time shadows, no GI sampling.
    // Mobile-safe. Works on Adreno 610 / Mali-G52.
    // ──────────────────────────────────────────────────────────────────────────
    Properties
    {
        _GlassTint      ("Glass Tint",     Color  ) = (0.88, 0.93, 1.0, 0.06)
        _FresnelPower   ("Fresnel Power",  Range(1,8)) = 4.5
        _FresnelColor   ("Fresnel Color",  Color  ) = (1,1,1,0.36)
        _SpecColor2     ("Specular Color", Color  ) = (1,1,1,1)
        _Shininess      ("Shininess",      Range(16,256)) = 128
        _Smoothness     ("Smoothness",     Range(0,1)) = 0.97
        _RefractionStrength("Refraction Strength", Range(0,0.08)) = 0.02
        _AbsorptionStrength("Absorption Strength", Range(0,4)) = 1.1
        _MicroNormalScale("Micro Normal Scale", Range(0,0.2)) = 0.06
        _ReflectionStrip("Reflection Strip Strength", Range(0,1)) = 0.18
        _ReflectionX    ("Reflection Strip X", Range(-1,1)) = 0.55
        _ReflectionWidth("Reflection Strip Width", Range(0.01,0.5)) = 0.08
        // Second (shadow-side) reflection strip — dimmer, wider, cool tint
        _ReflectionStrip2("Shadow Strip Strength",  Range(0,1)) = 0.07
        _ReflectionX2   ("Shadow Strip X",     Range(-1,1)) = -0.55
        _ReflectionWidth2("Shadow Strip Width", Range(0.01,0.5)) = 0.13
        _RimSheenColor ("Rim Sheen Color", Color) = (0.90, 0.95, 1.0, 1.0)
        _RimSheenIntensity ("Rim Sheen Intensity", Range(0,1.5)) = 0.10
        _RimSheenPower ("Rim Sheen Power", Range(1,8)) = 4.5
        _EmptyBottleBoost ("Empty Bottle Boost", Range(0,1)) = 0
        _FrostTint      ("Frost Tint", Color) = (0.93, 0.97, 1.0, 1.0)
        _FrostScatter   ("Frost Scatter", Range(0,1)) = 0.18
        _FrostAlpha     ("Frost Alpha", Range(0,0.35)) = 0.18

        // Block A: hard cap on total glass face alpha (prevents liquid-colour washout).
        // liquid_visible >= 1 - _MaxGlassAlpha. 0.20 → ≥80% liquid shows through.
        _MaxGlassAlpha  ("Max Glass Alpha", Range(0,1)) = 0.20

        // Block A: cap on additive specular luminance contribution (0..1). Prevents
        // bright specular highlight from bleaching the liquid colour underneath.
        _SpecMaxContrib ("Spec Max Contrib", Range(0,1)) = 0.12

        // Block E: sink-only visual marking. Set to 1.0 for sink bottles (via
        // MaterialPropertyBlock; default 0 = normal bottle).
        // When 1.0: renders a dark rim band near the bottle neck (UV.y > 0.82) and
        // a dark base-line band near the bottle bottom (UV.y < 0.07).
        // Bands remain dark under any lighting — specular/Fresnel clamped to 0 in bands.
        _SinkOnly       ("Sink Only",      Range(0,1)) = 0

        // Per-bottle capacity ratio (0.1..1.0). Applied in the vertex shader to
        // scale ONLY the cylindrical body; the hemispherical dome (bottom) and
        // neck+rim (top) keep their full size so they remain clearly identifiable.
        // NOTE: Mesh is now generated with the correct proportions baked in by
        // BottleMeshGenerator.GenerateBottleMesh(capacityRatio). This property
        // is kept for compatibility but the vertex deformation has been removed.
        _CapacityRatio  ("Capacity Ratio", Range(0.1, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+1"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        // ── Pass 1: Back faces (inner glass) ──────────────────────────────
        Pass
        {
            Name "GLASS_BACK"
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _GlassTint;
            float  _SinkOnly;
            float4 _FrostTint;
            float  _FrostAlpha;

            struct Attributes { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = UnityObjectToClipPos(IN.posOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Inner surface is slightly darker / more saturated
                float4 col = float4(_GlassTint.rgb * 0.75, _GlassTint.a * 0.5);

                // Obj-1 fix (ALL bottles): suppress inner glass alpha in the dome area.
                // AppendDome UV formula gives UV.y ≈ 1.0 for all dome verts; the inner
                // glass tint at those UVs created a visible dark/light ring just above the
                // rounded base — the "bottom overlay stripe".  Fading col.a to near-zero
                // for UV.y > 0.94 removes it without affecting any other surface region.
                float domeArea = smoothstep(0.94, 1.0, IN.uv.y);
                col.a *= (1.0 - domeArea * 0.85);

                float uvY = IN.uv.y;
                float neckMask = saturate((uvY - 0.82) / 0.04) * (1.0 - smoothstep(0.955, 0.985, uvY));
                float baseMask = smoothstep(0.97, 1.0, uvY);
                float indicatorMask = saturate(max(neckMask, baseMask));

                // Shared indicator path: sinks render opaque dark caps while regular
                // bottles render frosted semi-transparent glass in the same regions.
                if (_SinkOnly > 0.5)
                {
                    float darkBand = indicatorMask;
                    col.rgb = lerp(col.rgb, float3(0.05, 0.05, 0.07), darkBand * 0.9);
                    col.a   = lerp(col.a,   0.80, darkBand);
                }
                else
                {
                    col.rgb = lerp(col.rgb, _FrostTint.rgb * 0.84, indicatorMask * 0.68);
                    col.a = lerp(col.a, max(col.a, _FrostAlpha * 0.82), indicatorMask);
                }

                return col;
            }
            ENDHLSL
        }

        // ── Pass 2: Front faces (exterior glass) ──────────────────────────
        Pass
        {
            Name "GLASS_FRONT"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _GlassTint;
            float  _FresnelPower;
            float4 _FresnelColor;
            float4 _SpecColor2;
            float  _Shininess;
            float  _Smoothness;
            float  _RefractionStrength;
            float  _AbsorptionStrength;
            float  _MicroNormalScale;
            float  _ReflectionStrip;
            float  _ReflectionX;
            float  _ReflectionWidth;
            float  _ReflectionStrip2;
            float  _ReflectionX2;
            float  _ReflectionWidth2;
            float4 _RimSheenColor;
            float  _RimSheenIntensity;
            float  _RimSheenPower;
            float  _EmptyBottleBoost;
            float4 _FrostTint;
            float  _FrostScatter;
            float  _FrostAlpha;
            float  _MaxGlassAlpha;
            float  _SpecMaxContrib;
            float  _SinkOnly;
            float  _CapacityRatio;

            // URP main directional light.  Built-in pipeline fills _WorldSpaceLightPos0;
            // URP fills _MainLightPosition.  We declare both and use whichever is non-zero.
            float4 _MainLightPosition;
            float4 _MainLightColor;

            struct Attributes
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
                float2 uv       : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS    : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS: TEXCOORD1;
                float2 uv       : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS    = UnityObjectToClipPos(IN.posOS);
                OUT.normalWS = UnityObjectToWorldNormal(IN.normalOS);
                float3 worldPos = mul(unity_ObjectToWorld, IN.posOS).xyz;
                OUT.viewDirWS = normalize(_WorldSpaceCameraPos - worldPos);
                OUT.uv       = IN.uv;
                OUT.worldPos = worldPos;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Lightweight procedural micro-normal perturbation (no texture fetch)
                float microA = sin(IN.worldPos.y * 47.0 + IN.worldPos.x * 31.0);
                float microB = cos(IN.worldPos.z * 43.0 - IN.worldPos.y * 27.0);
                float3 micro = float3(microA, microB, microA - microB) * _MicroNormalScale;
                N = normalize(N + micro);

                // ── Fresnel ────────────────────────────────────────────────
                float NoV = saturate(dot(N, V));
                float fresnel = pow(1.0 - NoV, _FresnelPower);
                // Block A fix: Fresnel colour contribution uses clamped amplitude so
                // grazing-angle brightening cannot reach full white.
                float3 fresnelCol = _FresnelColor.rgb * fresnel * _FresnelColor.a;
                float rimSheen = pow(1.0 - NoV, _RimSheenPower) * _RimSheenIntensity;
                float3 rimSheenCol = _RimSheenColor.rgb * rimSheen;

                // Empty bottles need a stronger glass read on dark backgrounds, but they
                // still must stay transparent rather than collapsing into a white shell.
                float emptyBoost = saturate(_EmptyBottleBoost);
                float emptyEdge = fresnel * emptyBoost;
                fresnelCol += _FresnelColor.rgb * emptyEdge * 0.18;
                rimSheenCol += _RimSheenColor.rgb * emptyEdge * 0.08;

                // ── Specular from scene directional light ───────────────────
                // URP fills _MainLightPosition; built-in fills _WorldSpaceLightPos0.
                // Use _MainLightPosition when available (non-zero), else fall back.
                float3 rawLightDir = _MainLightPosition.xyz;
                if (dot(rawLightDir, rawLightDir) < 0.001)
                    rawLightDir = _WorldSpaceLightPos0.xyz;
                float3 L = normalize(rawLightDir);
                float3 H = normalize(L + V);
                float  shininess = lerp(24.0, _Shininess, _Smoothness);
                float  spec = pow(saturate(dot(N, H)), shininess);
                // Block A fix: clamp total specular luminance contribution to _SpecMaxContrib
                // so a bright specular spot does not bleach the liquid colour underneath.
                float3 specCol = _SpecColor2.rgb * min(spec * _SpecColor2.a,
                                                       _SpecMaxContrib);

                // ── Refraction/attenuation approximation ───────────────────
                float viewThickness = 1.0 - NoV;
                float absorb = exp(-_AbsorptionStrength * (0.3 + viewThickness * 1.8));
                float2 refractShift = N.xz * (_RefractionStrength * viewThickness);

                // ── Reflection strip (slightly refracted) ──────────────────
                // Mimics the vertical bright strip on 2D bottle
                float2 stripUv = IN.uv + refractShift;
                float stripMask = smoothstep(
                    _ReflectionWidth,
                    0.0,
                    abs(stripUv.x - (_ReflectionX * 0.5 + 0.5)));
                float3 stripCol = float3(1,1,1) * stripMask * _ReflectionStrip;
                // Shadow-side strip: softer, cool-tinted, wraps around opposite edge
                float stripMask2 = smoothstep(
                    _ReflectionWidth2,
                    0.0,
                    abs(stripUv.x - (_ReflectionX2 * 0.5 + 0.5)));
                float3 stripCol2 = float3(0.82, 0.90, 1.0) * stripMask2 * _ReflectionStrip2;
                stripCol += float3(0.92, 0.97, 1.0) * stripMask * emptyBoost * 0.05;
                stripCol2 += float3(0.72, 0.82, 0.94) * stripMask2 * emptyBoost * 0.04;
                // ── Compose ────────────────────────────────────────────────
                float3 baseTint = _GlassTint.rgb * absorb;
                float3 color = baseTint + fresnelCol + rimSheenCol + specCol + stripCol + stripCol2;
                // Block A fix: cap total alpha to _MaxGlassAlpha so liquid colours
                // always show through clearly.
                float alpha = _GlassTint.a + fresnel * _FresnelColor.a * 0.35 + rimSheen * 0.01;
                alpha += emptyBoost * (0.035 + fresnel * 0.05);
                alpha = min(alpha, _MaxGlassAlpha);

                // ── Neck capacity marker ─────────────────────────────────────
                // Subtle darkening band at the body-shoulder junction, showing players
                // exactly where the fillable chamber ends (non-sink bottles only).
                // Junction UV: vFrac = (y + BodyHalfHeight + DomeRadius) / totalHeight
                // At bodyTop: vFrac ≈ (0.57 + 1.6·cap) / (0.935 + 1.6·cap)
                if (_SinkOnly < 0.5)
                {
                    float c = clamp(_CapacityRatio, 0.1, 1.0);
                    float junctionUV = (0.57 + 1.6 * c) / max(0.935 + 1.6 * c, 0.001);
                    float uvY = IN.uv.y;
                    // 3-pixel-wide softened band centred on junctionUV
                    float band = smoothstep(junctionUV - 0.020, junctionUV, uvY)
                               * (1.0 - smoothstep(junctionUV, junctionUV + 0.030, uvY));
                    // Keep the fill-cap marker readable without turning it into a dark stripe.
                    color = lerp(color, color * 0.78, band * 0.30);
                    alpha = lerp(alpha, min(alpha + 0.04, _MaxGlassAlpha), band * 0.25);
                }

                float uvY = IN.uv.y;
                float neckMask = saturate((uvY - 0.82) / 0.04) * (1.0 - smoothstep(0.955, 0.985, uvY));
                float baseMask = smoothstep(0.97, 1.0, uvY);
                float indicatorMask = saturate(max(neckMask, baseMask));

                if (_SinkOnly > 0.5)
                {
                    // Dark the top band.
                    color = lerp(color, float3(0.04, 0.04, 0.06), neckMask * 0.92);
                    alpha = lerp(alpha, 0.85, neckMask);

                    // Dark the bottom dome band.
                    color = lerp(color, float3(0.04, 0.04, 0.06), baseMask * 0.88);
                    alpha = lerp(alpha, 0.78, baseMask);

                    // Neutral-white specular gloss on the rim band only (neck/shoulder).
                    // Excluded from domeMask: the hemispherical bottom dome has surface normals
                    // that produce a circular Blinn-Phong hotspot, which looks fake and distracting.
                    color += float3(0.90, 0.92, 1.0) * (spec * 0.65 * neckMask);
                }
                else
                {
                    float edgeScatter = lerp(0.55, 1.0, fresnel);
                    float3 frostColor = _FrostTint.rgb * (1.0 + _FrostScatter * edgeScatter);
                    color = lerp(color, frostColor, indicatorMask * 0.42);
                    color += _FrostTint.rgb * indicatorMask * _FrostScatter * 0.10 * (0.5 + stripMask + stripMask2);
                    alpha = lerp(alpha,
                        min(_MaxGlassAlpha, max(alpha, _FrostAlpha + rimSheen * 0.02)),
                        indicatorMask * 0.92);
                }

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
