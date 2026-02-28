Shader "Decantra/TutorialSpotlightMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,0.64)
        _HoleCenter ("Hole Center", Vector) = (0.5,0.5,0,0)
        _HoleSize ("Hole Size", Vector) = (0.25,0.16,0,0)
        _CornerRadius ("Corner Radius", Range(0.001,0.5)) = 0.08
        _Feather ("Feather", Range(0.001,0.3)) = 0.04
        _UseCircle ("Use Circle", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite Off
        ZTest Always
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
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _HoleCenter;
            float4 _HoleSize;
            float _CornerRadius;
            float _Feather;
            float _UseCircle;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float sdRoundRect(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 center = _HoleCenter.xy;
                float2 holeSize = max(_HoleSize.xy, float2(0.001, 0.001));
                float feather = max(_Feather, 0.001);
                float useCircle = step(0.5, _UseCircle);

                float2 local = uv - center;

                float circleDistance = length(local) - max(holeSize.x, holeSize.y) * 0.5;
                float roundRectDistance = sdRoundRect(local, holeSize * 0.5, min(_CornerRadius, min(holeSize.x, holeSize.y) * 0.5));
                float distance = lerp(roundRectDistance, circleDistance, useCircle);

                float alpha = smoothstep(0.0, feather, distance);
                fixed4 tex = tex2D(_MainTex, uv);
                fixed4 color = _Color;
                color.a *= alpha * tex.a;
                return color;
            }
            ENDCG
        }
    }
}
