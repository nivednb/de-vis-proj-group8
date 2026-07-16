Shader "Custom/PipeFlow"
{
    Properties
    {
        _MainTex ("Flow Texture (tileable dashes/chevrons, alpha)", 2D) = "white" {}
        _FlowColor ("Flow Color", Color) = (0.2, 0.8, 0.3, 1)
        _FlowSpeed ("Flow Speed", Float) = 1.0
        _Tiling ("V Tiling (dash density)", Float) = 4.0
        _BaseAlpha ("Pipe Base Alpha (faint pipe tint, 0 = transparent pipe)", Range(0,1)) = 0.15
        _FlowIntensity ("Flow Band Intensity", Range(0,2)) = 1.1
        _BandSharpness ("Flow Band Sharpness", Range(0.1,1.5)) = 0.62
        [Toggle] _GhostMode ("Ghost Supply Mode (unmodeled source)", Float) = 0
        _GhostAlphaMul ("Ghost Alpha Multiplier", Range(0,1)) = 0.4
        _GhostTilingMul ("Ghost Tiling Multiplier (wider dash spacing)", Range(0.1,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _FlowColor;
            float _FlowSpeed;
            float _Tiling;
            float _BaseAlpha;
            float _FlowIntensity;
            float _BandSharpness;
            float _FlowOffset; // set per-instance via MaterialPropertyBlock
            float _GhostMode;
            float _GhostAlphaMul;
            float _GhostTilingMul;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float tiling = _Tiling * lerp(1.0, _GhostTilingMul, _GhostMode);
                o.uv.y *= tiling;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 scrolledUV = i.uv + float2(0, _FlowOffset);
                fixed4 tex = tex2D(_MainTex, scrolledUV);
                float proceduralBand = smoothstep(1.0 - _BandSharpness, 1.0, frac(scrolledUV.y));
                float centerGlow = 1.0 - abs(i.uv.x - 0.5) * 1.4;
                centerGlow = saturate(centerGlow);
                fixed4 col = _FlowColor;
                float alphaMul = lerp(1.0, _GhostAlphaMul, _GhostMode);
                col.rgb *= 0.75 + proceduralBand * 0.75;
                col.a = saturate(_BaseAlpha + max(tex.a, proceduralBand) * centerGlow * _FlowIntensity) * alphaMul;
                return col;
            }
            ENDCG
        }
    }
}
