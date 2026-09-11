Shader "Custom/PipeFlow"
{
    Properties
    {
        _MainTex ("Flow Texture", 2D) = "white" {}
        _FlowColor ("Carrier Colour", Color) = (0.2,0.8,0.3,1)
        _SpeciesColorA ("Species A", Color) = (0.2,1,0.3,1)
        _SpeciesColorB ("Species B", Color) = (0.7,0.9,1,1)
        _SpeciesColorC ("Species C", Color) = (1,0.8,0.2,1)
        _SpeciesCount ("Species Count", Range(1,3)) = 1
        _SpeciesFractions ("Species Fractions", Vector) = (1,0,0,0)
        _FlowSpeed ("Flow Speed", Float) = 1
        _Tiling ("Tracer Density", Float) = 18
        _BaseAlpha ("Pipe Alpha", Range(0,1)) = 0.2
        _FlowIntensity ("Flow Intensity", Range(0,2)) = 1.1
        [Toggle] _GhostMode ("Ghost Supply", Float) = 0
        _GhostAlphaMul ("Ghost Alpha", Range(0,1)) = 0.4
        _GhostTilingMul ("Ghost Spacing", Range(0.1,1)) = 0.5
        _IsLiquid ("Liquid", Float) = 0
        _IsTwoPhase ("Two Phase", Float) = 0
        _Highlight ("Probe Highlight", Range(0,1)) = 0
        _UseObjectFlow ("Use Object Flow Coordinates", Float) = 0
        _FlowAxisOS ("Flow Axis (Object Space)", Vector) = (0,1,0,0)
        _FlowMin ("Flow Axis Minimum", Float) = -0.5
        _FlowLength ("Flow Axis Length", Float) = 1
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
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _FlowColor, _SpeciesColorA, _SpeciesColorB, _SpeciesColorC;
            float4 _SpeciesFractions;
            float _SpeciesCount, _Tiling, _BaseAlpha, _FlowIntensity, _FlowOffset;
            float _GhostMode, _GhostAlphaMul, _GhostTilingMul, _IsLiquid, _IsTwoPhase;
            float _Highlight;
            float _UseObjectFlow, _FlowMin, _FlowLength;
            float4 _FlowAxisOS;

            // --- procedural noise -------------------------------------------------
            // Continuous fields replace the old per-species packet masks. Gas and liquid
            // differ in how the field is used, not just in colour: a gas is a turbulent
            // cloud whose density varies everywhere, a liquid is a filled bore whose
            // surface merely ripples.
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float vnoise(float2 p)
            {
                float2 ip = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(ip);
                float b = hash21(ip + float2(1,0));
                float c = hash21(ip + float2(0,1));
                float d = hash21(ip + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }
            float fbm2(float2 p) { return vnoise(p) * 0.64 + vnoise(p * 2.17 + 4.7) * 0.36; }
            float fbm3(float2 p)
            {
                return vnoise(p) * 0.52 + vnoise(p * 2.11 + 3.3) * 0.32 + vnoise(p * 4.23 + 9.1) * 0.16;
            }

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f
            {
                float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1;
                float3 view:TEXCOORD2; float3 objectPos:TEXCOORD3; float3 objectNormal:TEXCOORD4;
            };
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                o.normal=UnityObjectToWorldNormal(v.normal); o.view=normalize(WorldSpaceViewDir(v.vertex));
                o.objectPos=v.vertex.xyz; o.objectNormal=normalize(v.normal); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 wn=normalize(i.normal);
                float ndv=saturate(dot(wn,normalize(i.view)));
                float rim=1-ndv;
                fixed3 shell=lerp(fixed3(.28,.31,.35),fixed3(.62,.68,.74),pow(rim,2.5));
                float core=pow(ndv,1.35);
                float tiling=_Tiling*lerp(1,_GhostTilingMul,_GhostMode);
                float3 axis=normalize(_FlowAxisOS.xyz);
                float objectT=saturate((dot(i.objectPos,axis)-_FlowMin)/max(_FlowLength,.0001));
                float3 an=abs(axis);
                float objectLane=(an.y>an.x && an.y>an.z)
                    ? atan2(i.objectNormal.z,i.objectNormal.x)/6.2831853+.5
                    : ((an.x>an.z)
                        ? atan2(i.objectNormal.y,i.objectNormal.z)/6.2831853+.5
                        : atan2(i.objectNormal.y,i.objectNormal.x)/6.2831853+.5);
                float longitudinal=lerp(i.uv.y,objectT,saturate(_UseObjectFlow));
                float lane=frac(lerp(i.uv.x,objectLane,saturate(_UseObjectFlow)));

                // Advected axial coordinate, and a circumferential coordinate mirrored so
                // the noise has no visible seam where the lane wraps.
                float y=longitudinal*tiling+_FlowOffset;
                float laneM=abs(frac(lane)*2.0-1.0);
                // Which way is down on this part of the bore — lets a condensing stream
                // actually pool along the bottom of a horizontal run. Near zero on vertical
                // runs, where the phases correctly stop separating.
                float down=saturate(-wn.y);

                float carrier=.82+.18*sin(longitudinal*3.14159+_FlowOffset*.4);
                float flowOn=saturate(_FlowIntensity);

                fixed3 fluid=0; float fill=0; float bodyAlpha=0;
                if(_IsLiquid>.5)
                {
                    // A liquid fills the bore. Nearly uniform colour, slow advected shading
                    // for depth, and one bright curvature highlight — no gaps, no packets.
                    float ripple=fbm2(float2(y*.13,laneM*1.6));
                    float deep=fbm2(float2(y*.05+21.0,laneM*.8));
                    float sheen=pow(saturate(1.0-abs(laneM-.30)*2.2),3.5);
                    fluid=_FlowColor.rgb*(.58+.26*ripple+.16*deep)+sheen*.30;
                    fill=1.0;
                    bodyAlpha=.88;
                }
                else if(_IsTwoPhase>.5)
                {
                    // Condensed liquid runs along the underside with a wavy interface; gas
                    // rides above it, carrying entrained droplets.
                    float wave=fbm2(float2(y*.20,laneM*1.8));
                    float level=saturate((down-(.46-.16*wave))*3.2);
                    float mist=smoothstep(.32,.80,fbm3(float2(y*.16+17.0,laneM*3.0)));
                    float drops=smoothstep(.74,.97,fbm2(float2(y*.55+5.0,laneM*3.4)));
                    fixed3 liq=_SpeciesColorA.rgb*(.52+.30*wave);
                    fixed3 gas=_SpeciesColorB.rgb*(.18+.46*mist);
                    fluid=lerp(gas,liq,level)+drops*(1.0-level)*.28;
                    fill=lerp(.14+.44*mist+drops*.34,1.0,level);
                    bodyAlpha=lerp(.10+.30*mist,.84,level);
                }
                else
                {
                    // A gas is compressible and turbulent: density varies continuously and
                    // nothing has a hard edge. Composition shows as colour drifting through
                    // the cloud, each species riding its own eddy, rather than as a train of
                    // separate solid pellets.
                    // Low axial frequency against a higher circumferential one stretches each
                    // eddy along the bore, so the gas reads as streaming rather than bubbling.
                    float e1=fbm3(float2(y*.12,laneM*2.4));
                    float e2=fbm3(float2(y*.155+13.7,laneM*2.9+5.5));
                    float e3=fbm3(float2(y*.10+27.1,laneM*2.0+9.3));
                    float3 w=saturate(_SpeciesFractions.xyz);
                    w.y*=step(1.5,_SpeciesCount);
                    w.z*=step(2.5,_SpeciesCount);
                    float s1=w.x*e1, s2=w.y*e2, s3=w.z*e3;
                    float sum=max(s1+s2+s3,1e-4);
                    fixed3 tint=(_SpeciesColorA.rgb*s1+_SpeciesColorB.rgb*s2+_SpeciesColorC.rgb*s3)/sum;
                    float wsum=max(w.x+w.y+w.z,1e-4);
                    // Value noise clusters around 0.5, which on its own reads as uniform fog.
                    // Stretching it to the full range and squaring gives real gaps between
                    // real puffs — the contrast is what makes it look like a gas.
                    float raw=sum/wsum;
                    float turb=smoothstep(.30,.80,raw);
                    turb*=turb;
                    // The route's legend colour stays dominant; the species tint modulates it.
                    fluid=lerp(_FlowColor.rgb,tint,.55)*(.26+.70*turb);
                    fill=.10+.62*turb;
                    bodyAlpha=.05+.40*turb;
                }

                float ghost=lerp(1,_GhostAlphaMul,_GhostMode);
                fixed3 shellLit=shell*(.84+.60*_Highlight);
                fixed4 result;
                // Every tube draws both faces and runs unlit over a bloomed scene, so the
                // fluid is deliberately kept below 1 — brightness here accumulates across
                // overlapping pipes and blows out fast.
                fixed3 body=saturate(fluid*(.80+.28*_FlowIntensity));
                result.rgb=lerp(shellLit,body,saturate(core*.55+fill*.80*flowOn));
                result.rgb+=_Highlight*(.10+.26*pow(rim,2.0));
                result.a=saturate((.12+_BaseAlpha*(.65+.35*carrier)*core+bodyAlpha*_FlowIntensity)*ghost
                    +_Highlight*.30);
                return result;
            }
            ENDCG
        }
    }
}
