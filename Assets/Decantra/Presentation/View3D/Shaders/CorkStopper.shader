/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/
Shader "Decantra/CorkStopper"
{
    // ──────────────────────────────────────────────────────────────────────────
    // Opaque cork stopper shader for completed 3D bottle caps.
    //
    // Visual effects:
    //   • Warm matte base colour (natural cork beige/brown).
    //   • Procedural value-noise pore pattern — small dark specks on the cork face,
    //     fibrous grain lines on the cylindrical sides.
    //   • Lambertian diffuse shading driven by the scene directional light so the
    //     cylindrical side tapers naturally from bright front-face to shadowed back.
    //   • Soft Blinn-Phong specular (shininess = 14 → very matte, smoothness ≈ 0.1).
    //   • _Color tint set from MaterialPropertyBlock to match the bottle's liquid colour.
    //
    // Lighting: reads _MainLightPosition (URP) / _WorldSpaceLightPos0 (built-in).
    // Mobile-safe — no texture samples, no screen-space ops.
    // Works on Adreno 610 / Mali-G52.
    // ──────────────────────────────────────────────────────────────────────────
    Properties
    {
        _Color      ("Liquid Tint",   Color ) = (0.80, 0.72, 0.58, 1)
        _PoreScale  ("Pore Scale",    Range(8, 80)) = 32
        _PoreDepth  ("Pore Depth",    Range(0, 0.55)) = 0.22
        _GrainScale ("Grain Scale",   Range(2, 30))  = 10
        _GrainDepth ("Grain Depth",   Range(0, 0.4))  = 0.14
        _Ambient    ("Ambient",       Range(0, 0.6))  = 0.46
        _SpecPower  ("Spec Shininess", Range(4, 32))   = 14
        _SpecStr    ("Spec Strength",  Range(0, 0.3))  = 0.06
    }

    SubShader
    {
        Tags
        {
            "Queue"  = "Transparent+2"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "CORK"
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Color;
            float  _PoreScale;
            float  _PoreDepth;
            float  _GrainScale;
            float  _GrainDepth;
            float  _Ambient;
            float  _SpecPower;
            float  _SpecStr;

            // URP + built-in pipeline light direction compatibility
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
                float3 worldPos : TEXCOORD2;
                float2 uv       : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS     = UnityObjectToClipPos(IN.posOS);
                OUT.normalWS  = UnityObjectToWorldNormal(IN.normalOS);
                float3 wp     = mul(unity_ObjectToWorld, IN.posOS).xyz;
                OUT.viewDirWS = normalize(_WorldSpaceCameraPos - wp);
                OUT.worldPos  = wp;
                OUT.uv        = IN.uv;
                return OUT;
            }

            // ── Noise helpers ──────────────────────────────────────────────────
            // Deterministic float hash for 2D position — no texture required
            float hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Smooth value noise (bilinear interpolation of hash grid)
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);   // Hermite smoothstep
                return lerp(
                    lerp(hash2(i),               hash2(i + float2(1,0)), u.x),
                    lerp(hash2(i + float2(0,1)), hash2(i + float2(1,1)), u.x),
                    u.y);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // ── Directional light ─────────────────────────────────────────
                float3 rawLightDir = _MainLightPosition.xyz;
                if (dot(rawLightDir, rawLightDir) < 0.001)
                    rawLightDir = _WorldSpaceLightPos0.xyz;
                float3 L = normalize(rawLightDir);

                // ── Lambertian diffuse ────────────────────────────────────────
                float NdotL  = saturate(dot(N, L));
                float diffuse = lerp(_Ambient, 1.0, NdotL);

                // Blend in a mild front-center bias so cork tops read more centered
                // from gameplay distance and better match the liquid's cylindrical shading.
                float cylCenter = saturate(cos((IN.uv.x - 0.5) * 3.14159265));
                float cylShade = lerp(0.88, 1.0, pow(cylCenter, 1.35));
                diffuse = lerp(diffuse, cylShade, 0.35);

                // ── Soft Blinn-Phong specular (matte cork) ───────────────────
                float3 H    = normalize(L + V);
                float  spec = pow(saturate(dot(N, H)), _SpecPower) * _SpecStr;

                // ── Procedural surface detail ─────────────────────────────────
                // Pores: scattered small dots on end caps and sides.
                // Using mesh UV avoids world-position asymmetry: the pattern is
                // identical regardless of the bottle's screen column / world X.
                //
                // Side UV: uv.x = azimuth 0..1 around circumference, uv.y = height 0..1
                // Cap UV:  set in vertex as (cos*0.5+0.5, sin*0.5+0.5), centred at 0.5
                float2 poreUVSide = float2(IN.uv.x * _PoreScale * 2.0,
                                           IN.uv.y * _PoreScale * 2.0);
                float2 poreUVCap  = (IN.uv - float2(0.5, 0.5)) * _PoreScale;
                float2 poreUV = lerp(poreUVSide, poreUVCap,
                                     saturate(abs(N.y) * 2.0));
                float pore   = valueNoise(poreUV);
                float poreMask = saturate(pow(pore, 3.5));   // small dark specks
                float poreDark = 1.0 - poreMask * _PoreDepth;

                // Grain lines: thin vertical streaks along cork fibres (cylindrical sides only).
                // UV.x provides the horizontal grain coordinate, giving a symmetric,
                // position-independent pattern that looks the same for every bottle.
                float2 grainUV = float2(IN.uv.x * _GrainScale * 4.0,
                                        IN.uv.y * _GrainScale);
                float grain    = valueNoise(grainUV);
                float grainMask = saturate((1.0 - abs(N.y)) * grain);   // fades on caps
                float grainDark  = 1.0 - grainMask * _GrainDepth;

                // ── Subtle ambient occlusion tint where cork meets neck glass ─
                // The bottom 4% of the UV height is where the cork disappears into
                // the neck. Darken it slightly to simulate AO contact shadow.
                float aoMask = smoothstep(0.0, 0.04, IN.uv.y);   // 0=dark rim, 1=open
                float ao     = lerp(0.55, 1.0, aoMask);

                // ── Compose ──────────────────────────────────────────────────
                float3 baseColor = _Color.rgb * poreDark * grainDark * ao;
                float3 finalColor = baseColor * (diffuse + spec);

                return float4(saturate(finalColor), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Diffuse"
}
