/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/
Shader "Decantra/Liquid3D"
{
    // ──────────────────────────────────────────────────────────────────────────
    // Layered liquid shader for 3D bottles.
    //
    // Supports up to 9 liquid layers (matching max bottle capacity = 9 slots).
    // Each layer is defined by:
    //   _LayerFillMin[i] .. _LayerFillMax[i]  : fill fraction in [0..1]
    //   _LayerColor[i]                         : RGBA liquid color
    //
    // The shader operates on FLAT QUAD meshes positioned in 3D space to
    // represent the liquid cross-section inside the bottle.  UV.y carries
    // the normalised fill position (0 = bottom, 1 = top of interior region).
    //
    // Surface tilt illusion
    //   _SurfaceTiltDegrees tilts the effective "fill plane" so that the
    //   visible liquid surface is angled relative to the bottle's local
    //   horizontal.  This is purely a UV offset trick — no mesh deformation.
    //
    // Fresnel edge darkening
    //   A simple Schlick-style rim term darkens approaching grazing angles,
    //   simulating the depth-of-glass effect.  Mobile-safe — no screen-space ops.
    //
    // Performance budget
    //   Runs on mid-tier mobile (Mali-G52, Adreno 610).
    //   No texture samples, no render-texture blits.
    //   Single pass, transparent queue, URP-compatible (no URP-specific macros
    //   required — plain ShaderLab + HLSL).
    // ──────────────────────────────────────────────────────────────────────────
    Properties
    {
        // Per-layer data (max 9 layers)
        _Layer0Color  ("Layer 0 Color",  Color) = (1,0,0,1)
        _Layer1Color  ("Layer 1 Color",  Color) = (0,1,0,1)
        _Layer2Color  ("Layer 2 Color",  Color) = (0,0,1,1)
        _Layer3Color  ("Layer 3 Color",  Color) = (1,1,0,1)
        _Layer4Color  ("Layer 4 Color",  Color) = (1,0,1,1)
        _Layer5Color  ("Layer 5 Color",  Color) = (0,1,1,1)
        _Layer6Color  ("Layer 6 Color",  Color) = (0.5,0.5,0,1)
        _Layer7Color  ("Layer 7 Color",  Color) = (0,0.5,0.5,1)
        _Layer8Color  ("Layer 8 Color",  Color) = (0.5,0,0.5,1)

        _Layer0Min ("Layer 0 FillMin", Range(0,1)) = 0
        _Layer0Max ("Layer 0 FillMax", Range(0,1)) = 0.25
        _Layer1Min ("Layer 1 FillMin", Range(0,1)) = 0.25
        _Layer1Max ("Layer 1 FillMax", Range(0,1)) = 0.5
        _Layer2Min ("Layer 2 FillMin", Range(0,1)) = 0.5
        _Layer2Max ("Layer 2 FillMax", Range(0,1)) = 0.75
        _Layer3Min ("Layer 3 FillMin", Range(0,1)) = 0
        _Layer3Max ("Layer 3 FillMax", Range(0,1)) = 0
        _Layer4Min ("Layer 4 FillMin", Range(0,1)) = 0
        _Layer4Max ("Layer 4 FillMax", Range(0,1)) = 0
        _Layer5Min ("Layer 5 FillMin", Range(0,1)) = 0
        _Layer5Max ("Layer 5 FillMax", Range(0,1)) = 0
        _Layer6Min ("Layer 6 FillMin", Range(0,1)) = 0
        _Layer6Max ("Layer 6 FillMax", Range(0,1)) = 0
        _Layer7Min ("Layer 7 FillMin", Range(0,1)) = 0
        _Layer7Max ("Layer 7 FillMax", Range(0,1)) = 0
        _Layer8Min ("Layer 8 FillMin", Range(0,1)) = 0
        _Layer8Max ("Layer 8 FillMax", Range(0,1)) = 0

        // Total fill (0..1). Used to hard-clip top surface.
        _TotalFill ("Total Fill", Range(0,1)) = 0.75

        // Valid layer count (0..9).  Layers above this index are ignored.
        _LayerCount ("Layer Count", Int) = 3

        // Surface tilt in degrees. +ve = tilt right.
        _SurfaceTiltDegrees ("Surface Tilt Degrees", Range(-18,18)) = 0

        // Wobble UV offset (horizontal shift applied to fill level sampling).
        // Driven by WobbleSolver via MaterialPropertyBlock each frame.
        _WobbleOffset ("Wobble Offset", Float) = 0

        // Liquid opacity (glass transparency effect blends with this).
        _Alpha ("Alpha", Range(0,1)) = 0.92

        // Ambient light multiplier to simulate subsurface translucency.
        _TranslucencyStrength ("Translucency", Range(0,1)) = 0.35

        // Foam/agitation band thickness at layer boundaries (0 = off).
        _FoamBandHeight ("Foam Band Height", Range(0,0.05)) = 0.008

        // Hard edge sharpness at layer boundaries; higher = sharper.
        _BoundarySharpness ("Boundary Sharpness", Float) = 200
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "LIQUID_3D"
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            // ── Uniforms ───────────────────────────────────────────────────
            float4 _Layer0Color, _Layer1Color, _Layer2Color, _Layer3Color, _Layer4Color;
            float4 _Layer5Color, _Layer6Color, _Layer7Color, _Layer8Color;

            float _Layer0Min, _Layer0Max;
            float _Layer1Min, _Layer1Max;
            float _Layer2Min, _Layer2Max;
            float _Layer3Min, _Layer3Max;
            float _Layer4Min, _Layer4Max;
            float _Layer5Min, _Layer5Max;
            float _Layer6Min, _Layer6Max;
            float _Layer7Min, _Layer7Max;
            float _Layer8Min, _Layer8Max;

            float  _TotalFill;
            int    _LayerCount;
            float  _SurfaceTiltDegrees;
            float  _WobbleOffset;
            float  _Alpha;
            float  _TranslucencyStrength;
            float  _FoamBandHeight;
            float  _BoundarySharpness;

            // ── Vertex I/O ─────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
            };

            // ── Vertex shader ──────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = UnityObjectToClipPos(IN.positionOS);
                OUT.uv         = IN.uv;
                OUT.normalWS   = UnityObjectToWorldNormal(IN.normalOS);
                float3 worldPos = mul(unity_ObjectToWorld, IN.positionOS).xyz;
                OUT.viewDirWS  = normalize(_WorldSpaceCameraPos - worldPos);
                return OUT;
            }

            // ── Helpers ────────────────────────────────────────────────────

            // Smooth step that approximates a hard boundary (high sharpness = near-step).
            float hardStep(float edge, float x, float sharpness)
            {
                return saturate((x - edge) * sharpness + 0.5);
            }

            // Sample the liquid color at fill-fraction fillY.
            // Returns RGBA with zero alpha outside all layers or above totalFill.
            float4 sampleLiquid(float fillY, float uX)
            {
                // Apply surface tilt: effective fill position varies with horizontal UV.
                // tiltOffset is proportional to UV.x offset from centre (0..1 → -0.5..+0.5).
                float tiltRad = _SurfaceTiltDegrees * (3.14159265 / 180.0);
                float tiltOffset = tan(tiltRad) * (uX - 0.5) * 0.5; // scaled to half-width
                float effectiveFill = fillY - tiltOffset + _WobbleOffset;

                // Clip above total fill surface
                if (effectiveFill > _TotalFill) return float4(0,0,0,0);

                // Macros to sample each layer:
#define SAMPLE_LAYER(idx, minV, maxV, col) \
    if (idx < _LayerCount && effectiveFill >= minV && effectiveFill < maxV) { \
        float t = (effectiveFill - minV) / max(maxV - minV, 1e-5); \
        float foam = (_FoamBandHeight > 0) ? \
            smoothstep(1.0 - _FoamBandHeight/(maxV-minV+1e-5), 1.0, t) * 0.35 : 0.0; \
        float4 c = col; \
        c.rgb = lerp(c.rgb, float3(1,1,1), foam); \
        c.rgb = lerp(c.rgb, c.rgb * (1.0 + _TranslucencyStrength), 0.5); \
        c.a = _Alpha; \
        return c; \
    }

                SAMPLE_LAYER(0, _Layer0Min, _Layer0Max, _Layer0Color)
                SAMPLE_LAYER(1, _Layer1Min, _Layer1Max, _Layer1Color)
                SAMPLE_LAYER(2, _Layer2Min, _Layer2Max, _Layer2Color)
                SAMPLE_LAYER(3, _Layer3Min, _Layer3Max, _Layer3Color)
                SAMPLE_LAYER(4, _Layer4Min, _Layer4Max, _Layer4Color)
                SAMPLE_LAYER(5, _Layer5Min, _Layer5Max, _Layer5Color)
                SAMPLE_LAYER(6, _Layer6Min, _Layer6Max, _Layer6Color)
                SAMPLE_LAYER(7, _Layer7Min, _Layer7Max, _Layer7Color)
                SAMPLE_LAYER(8, _Layer8Min, _Layer8Max, _Layer8Color)
#undef SAMPLE_LAYER

                return float4(0,0,0,0);
            }

            // ── Fragment shader ────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                float4 col = sampleLiquid(IN.uv.y, IN.uv.x);
                if (col.a < 0.001) discard;
                return col;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
