/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/
Shader "Decantra/Liquid3D"
{
    // Clean layered liquid shader for 3D bottles.
    // Rendering rules:
    //   - Layer selection depends on fill height only.
    //   - Liquid color within a layer is vertically invariant.
    //   - Final shading depends only on horizontal position inside the bottle.
    Properties
    {
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

        _TotalFill ("Total Fill", Range(0,1)) = 0.75
        _LayerCount ("Layer Count", Int) = 3
        _SurfaceTiltDegrees ("Surface Tilt Degrees", Range(-18,18)) = 0
        _SurfaceArcHeight ("Surface Arc Height", Range(0,0.03)) = 0.012
        _WobbleOffset ("Wobble Offset", Float) = 0
        _Alpha ("Alpha", Range(0,1)) = 0.99
        _Agitation ("Agitation", Range(0,1)) = 0

        // Horizontal-only cylindrical shading, tuned to match the cork readability model.
        _CylPower ("Cyl Power", Range(0.5, 4.0)) = 1.9
        _CylEdgeBrightness ("Cyl Edge Brightness", Range(0.5, 1.0)) = 0.72
        _CylCenterBrightness ("Cyl Center Brightness", Range(1.0, 1.25)) = 1.18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "LIQUID_3D"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

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

            float _TotalFill;
            int _LayerCount;
            float _SurfaceTiltDegrees;
            float _SurfaceArcHeight;
            float _WobbleOffset;
            float _Alpha;
            float _Agitation;
            float _CylPower;
            float _CylEdgeBrightness;
            float _CylCenterBrightness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = UnityObjectToClipPos(IN.positionOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            float ComputeEffectiveFill(float fillY, float uX)
            {
                float tiltRad = _SurfaceTiltDegrees * (3.14159265 / 180.0);
                float tiltOffset = tan(tiltRad) * (uX - 0.5) * 0.5;
                return fillY - tiltOffset + _WobbleOffset;
            }

            float ComputeBoundaryArcOffset(float uX)
            {
                float edgeDistance = abs(uX - 0.5) * 2.0;
                return -_SurfaceArcHeight * edgeDistance * edgeDistance;
            }

            float3 RgbToHsv(float3 c)
            {
                float4 K = float4(0.0, -0.3333333, 0.6666667, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-5;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 0.6666667, 0.3333333, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float3 ApplyBrightnessPreservingChroma(float3 rgb, float brightness)
            {
                float3 hsv = RgbToHsv(rgb);
                hsv.z = saturate(hsv.z * brightness);
                return HsvToRgb(hsv);
            }

            float4 SampleLiquid(float fillY, float uX)
            {
                float effectiveFill = ComputeEffectiveFill(fillY, uX);
                float boundaryArcOffset = ComputeBoundaryArcOffset(uX);

#define SAMPLE_LAYER(idx, minV, maxV, col) \
    if (idx < _LayerCount) { \
        float layerMin = minV + boundaryArcOffset; \
        float layerMax = min(maxV, _TotalFill) + boundaryArcOffset; \
        if (layerMax > layerMin && effectiveFill >= layerMin && effectiveFill < layerMax) { \
        float4 c = col; \
        c.a = _Alpha; \
        return c; \
        } \
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

                return float4(0, 0, 0, 0);
            }

            float ComputeHorizontalShading(float uX)
            {
                float centered = saturate(1.0 - abs(uX - 0.5) * 2.0);
                float cylindrical = pow(centered, _CylPower);
                return lerp(_CylEdgeBrightness, _CylCenterBrightness, cylindrical);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 col = SampleLiquid(IN.uv.y, IN.uv.x);
                if (col.a < 0.001)
                {
                    discard;
                }

                col.rgb = ApplyBrightnessPreservingChroma(col.rgb, ComputeHorizontalShading(IN.uv.x));
                return col;
            }

            ENDHLSL
        }
    }

    FallBack Off
}