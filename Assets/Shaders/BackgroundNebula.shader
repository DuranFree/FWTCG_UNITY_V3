Shader "UI/BackgroundNebula"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RotationSpeed ("Rotation Speed (rad/sec)", Float) = 0.05
        _DistortStrength ("Distortion Strength (uv)", Float) = 0.004
        _DistortFreq ("Distortion Angular Frequency", Float) = 5.0
        _DistortSpeed ("Distortion Time Speed", Float) = 0.6
        _NebulaCenter ("Nebula Center (uv)", Vector) = (0.5, 0.5, 0, 0)
        _NebulaInner ("Nebula Inner Radius (uv, aspect-corrected)", Float) = 0.18
        _NebulaOuter ("Nebula Outer Radius (uv, aspect-corrected)", Float) = 0.42
        _EdgeSoft ("Edge Softness", Float) = 0.06
        _Aspect ("Display Aspect (w/h)", Float) = 1.7777778

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _RotationSpeed;
            float _DistortStrength;
            float _DistortFreq;
            float _DistortSpeed;
            float4 _NebulaCenter;
            float _NebulaInner;
            float _NebulaOuter;
            float _EdgeSoft;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 center = _NebulaCenter.xy;

                // Convert offset to aspect-corrected (square) space so rotation/mask are circular on screen
                float2 d = uv - center;
                float2 dC = float2(d.x * _Aspect, d.y);
                float r = length(dC);

                // Static (background) sample
                fixed4 staticCol = tex2D(_MainTex, uv);

                // Rotate offset around center
                float angle = _Time.y * _RotationSpeed;
                float ca = cos(angle);
                float sa = sin(angle);
                float2 rotDC = float2(dC.x * ca - dC.y * sa, dC.x * sa + dC.y * ca);

                // Two-octave distortion: radial wobble + tangential swirl
                float theta = atan2(dC.y, dC.x);
                float t = _Time.y * _DistortSpeed;
                float wobbleR = sin(theta * _DistortFreq + t) * 0.6
                              + sin(theta * (_DistortFreq * 2.3) - t * 1.7 + r * 18.0) * 0.4;
                float wobbleT = cos(theta * (_DistortFreq * 0.7) + t * 0.8 + r * 12.0) * 0.5
                              + sin(r * 30.0 - t * 1.3) * 0.5;
                float2 nrm = (r > 1e-5) ? (dC / r) : float2(0, 0);
                float2 tan2 = float2(-nrm.y, nrm.x);
                rotDC += (nrm * wobbleR + tan2 * wobbleT) * _DistortStrength;

                // Convert back to UV space (undo aspect correction on x)
                float2 rotD = float2(rotDC.x / _Aspect, rotDC.y);
                float2 rotUV = center + rotD;
                fixed4 rotCol = tex2D(_MainTex, rotUV);

                // Radial mask (1 inside [inner, outer], soft edges)
                float maskOuter = 1.0 - smoothstep(_NebulaOuter - _EdgeSoft, _NebulaOuter + _EdgeSoft, r);
                float maskInner = smoothstep(_NebulaInner - _EdgeSoft, _NebulaInner + _EdgeSoft, r);
                float mask = maskOuter * maskInner;

                fixed4 col = lerp(staticCol, rotCol, mask) * i.color * _Color;
                return col;
            }
            ENDCG
        }
    }
}
