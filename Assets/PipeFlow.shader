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
            fixed4 _FlowColor, _SpeciesColorA, _SpeciesColorB, _SpeciesColorC;
            float4 _SpeciesFractions;
            float _SpeciesCount, _Tiling, _BaseAlpha, _FlowIntensity, _FlowOffset;
            float _GhostMode, _GhostAlphaMul, _GhostTilingMul, _IsLiquid, _IsTwoPhase;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float3 view:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                o.normal=UnityObjectToWorldNormal(v.normal); o.view=normalize(WorldSpaceViewDir(v.vertex)); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float ndv=saturate(dot(normalize(i.normal),normalize(i.view)));
                float rim=1-ndv;
                fixed3 shell=lerp(fixed3(.22,.25,.28),fixed3(.50,.56,.62),pow(rim,2.5));
                float core=pow(ndv,1.35);
                float tiling=_Tiling*lerp(1,_GhostTilingMul,_GhostMode);
                float y=i.uv.y*tiling+_FlowOffset;
                float lane=frac(i.uv.x);
                float carrier=.88+.12*sin(i.uv.y*3.14159+_FlowOffset*.4);
                fixed3 species=0; float packets=0;
                if(_IsLiquid>.5)
                {
                    float p=(1-smoothstep(.01,.32,abs(frac(y*.20)-.5)))*(1-smoothstep(.12,.42,abs(lane-.5)));
                    species=_FlowColor.rgb*p*.35; packets=p*.18;
                }
                else if(_IsTwoPhase>.5)
                {
                    float a=(1-smoothstep(.02,.20,abs(frac(y*.48)-.5)))*(1-smoothstep(.08,.27,abs(lane-.32)));
                    float b=(1-smoothstep(.03,.25,abs(frac(y*.25)-.5)))*(1-smoothstep(.14,.37,abs(lane-.68)));
                    species=_SpeciesColorA.rgb*a+_SpeciesColorB.rgb*b; packets=a*.25+b*.35;
                }
                else
                {
                    float a=(1-smoothstep(.02,.22,abs(frac(y*.38+.12)-.5)))*(1-smoothstep(.08,.28,abs(lane-.26)));
                    float b=(1-smoothstep(.02,.22,abs(frac(y*.42+.42)-.5)))*(1-smoothstep(.08,.28,abs(lane-.50)))*step(1.5,_SpeciesCount);
                    float c=(1-smoothstep(.02,.22,abs(frac(y*.34+.68)-.5)))*(1-smoothstep(.08,.28,abs(lane-.74)))*step(2.5,_SpeciesCount);
                    float3 weights=sqrt(saturate(_SpeciesFractions.xyz));
                    species=_SpeciesColorA.rgb*a*weights.x+_SpeciesColorB.rgb*b*weights.y+_SpeciesColorC.rgb*c*weights.z;
                    packets=(a*weights.x+b*weights.y+c*weights.z)*.32;
                }
                float ghost=lerp(1,_GhostAlphaMul,_GhostMode);
                fixed3 fluid=_FlowColor.rgb*.72+species*_FlowIntensity;
                fixed4 result;
                result.rgb=lerp(shell,fluid,core);
                result.a=saturate((.34+_BaseAlpha*carrier*core+packets*_FlowIntensity)*ghost);
                return result;
            }
            ENDCG
        }
    }
}
