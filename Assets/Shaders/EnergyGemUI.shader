// FWTCG/EnergyGemUI  v5
// 修复：
//   1. 使用 _HdrColor（shader property，不走 Color32 截断）传 HDR 颜色
//   2. 亮度模式着色：提取纹理亮度保留石头/宝石结构，用 _HdrColor 上色
//      → 不受原始纹理颜色影响，任何调色板颜色都能正确渲染
//   3. UV 扰动仍限制在宝石亮区，石头暗区结构不变

Shader "FWTCG/EnergyGemUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // ── HDR 调色板颜色（不走 Color32，支持 >1 触发 Bloom）────────
        [HDR] _HdrColor ("HDR Color", Color) = (1,1,1,1)
        // 亮度缩放：dim 模式可降低整体亮度
        _LumMultiplier ("Lum Multiplier", Float) = 1.5
        // 亮度地板：石头暗区最低可见度（0=全黑，0.1=微弱结构可见）
        _LumFloor ("Lum Floor", Float) = 0.12

        // 能量流动参数
        _DistortSpeed    ("Distort Speed",     Float) = 0.25
        _DistortStrength ("Distort Strength",  Float) = 0.035
        _DistortScale    ("Distort Scale",     Float) = 4.0
        _MaskLow         ("Mask Low  (stone)", Float) = 0.12
        _MaskHigh        ("Mask High (gem)",   Float) = 0.50

        // Unity UI 必需
        _StencilComp     ("Stencil Comparison", Float) = 8
        _Stencil         ("Stencil ID",         Float) = 0
        _StencilOp       ("Stencil Operation",  Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask",  Float) = 255
        _ColorMask       ("Color Mask",         Float) = 15
        [Toggle(UNITY_UI_CLIP_RECT)]  _UseClipRect  ("Use Clip Rect",  Float) = 1
        [Toggle(UNITY_UI_ALPHACLIP)]  _UseAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]  Comp [_StencilComp]  Pass [_StencilOp]
            ReadMask [_StencilReadMask]  WriteMask [_StencilWriteMask]
        }

        Cull Off  Lighting Off  ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
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
                float4 vertex    : SV_POSITION;
                float4 color     : TEXCOORD2;   // TEXCOORD 避免 COLOR 插值截断到 [0,1]
                float2 texcoord  : TEXCOORD0;
                float4 worldPos  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _Color;
            float4    _TextureSampleAdd;
            float4    _ClipRect;
            float4    _MainTex_ST;

            // HDR 调色板颜色（shader property，不受 Color32 截断）
            float4    _HdrColor;
            float     _LumMultiplier;
            float     _LumFloor;

            float     _DistortSpeed;
            float     _DistortStrength;
            float     _DistortScale;
            float     _MaskLow;
            float     _MaskHigh;

            // ── Value Noise ──────────────────────────────────────────────
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
                // 只传 alpha（用于 UI clip），颜色由 _HdrColor 提供
                o.color    = float4(1.0, 1.0, 1.0, v.color.a * _Color.a);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // ── Step 1: 采样原始 UV 计算宝石掩码 ───────────────────
                fixed4 origSample = tex2D(_MainTex, uv);
                float origLum  = dot(origSample.rgb, float3(0.299, 0.587, 0.114));
                float gemMask  = smoothstep(_MaskLow, _MaskHigh, origLum);

                // ── Step 2: UV 扰动（只在宝石亮区）────────────────────
                float  t = _Time.y * _DistortSpeed;
                float2 d;
                d.x = _Noise(uv * _DistortScale + float2( t * 0.65,  t * 0.32));
                d.y = _Noise(uv * _DistortScale + float2(-t * 0.38,  t * 0.57) + float2(3.71, 1.53));
                float2 distortedUV = uv + d * _DistortStrength * gemMask;

                // ── Step 3: 采样扰动后的纹理 ──────────────────────────
                fixed4 texSample = tex2D(_MainTex, distortedUV) + _TextureSampleAdd;

                // ── Step 4: 亮度模式上色（核心修复）──────────────────
                // 提取纹理亮度：保留石头/宝石结构，剥离原始纹理颜色
                float texLum = dot(texSample.rgb, float3(0.299, 0.587, 0.114));
                // 亮度调制：stone 暗区有最低可见度，gem 亮区得到 _HdrColor 全量
                float modLum = texLum * _LumMultiplier + _LumFloor;
                // 最终颜色 = HDR 调色板颜色 × 亮度结构（supports Bloom via HDR values）
                float3 rgb = _HdrColor.rgb * modLum;
                // alpha 来自纹理 + vertex alpha（用于 UI clip rect）
                float  a   = texSample.a * i.color.a;

                fixed4 color = fixed4(rgb, a);

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
