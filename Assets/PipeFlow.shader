Shader "Custom/PipeFlow"
{
    Properties
    {
        _MainTex ("Flow Texture (tileable dashes/chevrons, alpha)", 2D) = "white" {}
        _FlowColor ("Flow Color", Color) = (0.2, 0.8, 0.3, 1)
        _SpeciesColorA ("Primary Species", Color) = (0.2, 1.0, 0.3, 1)
        _SpeciesColorB ("Secondary Species", Color) = (0.7, 0.9, 1.0, 1)
        _SpeciesColorC ("Tertiary Species", Color) = (1.0, 0.8, 0.2, 1)
        _SpeciesCount ("Species Count", Range(1,3)) = 1
        _FlowSpeed ("Flow Speed", Float) = 1.0
        _Tiling ("V Tiling (dash density)", Float) = 4.0
        _BaseAlpha ("Pipe Base Alpha (faint pipe tint, 0 = transparent pipe)", Range(0,1)) = 0.15
        _FlowIntensity ("Flow Band Intensity", Range(0,2)) = 1.1
        _BandSharpness ("Flow Band Sharpness", Range(0.1,1.5)) = 0.62
        [Toggle] _GhostMode ("Ghost Supply Mode (unmodeled source)", Float) = 0
        _GhostAlphaMul ("Ghost Alpha Multiplier", Range(0,1)) = 0.4
        _GhostTilingMul ("Ghost Tiling Multiplier (wider dash spacing)", Range(0.1,1)) = 0.5
        _IsLiquid ("Is Liquid Mode", Float) = 0
        _IsTwoPhase ("Is Two-Phase Mode", Float) = 0
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
            fixed4 _SpeciesColorA;
            fixed4 _SpeciesColorB;
            fixed4 _SpeciesColorC;
            float _SpeciesCount;
            float _FlowSpeed;
            float _Tiling;
            float _BaseAlpha;
            float _FlowIntensity;
            float _BandSharpness;
            float _FlowOffset; // set per-instance via MaterialPropertyBlock
            float _GhostMode;
            float _GhostAlphaMul;
            float _GhostTilingMul;
            float _IsLiquid;
            float _IsTwoPhase;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD3;
                float3 worldView : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldView = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.worldNormal);
                float3 V = normalize(i.worldView);
                float NdotV = saturate(dot(N, V));

                // 1. Physical Pipe Shell (steel-grey neutral engineering material)
                float rim = 1.0 - NdotV;
                fixed4 pipeBaseColor = fixed4(0.22, 0.25, 0.28, 0.38);
                fixed4 pipeRimColor = fixed4(0.50, 0.56, 0.62, 0.80);
                fixed4 physicalShell = lerp(pipeBaseColor, pipeRimColor, pow(rim, 2.5));

                // 2. Fluid Core (concentrated center-line movement)
                float fluidCore = pow(NdotV, 1.6);

                // Continuous Carrier Flow (smooth, continuous background, direction visible through gentle longitudinal movement)
                float speedY = _FlowOffset;
                float scrolledY = i.uv.y * _Tiling + speedY;

                fixed4 carrierColor = _FlowColor;
                float carrierWave = 0.90 + 0.10 * sin(i.uv.y * 3.14159 + speedY * 0.4);
                float carrierAlpha = _BaseAlpha * carrierWave * fluidCore;

                // 3. Flow Tracers (elongated packets in parallel lanes inside the pipe)
                float lane = frac(i.uv.x);
                fixed3 speciesColor = fixed3(0,0,0);
                float speciesAlpha = 0;

                if (_IsLiquid > 0.5)
                {
                    // Liquid: continuous carrier, very sparse long velocity tracers
                    float scrolledYL = scrolledY * 0.4;
                    float tracerL = abs(frac(scrolledYL * 0.2) - 0.5);
                    float packetL = (1.0 - smoothstep(0.01, 0.32, tracerL)) * (1.0 - smoothstep(0.12, 0.40, abs(lane - 0.5)));

                    speciesColor = _FlowColor.rgb * packetL * 0.35;
                    speciesAlpha = packetL * 0.18;
                    carrierAlpha = _BaseAlpha * 1.15 * fluidCore;
                }
                else if (_IsTwoPhase > 0.5)
                {
                    // Two-phase: fast vapour packets (top) + slower liquid droplets (bottom)
                    // Vapour (Species A - fast)
                    float scrolledYV = scrolledY * 1.4;
                    float tracerV = abs(frac(scrolledYV * 0.45) - 0.5);
                    float packetV = (1.0 - smoothstep(0.02, 0.20, tracerV)) * (1.0 - smoothstep(0.08, 0.25, abs(lane - 0.32)));

                    // Liquid droplets (Species B - slow)
                    float scrolledYLd = scrolledY * 0.55;
                    float tracerLd = abs(frac(scrolledYLd * 0.28) - 0.5);
                    float packetLd = (1.0 - smoothstep(0.03, 0.25, tracerLd)) * (1.0 - smoothstep(0.14, 0.35, abs(lane - 0.68)));

                    speciesColor = _SpeciesColorA.rgb * packetV + _SpeciesColorB.rgb * packetLd;
                    speciesAlpha = packetV * 0.25 + packetLd * 0.35;
                    carrierAlpha = _BaseAlpha * 0.85 * fluidCore;
                }
                else
                {
                    // Multi-component gas representation: parallel lanes
                    // Species A (Lane 1, centered at 0.26)
                    float scrolledYA = scrolledY * 0.95 + 0.12;
                    float tracerA_Length = abs(frac(scrolledYA * 0.38) - 0.5);
                    float packetA = (1.0 - smoothstep(0.02, 0.22, tracerA_Length)) * (1.0 - smoothstep(0.08, 0.28, abs(lane - 0.26)));

                    // Species B (Lane 2, centered at 0.50)
                    float scrolledYB = scrolledY * 1.05 + 0.42;
                    float tracerB_Length = abs(frac(scrolledYB * 0.42) - 0.5);
                    float packetB = (1.0 - smoothstep(0.02, 0.22, tracerB_Length)) * (1.0 - smoothstep(0.08, 0.28, abs(lane - 0.50))) * step(1.5, _SpeciesCount);

                    // Species C (Lane 3, centered at 0.74)
                    float scrolledYC = scrolledY * 0.88 + 0.68;
                    float tracerC_Length = abs(frac(scrolledYC * 0.34) - 0.5);
                    float packetC = (1.0 - smoothstep(0.02, 0.22, tracerC_Length)) * (1.0 - smoothstep(0.08, 0.28, abs(lane - 0.74))) * step(2.5, _SpeciesCount);

                    speciesColor = _SpeciesColorA.rgb * packetA + _SpeciesColorB.rgb * packetB + _SpeciesColorC.rgb * packetC;
                    speciesAlpha = (packetA + packetB + packetC) * 0.32;
                }

                // Blend fluid core with physical shell
                fixed4 fluidColor = fixed4(carrierColor.rgb * 0.80 + speciesColor * _FlowIntensity, saturate(carrierAlpha + speciesAlpha * _FlowIntensity));

                fixed4 finalColor;
                finalColor.rgb = lerp(physicalShell.rgb, fluidColor.rgb, fluidCore);
                finalColor.a = saturate(physicalShell.a + fluidColor.a);

                return finalColor;
            }
            ENDCG
        }
    }
}
