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
    //   Total glass alpha is capped at _MaxGlassAlpha (default 0.26) so that at
    //   least 74% of liquid color always shows through the glass face.
    //   Specular contribution is restricted to ≤ _SpecMaxContrib (default 0.12)
    //   additive luminance so bright highlights cannot bleach liquid colors.
    //
    // Performance: single directional light, no real-time shadows, no GI sampling.
    // Mobile-safe. Works on Adreno 610 / Mali-G52.
    // ──────────────────────────────────────────────────────────────────────────
    Properties
    {
        _GlassTint      ("Glass Tint",     Color  ) = (0.88, 0.93, 1.0, 0.08)
        _FresnelPower   ("Fresnel Power",  Range(1,8)) = 4.5
        _FresnelColor   ("Fresnel Color",  Color  ) = (1,1,1,0.22)
        _SpecColor2     ("Specular Color", Color  ) = (1,1,1,1)
        _Shininess      ("Shininess",      Range(16,256)) = 128
        _Smoothness     ("Smoothness",     Range(0,1)) = 0.97
        _RefractionStrength("Refraction Strength", Range(0,0.08)) = 0.02
        _AbsorptionStrength("Absorption Strength", Range(0,4)) = 1.1
        _MicroNormalScale("Micro Normal Scale", Range(0,0.2)) = 0.06
        _ReflectionStrip("Reflection Strip Strength", Range(0,1)) = 0.10
        _ReflectionX    ("Reflection Strip X", Range(-1,1)) = 0.55
        _ReflectionWidth("Reflection Strip Width", Range(0.01,0.5)) = 0.08

        // Block A: hard cap on total glass face alpha (prevents liquid-colour washout).
        // liquid_visible >= 1 - _MaxGlassAlpha. Default 0.18 → ≥82% liquid shows through.
        _MaxGlassAlpha  ("Max Glass Alpha", Range(0,1)) = 0.18

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
            float  _CapacityRatio;

            struct Attributes { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Body-only height scaling so dome (bottom) and shoulder+neck+rim (top)
                // keep their full shape — only the cylindrical body section changes height.
                // Mesh coordinate ranges (from BottleMeshGenerator constants):
                //   Dome spans yMin=-0.80 up to kDomeTop=-0.61  (= -BodyHalf + DomeRadius*0.5)
                //   Body cylinder from -0.61 to kBodyTop=+0.80  (= BodyHalfHeight)
                //   Shoulder/neck/rim above +0.80
                //   kBodyHeight = 0.80 - (-0.61) = 1.41
                const float kDomeTop   = -0.61; // dome / body boundary
                const float kBodyTop   =  0.80; // body / shoulder boundary
                const float kBodyHeight = 1.41; // kBodyTop - kDomeTop
                float posY = IN.posOS.y;
                if (posY > kBodyTop)
                    posY -= kBodyHeight * (1.0 - _CapacityRatio); // shift shoulder/neck down
                else if (posY > kDomeTop)
                    posY = kDomeTop + (posY - kDomeTop) * _CapacityRatio; // scale body
                // posY <= kDomeTop: dome untouched
                IN.posOS.y = posY;
                OUT.posCS = UnityObjectToClipPos(IN.posOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Inner surface is slightly darker / more saturated
                float4 col = float4(_GlassTint.rgb * 0.75, _GlassTint.a * 0.5);

                // Block E: dark bands for sink-only bottles
                if (_SinkOnly > 0.5)
                {
                    float uvY = IN.uv.y;
                    float rimBand = saturate((uvY - 0.82) / 0.04);         // 0→1 as y goes 0.82→0.86
                    float baseBand = saturate((0.07 - uvY) / 0.04);        // 0→1 as y goes 0.07→0.03
                    float band = saturate(rimBand + baseBand);
                    col.rgb = lerp(col.rgb, float3(0.05, 0.05, 0.07), band * 0.9);
                    col.a = lerp(col.a, 0.88, band);
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
                // Body-only height scaling — same 3-zone logic as the back-face pass.
                const float kDomeTop   = -0.61;
                const float kBodyTop   =  0.80;
                const float kBodyHeight = 1.41;
                float posY = IN.posOS.y;
                if (posY > kBodyTop)
                    posY -= kBodyHeight * (1.0 - _CapacityRatio);
                else if (posY > kDomeTop)
                    posY = kDomeTop + (posY - kDomeTop) * _CapacityRatio;
                IN.posOS.y = posY;
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

                // ── Compose ────────────────────────────────────────────────
                float3 baseTint = _GlassTint.rgb * absorb;
                float3 color = baseTint + fresnelCol + specCol + stripCol;
                // Block A fix: cap total alpha to _MaxGlassAlpha so liquid colours
                // always show through clearly.
                float alpha = _GlassTint.a + fresnel * _FresnelColor.a * 0.5;
                alpha = min(alpha, _MaxGlassAlpha);

                // ── Block E: sink-only dark bands ──────────────────────────
                // When _SinkOnly=1, override rim + base regions with near-black.
                // The bands are pinned in UV space so they cannot change layout bounds.
                // Specular/Fresnel contributions are zeroed in band regions so the
                // marking remains visible under strong lighting.
                if (_SinkOnly > 0.5)
                {
                    float uvY = IN.uv.y;
                    float rimBand  = saturate((uvY - 0.82) / 0.04);   // neck top band
                    float baseBand = saturate((0.07 - uvY) / 0.04);   // base bottom band
                    float band = saturate(rimBand + baseBand);

                    // Replace glass colour with near-black in bands; blend edges softly.
                    color = lerp(color, float3(0.04, 0.04, 0.06), band * 0.95);
                    // Boost alpha in bands so black is clearly visible.
                    alpha = lerp(alpha, 0.92, band);
                }

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
