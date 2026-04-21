// UI-compatible dissolve shader (URP project, UGUI canvas).
// Built-in value-noise fBm (no external noise texture dependency).
// Properties: _DissolveAmount 0..1 advances the burn; _EdgeColor + _EdgeGlow define the hot fringe.
Shader "FWTCG/UIDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _DissolveAmount ("Dissolve Amount", Range(0, 1.2)) = 0
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.08
        _EdgeColor ("Edge Color", Color) = (1, 0.85, 0.35, 1)
        _EdgeGlow ("Edge Glow Intensity", Range(0, 8)) = 3.5
        _NoiseScale ("Noise Scale", Range(0.5, 8)) = 2.5
        // xy = direction (normalized), z = directional blend weight (0 = pure noise, 1 = pure linear)
        _DissolveDirection ("Dissolve Direction", Vector) = (0, 1, 0.35, 0)
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

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _DissolveAmount;
            float _EdgeWidth;
            fixed4 _EdgeColor;
            float _EdgeGlow;
            float _NoiseScale;
            float4 _DissolveDirection;

            float hash21(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 uv)
            {
                float v = 0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += valueNoise(uv) * amp;
                    uv *= 2.03;
                    amp *= 0.5;
                }
                return v;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPos = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // fBm noise value [0,1]
                float n = fbm(IN.texcoord * _NoiseScale);

                // Directional linear value along _DissolveDirection.xy (e.g. (0,1) = bottom→top sweep)
                float2 dir = _DissolveDirection.xy;
                float linearVal = saturate(dot(IN.texcoord, dir));

                // Blend noise and directional map by _DissolveDirection.z
                float w = saturate(_DissolveDirection.z);
                float dissolveMap = saturate(n * (1.0 - w) + linearVal * w);

                float cutoff = _DissolveAmount;
                float edgeEnd = cutoff + _EdgeWidth;

                if (dissolveMap < cutoff)
                {
                    color.a = 0;
                }
                else if (dissolveMap < edgeEnd)
                {
                    // Hot burning edge — blend toward glowing edge color
                    float t = 1.0 - (dissolveMap - cutoff) / max(_EdgeWidth, 0.0001);
                    color.rgb = lerp(color.rgb, _EdgeColor.rgb * _EdgeGlow, t);
                    color.a = max(color.a, _EdgeColor.a * t);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPos.xy, _ClipRect);
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
