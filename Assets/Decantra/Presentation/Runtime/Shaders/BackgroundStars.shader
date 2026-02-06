Shader "Decantra/BackgroundStars"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StarDensity ("Star Density", Range(0.01, 1.0)) = 0.35
        _StarSpeed ("Star Speed", Range(0.01, 1.0)) = 0.40
        _StarBrightness ("Star Brightness", Range(0.05, 1.0)) = 0.50
    }
    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _DecantraStarTime;
            float _StarDensity;
            float _StarSpeed;
            float _StarBrightness;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float2 Hash2(float2 p)
            {
                float2 h;
                h.x = frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
                h.y = frac(sin(dot(p, float2(269.5, 183.3))) * 43758.5453);
                return h;
            }

            float StarLayer(float2 uv, float2 grid, float speed, float density)
            {
                float2 uvScroll = uv;
                float starTime = _Time.y + _DecantraStarTime;
                uvScroll.y = frac(uv.y + starTime * speed);
                float2 cell = floor(uvScroll * grid);
                float2 local = frac(uvScroll * grid);

                float rnd = Hash(cell);
                float starMask = step(1.0 - density, rnd);

                float2 starPos = Hash2(cell + 5.31);
                float2 delta = local - starPos;
                // Increased radius for visibility and motion detection
                float radius = lerp(0.12, 0.26, Hash(cell + 1.73));
                float falloff = smoothstep(radius, 0.0, length(delta));

                float brightnessSeed = Hash(cell + 9.87);
                // Continuous brightness (infinite levels) with a dim-heavy distribution.
                // 0.25 sets the darkest grey; 1.0 keeps rare bright stars; exponent 1.6 biases dim.
                float brightness = lerp(0.25, 1.0, pow(brightnessSeed, 1.6));

                return starMask * falloff * brightness;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Map normalized uniforms to per-layer shader parameters.
                // Density uniform [0.01..1.0] scales base densities [0.07, 0.06, 0.05] around default 0.35.
                // Speed uniform [0.01..1.0] scales base speeds [0.12, 0.21, 0.30] around default 0.40.
                // Brightness uniform [0.05..1.0] scales the final intensity multiplier around default 0.50 → 1.6x.
                float densityScale = _StarDensity / 0.35;
                float speedScale = _StarSpeed / 0.40;
                float brightnessMultiplier = _StarBrightness / 0.50 * 1.6;

                float star1 = StarLayer(uv, float2(90.0, 160.0), 0.12 * speedScale, 0.07 * densityScale);
                float star2 = StarLayer(uv, float2(120.0, 210.0), 0.21 * speedScale, 0.06 * densityScale);
                float star3 = StarLayer(uv, float2(160.0, 280.0), 0.30 * speedScale, 0.05 * densityScale);

                float intensity = saturate(star1 + star2 + star3);

                // Apply configurable brightness.
                intensity = saturate(intensity * brightnessMultiplier);

                return fixed4(intensity, intensity, intensity, intensity) * i.color;
            }
            ENDCG
        }
    }
}
