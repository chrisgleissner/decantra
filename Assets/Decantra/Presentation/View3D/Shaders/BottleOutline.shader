/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/
Shader "Decantra/BottleOutline"
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inverted-hull glow outline — renders AFTER the glass (queue Transparent+3).
    //
    // Technique: Cull Front, vertices extruded along normals.  The extruded
    // back-face shell is slightly larger than the bottle, so only the ring of
    // pixels outside the original silhouette is actually new — the glass (at
    // queue Transparent+1) has already painted the bottle interior.
    //
    // ZTest Always so the extruded rim draws even when no depth is written
    // (glass has ZWrite Off). The fragment shader fades the shell to nearly
    // transparent when the surface faces the camera, leaving only a soft edge
    // halo so the liquid colours remain readable.
    //
    // Bottle3DView controls _GlowColor via MaterialPropertyBlock:
    //   - Normal bottles:  bright cool white for readability on dark backgrounds
    //   - Sink bottles:    near-black outline to distinguish sink-only state
    //   - Pour-ready:      faint edge glow, slightly wider than default
    // ──────────────────────────────────────────────────────────────────────────
    Properties
    {
        _GlowColor   ("Glow Color",   Color)             = (0.85, 0.85, 0.85, 0.7)
        _OutlineWidth ("Outline Width", Range(0.01, 0.1)) = 0.035
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+3"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _GlowColor;
            float  _OutlineWidth;

            static const float EdgeGlowPower = 3.2;
            static const float EdgeGlowFloor = 0.02;

            struct Attributes
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos    = mul(unity_ObjectToWorld, IN.posOS).xyz;
                float3 worldNormal = normalize(UnityObjectToWorldNormal(IN.normalOS));
                worldPos += worldNormal * _OutlineWidth;
                OUT.posCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                OUT.normalWS = worldNormal;
                OUT.worldPos = worldPos;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - IN.worldPos);
                float edge = pow(saturate(1.0 - abs(dot(normalize(IN.normalWS), viewDirWS))), EdgeGlowPower);
                float alpha = _GlowColor.a * max(edge, EdgeGlowFloor);
                return float4(_GlowColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
