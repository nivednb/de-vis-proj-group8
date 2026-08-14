Shader "Custom/FluidFlow"
{
    Properties
    {
        _FlowColor ("Flow Color", Color) = (0, 1, 0, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 2.0
        _FlowDensity ("Flow Density", Float) = 3.0
        _Opacity ("Opacity", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FlowColor;
                float _ScrollSpeed;
                float _FlowDensity;
                float _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float random(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float scrollOffset = _Time.y * _ScrollSpeed;
                float wavePosition = frac(IN.uv.x * _FlowDensity + scrollOffset);
                float wavePulse = sin(wavePosition * 3.14159) * 0.5 + 0.5;
                
                float crossSection = abs(IN.uv.y - 0.5) * 2.0;
                crossSection = smoothstep(1.0, 0.0, crossSection);
                
                float flowIntensity = wavePulse * crossSection;
                float variation = random(IN.uv * 5.0 + scrollOffset) * 0.2;
                flowIntensity = flowIntensity * (0.8 + variation);
                
                float4 output = _FlowColor;
                output.a = flowIntensity * _Opacity;
                
                return output;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
