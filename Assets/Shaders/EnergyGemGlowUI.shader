// FWTCG/EnergyGemGlowUI  v1 — Additive glow version of EnergyGemUI
// 亮度模式：提取纹理亮度保留石头/宝石结构，_HdrColor 决定色相
// vertex color（COLOR）只传 alpha，不传颜色 → 避免 Color32 HDR 截断
// v7: _LumFloor 由 gemMask 门控 → 石头边框不会在 MegaBurst 时发光
// v8: color.a 也乘 gemMask → 石头/边框区域完全透明，不染色，底层 RingBase 自然显示

Shader "FWTCG/EnergyGemGlowUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [HDR] _HdrColor      ("HDR Color",       Color) = (1,1,1,1)
        _LumMultiplier       ("Lum Multiplier",   Float) = 1.5
        _LumFloor            ("Lum Floor",        Float) = 0.12

        _DistortSpeed        ("Distort Speed",    Float) = 0.25
        _DistortStrength     ("Distort Strength", Float) = 0.035
        _DistortScale        ("Distort Scale",    Float) = 4.0
        _MaskLow             ("Mask Low",         Float) = 0.12
        _MaskHigh            ("Mask High",        Float) = 0.50
        _NoiseOffset         ("Noise Offset",     Vector) = (0,0,0,0)
        // 径向外边界裁切：UV 中心距离 > _OuterClipR 时平滑淡出（0 = 不裁切）
        _OuterClipR          ("Outer Clip Radius",Float) = 0.0
        _OuterClipFeather    ("Outer Clip Feather",Float) = 0.02

        _StencilComp         ("Stencil Comparison", Float) = 8
        _Stencil             ("Stencil ID",         Float) = 0
        _StencilOp           ("Stencil Operation",  Float) = 0
        _StencilWriteMask    ("Stencil Write Mask", Float) = 255
        _StencilReadMask     ("Stencil Read Mask",  Float) = 255
        _ColorMask           ("Color Mask",         Float) = 15
        [Toggle(UNITY_UI_CLIP_RECT)]  _UseClipRect  ("Use Clip Rect",  Float) = 1
        [Toggle(UNITY_UI_ALPHACLIP)]  _UseAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True"
               "RenderType"="Transparent" "PreviewType"="Plane"
               "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp]
            ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]
        }

        Cull Off  Lighting Off  ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One One      // Additive — glow only adds light, stone frame masked by gemMask
        ColorMask [_ColorMask]

        Pass
        {
            Name "EnergyGem"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
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
                fixed4 color    : COLOR;        // 只传 alpha，不传颜色
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4    _Color;
            fixed4    _TextureSampleAdd;
            float4    _ClipRect;
            float4    _MainTex_ST;

            float4    _HdrColor;        // HDR palette color（material property，不走 Color32）
            float     _LumMultiplier;
            float     _LumFloor;

            float     _DistortSpeed;
            float     _DistortStrength;
            float     _DistortScale;
            float     _MaskLow;
            float     _MaskHigh;
            float4    _NoiseOffset;
            float     _OuterClipR;
            float     _OuterClipFeather;

            float _Hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }
            float _Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(_Hash(i),               _Hash(i + float2(1,0)), u.x),
                    lerp(_Hash(i + float2(0,1)), _Hash(i + float2(1,1)), u.x),
                    u.y) * 2.0 - 1.0;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.worldPos = v.vertex;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color    = v.color * _Color;  // alpha 用于 UI clip，rgb 不重要
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // Gem mask（石头暗区不扰动）
                fixed4 origSample = tex2D(_MainTex, uv);
                float  origLum    = dot(origSample.rgb, float3(0.299, 0.587, 0.114));
                float  gemMask    = smoothstep(_MaskLow, _MaskHigh, origLum);

                // UV 扰动（_NoiseOffset 给每颗宝石独立随机相位，打破一致性）
                float  t = _Time.y * _DistortSpeed;
                float2 no = _NoiseOffset.xy;
                float2 d;
                d.x = _Noise(uv * _DistortScale + no + float2( t * 0.65,  t * 0.32));
                d.y = _Noise(uv * _DistortScale + no + float2(-t * 0.38,  t * 0.57) + float2(3.71, 1.53));
                float2 distUV = uv + d * _DistortStrength * gemMask;

                // 采样扰动后纹理
                fixed4 tex = tex2D(_MainTex, distUV) + _TextureSampleAdd;

                // 亮度模式：用纹理亮度保留石头/宝石结构，_HdrColor 决定色相
                // _LumFloor 仅作用于宝石亮区（gemMask），石头边框不参与发光
                // alpha 也由 gemMask 门控：石头/边框区域完全透明，只有宝石晶体区域着色
                float texLum = dot(tex.rgb, float3(0.299, 0.587, 0.114));

                // v9: 第二层低频噪声 → 宝石内部动态暗纹（"黑色扰动"）
                // 与 UV 扰动方向/速度不同，产生独立的流动感
                float vein    = _Noise(uv * _DistortScale * 1.3 + no
                                + float2(-t * 0.31, t * 0.46) + float2(5.13, 2.77));
                // vein ∈ [-1,1] → 映射到 [0.45, 1.0]，仅在 gemMask 区域生效
                float veinFac = lerp(1.0, saturate(vein * 0.5 + 0.75), gemMask);

                float modLum = texLum * _LumMultiplier * veinFac + _LumFloor * gemMask;
                fixed4 color;
                color.rgb = _HdrColor.rgb * modLum;
                color.a   = tex.a * i.color.a * gemMask;

                // 径向外边界裁切：UV 中心距离超过 _OuterClipR 时平滑淡出
                // 精灵 UV (0,0)-(1,1)，中心 (0.5,0.5)，外圈实测约 0.449
                if (_OuterClipR > 0.0)
                {
                    float uvDist  = length(uv - float2(0.5, 0.5));
                    float clipMask = smoothstep(_OuterClipR,
                                                _OuterClipR - _OuterClipFeather, uvDist);
                    color.rgb *= clipMask;
                    color.a   *= clipMask;
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
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
