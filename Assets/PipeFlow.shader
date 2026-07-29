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
            float _UseObjectFlow, _FlowMin, _FlowLength;
            float4 _FlowAxisOS;
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
                float ndv=saturate(dot(normalize(i.normal),normalize(i.view)));
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
                float y=longitudinal*tiling+_FlowOffset;
                float carrier=.82+.18*sin(longitudinal*3.14159+_FlowOffset*.4);
                fixed3 species=0; float packets=0;
                if(_IsLiquid>.5)
                {
                    // Liquids read as a continuous filled core with small bright
                    // entrained droplets, rather than a train of PVC-like rings.
                    float p=(1-smoothstep(.015,.105,abs(frac(y*.24)-.5)))*(1-smoothstep(.035,.17,abs(lane-.5)));
                    species=_FlowColor.rgb*p*.72; packets=p*.48;
                }
                else if(_IsTwoPhase>.5)
                {
                    float a=(1-smoothstep(.015,.105,abs(frac(y*.48)-.5)))*(1-smoothstep(.035,.16,abs(lane-.31)));
                    float b=(1-smoothstep(.015,.12,abs(frac(y*.29)-.5)))*(1-smoothstep(.04,.18,abs(lane-.69)));
                    species=_SpeciesColorA.rgb*a+_SpeciesColorB.rgb*b; packets=a*.62+b*.68;
                }
                else
                {
                    // Compact, independently moving packets. Narrow longitudinal
                    // and circumferential masks make each species read as discrete
                    // molecules instead of broad colour bands around the pipe.
                    float a=(1-smoothstep(.012,.105,abs(frac(y*.38+.12)-.5)))*(1-smoothstep(.025,.15,abs(lane-.20)));
                    float b=(1-smoothstep(.012,.105,abs(frac(y*.43+.42)-.5)))*(1-smoothstep(.025,.15,abs(lane-.50)))*step(1.5,_SpeciesCount);
                    float c=(1-smoothstep(.012,.105,abs(frac(y*.34+.68)-.5)))*(1-smoothstep(.025,.15,abs(lane-.80)))*step(2.5,_SpeciesCount);
                    float3 weights=sqrt(saturate(_SpeciesFractions.xyz));
                    species=_SpeciesColorA.rgb*a*weights.x+_SpeciesColorB.rgb*b*weights.y+_SpeciesColorC.rgb*c*weights.z;
                    packets=(a*weights.x+b*weights.y+c*weights.z)*1.15;
                }
                float ghost=lerp(1,_GhostAlphaMul,_GhostMode);
                fixed3 fluid=_FlowColor.rgb*.36+species*(1.35*_FlowIntensity);
                fixed4 result;
                result.rgb=lerp(shell*.84,fluid,saturate(core*.66+packets));
                result.a=saturate((.20+_BaseAlpha*(.65+.35*carrier)*core+packets*_FlowIntensity)*ghost);
                return result;
            }
            ENDCG
        }
    }
}
