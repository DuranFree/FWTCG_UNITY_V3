Shader "FWTCG/UIBlur"
{
    // Two-pass Gaussian blur for UI backdrop (Canvas Overlay workaround).
    // Pass 0: horizontal, Pass 1: vertical. Used by CardDetailPopup.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 10)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZTest Always Cull Off ZWrite Off

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4    _MainTex_TexelSize;
        float     _BlurSize;

        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv  = v.texcoord;
            return o;
        }

        // 9-tap linear-sampled Gaussian (σ ≈ 4, smoother than 5-tap, avoids banding)
        fixed4 blurDir(float2 uv, float2 dir)
        {
            float2 t = _MainTex_TexelSize.xy * dir * _BlurSize;
            fixed4 c  = tex2D(_MainTex, uv) * 0.198596;
            c += tex2D(_MainTex, uv + t * 1.411764706) * 0.175713;
            c += tex2D(_MainTex, uv - t * 1.411764706) * 0.175713;
            c += tex2D(_MainTex, uv + t * 3.294117647) * 0.121095;
            c += tex2D(_MainTex, uv - t * 3.294117647) * 0.121095;
            c += tex2D(_MainTex, uv + t * 5.176470588) * 0.065014;
            c += tex2D(_MainTex, uv - t * 5.176470588) * 0.065014;
            c += tex2D(_MainTex, uv + t * 7.058823529) * 0.027006;
            c += tex2D(_MainTex, uv - t * 7.058823529) * 0.027006;
            return c;
        }
        ENDCG

        Pass // 0: horizontal
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target { return blurDir(i.uv, float2(1, 0)); }
            ENDCG
        }

        Pass // 1: vertical
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target { return blurDir(i.uv, float2(0, 1)); }
            ENDCG
        }
    }

    FallBack Off
}
