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
    //
    // Performance: single directional light, no real-time shadows, no GI sampling.
    // Mobile-safe. Works on Adreno 610 / Mali-G52.
    // ──────────────────────────────────────────────────────────────────────────
    Properties
    {
        _GlassTint      ("Glass Tint",     Color  ) = (0.85, 0.92, 1.0, 0.18)
        _FresnelPower   ("Fresnel Power",  Range(1,8)) = 4.5
        _FresnelColor   ("Fresnel Color",  Color  ) = (1,1,1,0.6)
        _SpecColor2     ("Specular Color", Color  ) = (1,1,1,1)
        _Shininess      ("Shininess",      Range(16,256)) = 128
        _Smoothness     ("Smoothness",     Range(0,1)) = 0.97
        _RefractionStrength("Refraction Strength", Range(0,0.08)) = 0.02
        _AbsorptionStrength("Absorption Strength", Range(0,4)) = 1.1
        _MicroNormalScale("Micro Normal Scale", Range(0,0.2)) = 0.06
        _ReflectionStrip("Reflection Strip Strength", Range(0,1)) = 0.18
        _ReflectionX    ("Reflection Strip X", Range(-1,1)) = 0.55
        _ReflectionWidth("Reflection Strip Width", Range(0.01,0.5)) = 0.08
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

            struct Attributes { float4 posOS : POSITION; };
            struct Varyings   { float4 posCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = UnityObjectToClipPos(IN.posOS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Inner surface is slightly darker / more saturated
                return float4(_GlassTint.rgb * 0.75, _GlassTint.a * 0.5);
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
                float3 specCol = _SpecColor2.rgb * spec * _SpecColor2.a;

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
                float  alpha = _GlassTint.a + fresnel * _FresnelColor.a * 0.5;
                alpha = saturate(alpha);

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
