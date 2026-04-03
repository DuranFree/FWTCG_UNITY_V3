Shader "FWTCG/GlassPanel"
{
    // Simulated frosted-glass panel for UGUI CanvasGroup overlays.
    // ScreenSpaceOverlay canvas cannot sample _CameraOpaqueTexture, so we use
    // procedural noise to fake the frosted texture, plus a pulsing border.
    // DEV-25.
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}  // required by UGUI canvas system
        _TintColor      ("Tint Color",         Color)           = (0.04, 0.08, 0.18, 0.82)
        _NoiseScale     ("Frost Scale",         Range(20, 200))  = 80
        _NoiseStr       ("Frost Strength",      Range(0, 0.12))  = 0.04
        _BorderColor    ("Border Color",        Color)           = (0.04, 0.78, 0.73, 0.85)
        _BorderWidth    ("Border Width",        Range(0.004, 0.05)) = 0.018
        _HighlightAlpha ("Top Highlight Alpha", Range(0, 0.4))   = 0.18
        _HighlightH     ("Top Highlight Height",Range(0.02, 0.3)) = 0.10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;  // declared to satisfy UGUI; not sampled
            float4 _TintColor;
            float  _NoiseScale, _NoiseStr;
            float4 _BorderColor;
            float  _BorderWidth;
            float  _HighlightAlpha, _HighlightH;

            // Fast value noise (identical hash as HexGrid.shader for consistency)
            float hash(float2 p)
            {
                p  = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float vnoise(float2 uv, float s)
            {
                float2 p = uv * s;
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);   // smoothstep
                return lerp(
                    lerp(hash(i),              hash(i + float2(1,0)), f.x),
                    lerp(hash(i + float2(0,1)),hash(i + float2(1,1)), f.x),
                    f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.color  = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv  = i.uv;
                float  asp = _ScreenParams.x / _ScreenParams.y;
                float2 auv = float2(uv.x * asp, uv.y);

                // Two-octave frost noise
                float n = vnoise(auv,       _NoiseScale)
                        + vnoise(auv * 1.7 + 0.4, _NoiseScale * 0.6);
                n = n * 0.5 - 0.5;  // center around 0

                // Glass tint + frost
                fixed4 col  = _TintColor;
                col.rgb    += n * _NoiseStr;
                col.rgb     = saturate(col.rgb);

                // Top highlight stripe (mimics glass catching ambient light)
                float hMask = smoothstep(_HighlightH, 0.0, 1.0 - uv.y);
                col.rgb     = lerp(col.rgb, float3(1,1,1), _HighlightAlpha * hMask);

                // Subtle bottom vignette for depth
                float vg    = smoothstep(0.0, 0.15, uv.y);
                col.rgb    *= lerp(0.75, 1.0, vg);

                // Border with slow pulse
                float bw  = _BorderWidth;
                bool  isBorder = (uv.x < bw || uv.x > 1.0 - bw ||
                                  uv.y < bw || uv.y > 1.0 - bw);
                if (isBorder)
                {
                    float pulse = sin(_Time.y * 1.2) * 0.07 + 0.93;
                    col.rgb     = lerp(col.rgb, _BorderColor.rgb * pulse, _BorderColor.a);
                    col.a       = max(col.a, _BorderColor.a * 0.9);
                }

                return col * i.color;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
