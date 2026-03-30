Shader "UI/HexGrid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BgColor ("Background Color", Color) = (0.004, 0.04, 0.075, 1)
        _GridColor ("Grid Color", Color) = (0.04, 0.78, 0.73, 0.08)
        _GridScale ("Grid Scale", Float) = 40
        _GridThickness ("Grid Line Thickness", Range(0.01, 0.1)) = 0.04
        _NoiseIntensity ("Noise Intensity", Range(0, 0.1)) = 0.02
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.3
        _VignetteColor ("Vignette Tint", Color) = (0.04, 0.78, 0.73, 0.05)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _BgColor;
            fixed4 _GridColor;
            half _GridScale;
            half _GridThickness;
            half _NoiseIntensity;
            half _VignetteIntensity;
            fixed4 _VignetteColor;
            float4 _ClipRect;
            float4 _MainTex_ST;

            // Simple hash for procedural noise
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Hexagonal grid distance (returns distance to nearest hex edge)
            float hexDist(float2 p)
            {
                p = abs(p);
                float c = dot(p, normalize(float2(1.0, 1.73)));
                c = max(c, p.x);
                return c;
            }

            float4 hexCoords(float2 uv)
            {
                float2 r = float2(1.0, 1.73);
                float2 h = r * 0.5;
                float2 a = fmod(uv, r) - h;
                float2 b = fmod(uv - h, r) - h;

                float2 gv;
                if (dot(a, a) < dot(b, b))
                    gv = a;
                else
                    gv = b;

                float edge = hexDist(gv);
                return float4(gv, edge, 0);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Base background color
                fixed4 color = _BgColor;

                // Hex grid
                float2 hexUV = uv * _GridScale;
                float4 hc = hexCoords(hexUV);
                float edgeDist = hc.z;

                // Hex edge glow (thin lines at hex boundaries)
                float hexLine = 1.0 - smoothstep(0.45 - _GridThickness, 0.45, edgeDist);
                color.rgb += _GridColor.rgb * hexLine * _GridColor.a;

                // Procedural noise
                float noise = hash(uv * 200.0 + _Time.y * 0.1);
                color.rgb += (noise - 0.5) * _NoiseIntensity;

                // Vignette (darker at edges, slight teal tint at center)
                float2 centerDist = uv - 0.5;
                float vignette = dot(centerDist, centerDist);
                color.rgb *= 1.0 - vignette * _VignetteIntensity * 2.0;
                // Subtle teal center glow
                float centerGlow = saturate(1.0 - vignette * 4.0);
                color.rgb += _VignetteColor.rgb * centerGlow * _VignetteColor.a;

                color.a = 1.0;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
