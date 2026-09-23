Shader "Custom/PipeFlow"
{
    Properties
    {
        _FlowColor ("Stream Colour (legend)", Color) = (0.2,0.8,0.3,1)
        _FlowScale ("Feature Scale (cells per metre)", Float) = 0.6
        _FlowOffset ("Advected Offset (cells)", Float) = 0
        _BaseAlpha ("Pipe Alpha", Range(0,1)) = 0.2
        _FlowIntensity ("Flow Intensity", Range(0,2)) = 1.1
        [Toggle] _GhostMode ("Ghost Supply", Float) = 0
        _GhostAlphaMul ("Ghost Alpha", Range(0,1)) = 0.4
        _IsLiquid ("Liquid", Float) = 0
        _IsTwoPhase ("Two Phase", Float) = 0
        _Dashed ("Dashed (recycle loop)", Float) = 0
        _Highlight ("Probe Highlight", Range(0,1)) = 0
        _FlowOriginWS ("Route Origin (xyz), Route Distance There (w)", Vector) = (0,0,0,0)
        _FlowDirWS ("Flow Direction (world)", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _FlowColor;
            float _FlowScale, _FlowOffset, _BaseAlpha, _FlowIntensity;
            float _GhostMode, _GhostAlphaMul, _IsLiquid, _IsTwoPhase, _Dashed, _Highlight;
            float4 _FlowOriginWS, _FlowDirWS;
            // Plant-wide evolution clock, set once per frame by FinalPlantFlowRuntime.
            float _PipeFlowTime;

            // --- periodic value noise ---------------------------------------------
            // Every lattice repeats after a whole number of cells, and the C# side wraps
            // the advected offset and the clock at exactly those periods. The field is
            // therefore seamless for ever: nothing ever jumps back or restarts.
            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }
            float3 wrapCell(float3 c, float3 period) { return c - period * floor(c / period); }
            float vnoise(float3 p, float3 period)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(wrapCell(i, period));
                float n100 = hash31(wrapCell(i + float3(1,0,0), period));
                float n010 = hash31(wrapCell(i + float3(0,1,0), period));
                float n110 = hash31(wrapCell(i + float3(1,1,0), period));
                float n001 = hash31(wrapCell(i + float3(0,0,1), period));
                float n101 = hash31(wrapCell(i + float3(1,0,1), period));
                float n011 = hash31(wrapCell(i + float3(0,1,1), period));
                float n111 = hash31(wrapCell(i + float3(1,1,1), period));
                float a = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
                float b = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
                return lerp(a, b, f.z);
            }

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float3 worldPos:TEXCOORD0; float3 normal:TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                float3 wn = normalize(i.normal);
                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndv = abs(dot(wn, view));
                float rim = 1 - ndv;
                float core = pow(ndv, 1.35);

                // Distance along the whole route in metres. Every segment of a route carries
                // its own origin and the route distance at that origin, so consecutive
                // segments continue one another instead of each restarting the pattern.
                float3 dir = normalize(_FlowDirWS.xyz);
                float s = _FlowOriginWS.w + dot(i.worldPos - _FlowOriginWS.xyz, dir);
                float a = s * _FlowScale - _FlowOffset;

                // Angle around the bore, measured in a frame built from the flow direction.
                float3 refAxis = abs(dir.y) < 0.95 ? float3(0,1,0) : float3(1,0,0);
                float3 u = normalize(cross(refAxis, dir));
                float3 w = cross(dir, u);
                float lane = atan2(dot(wn, w), dot(wn, u)) / 6.2831853 + 0.5;

                float flowOn = saturate(_FlowIntensity);
                float3 c = _FlowColor.rgb;
                float3 fluid = 0; float fill = 0; float bodyAlpha = 0;

                if (_IsLiquid > .5 || _IsTwoPhase > .5)
                {
                    // A liquid fills the bore: nearly uniform legend colour, slow rolling
                    // shading and thin bright flow lines stretched along the pipe.
                    float t = _PipeFlowTime * 0.2;
                    float roll = vnoise(float3(a, lane * 4, t), float3(256, 4, 256));
                    float deep = vnoise(float3(a * 0.5 + 11.0, lane * 4, t * 0.5), float3(128, 4, 128));
                    float lines = vnoise(float3(a * 0.5, lane * 16, t), float3(128, 16, 256));
                    lines = smoothstep(0.62, 0.95, lines);
                    float sheen = pow(saturate(1.0 - abs(ndv - 0.72) * 3.0), 3.0);
                    float3 liquid = c * (0.55 + 0.25 * roll + 0.15 * deep) + lines * 0.22 + sheen * 0.18;

                    if (_IsTwoPhase > .5)
                    {
                        // Condensate runs along the underside with a wavy surface; the vapour
                        // above it is the same stream, lighter and thinner, carrying mist.
                        float down = saturate(-wn.y);
                        float level = saturate((down - (0.42 - 0.18 * roll)) * 3.2);
                        float mist = smoothstep(0.35, 0.85,
                            vnoise(float3(a * 2 + 5.0, lane * 8, t * 2), float3(512, 8, 512)));
                        float3 vapour = lerp(c, 1, 0.35) * (0.30 + 0.55 * mist);
                        fluid = lerp(vapour, liquid, level);
                        fill = lerp(0.30 + 0.45 * mist, 1.0, level);
                        bodyAlpha = lerp(0.18 + 0.35 * mist, 0.86, level);
                    }
                    else
                    {
                        fluid = liquid;
                        fill = 1.0;
                        bodyAlpha = 0.86;
                    }
                }
                else
                {
                    // A gas is turbulent: its density varies continuously, eddies stream along
                    // the pipe and slowly evolve while they travel, so the stream never reads
                    // as a rigid texture sliding past.
                    float t = _PipeFlowTime * 0.5;
                    float big  = vnoise(float3(a, lane * 4, t), float3(256, 4, 256));
                    float mid  = vnoise(float3(a * 2 + 17.0, lane * 8, t * 2), float3(512, 8, 512));
                    float fine = vnoise(float3(a * 4 + 3.0, lane * 16, t * 2), float3(1024, 16, 512));
                    float streak = vnoise(float3(a * 0.5, lane * 16, t * 0.5), float3(128, 16, 128));
                    float dens = smoothstep(0.30, 0.80, big * 0.55 + mid * 0.30 + fine * 0.15);
                    float wisp = smoothstep(0.55, 0.95, streak);
                    float body = saturate(dens * 0.85 + wisp * 0.35);

                    if (_Dashed > .5)
                    {
                        // The recycle loop is dashed in the legend, so here the gas travels
                        // as separated slugs (period 4 cells keeps the wrap seamless).
                        float p = frac(a * 0.25);
                        float dash = smoothstep(0.02, 0.12, p) * (1.0 - smoothstep(0.55, 0.65, p));
                        body *= lerp(0.12, 1.0, dash);
                    }

                    fluid = c * (0.40 + 0.80 * body) + pow(body, 3.0) * 0.16;
                    fill = 0.30 + 0.70 * body;
                    bodyAlpha = 0.14 + 0.62 * body;
                }

                float ghost = lerp(1, _GhostAlphaMul, _GhostMode);
                fixed3 shell = lerp(fixed3(.28,.31,.35), fixed3(.62,.68,.74), pow(rim, 2.5));
                shell *= (.84 + .60 * _Highlight);
                fixed4 result;
                // Every tube draws both faces over a bloomed scene, so the fluid is kept
                // below 1 — brightness accumulates across overlapping pipes.
                fixed3 body = saturate(fluid * (.80 + .20 * saturate(_FlowIntensity)));
                result.rgb = lerp(shell, body, saturate(core * .35 + fill * .85) * flowOn);
                result.rgb += _Highlight * (.10 + .26 * pow(rim, 2.0));
                result.a = saturate((.12 + _BaseAlpha * core + bodyAlpha * _FlowIntensity) * ghost + _Highlight * .30);
                return result;
            }
            ENDCG
        }
    }
}
