using UnityEngine;

[DisallowMultipleComponent]
public sealed class PipeFlowAnimator : MonoBehaviour
{
    public PlantFlowKind flowKind = PlantFlowKind.MixedFeed;
    public Color flowColor = Color.white;
    [Min(0f)] public float speed = 1f;
    [Min(0f)] public float density = 18f;
    [Range(0f, 1f)] public float pipeAlpha = .2f;
    [Range(0f, 3f)] public float flowIntensity = 1f;
    public bool reverseDirection;
    public bool isFlowing = true;
    public bool isGhostSupply;
    [Tooltip("Visible molar/phase fractions for multi-species routes. Values are normalized by Apply().")]
    public Vector3 speciesFractions = new(1f, 0f, 0f);

    static readonly int FlowColor=Shader.PropertyToID("_FlowColor"), A=Shader.PropertyToID("_SpeciesColorA"),
        B=Shader.PropertyToID("_SpeciesColorB"), C=Shader.PropertyToID("_SpeciesColorC"),
        Count=Shader.PropertyToID("_SpeciesCount"), Offset=Shader.PropertyToID("_FlowOffset"),
        Fractions=Shader.PropertyToID("_SpeciesFractions"),
        Tiling=Shader.PropertyToID("_Tiling"), Alpha=Shader.PropertyToID("_BaseAlpha"),
        Intensity=Shader.PropertyToID("_FlowIntensity"), Ghost=Shader.PropertyToID("_GhostMode"),
        Liquid=Shader.PropertyToID("_IsLiquid"), TwoPhase=Shader.PropertyToID("_IsTwoPhase");
    Renderer target;
    MaterialPropertyBlock block;
    float offset;

    void Awake() => Ensure();
    void OnEnable() => Apply();
    void OnValidate() => Apply();
    void Update()
    {
        Ensure();
        if (target == null) return;
        if (isFlowing) offset=Mathf.Repeat(offset+Time.deltaTime*speed*(reverseDirection?-1f:1f),1f);
        target.GetPropertyBlock(block); block.SetFloat(Offset,offset); target.SetPropertyBlock(block);
    }

    public void Apply()
    {
        Ensure(); if(target==null)return;
        Color a=flowColor,b=flowColor,c=flowColor; float count=1,liquid=0,twoPhase=0;
        switch(flowKind)
        {
            case PlantFlowKind.MixedFeed:
                a=new Color(.10f,1f,.22f); b=new Color(.86f,.94f,1f); count=2; break;
            case PlantFlowKind.SyngasCold:
            case PlantFlowKind.SyngasHeated:
                a=new Color(.10f,1f,.22f); b=new Color(.86f,.94f,1f); count=2; break;
            case PlantFlowKind.ReactorEffluent:
                a=new Color(.72f,.18f,1f); b=new Color(.15f,.70f,1f); c=new Color(.10f,1f,.22f); count=3; twoPhase=1; break;
            case PlantFlowKind.CrudeMethanolVapourLiquid:
                a=new Color(.72f,.18f,1f); b=new Color(.15f,.70f,1f); count=2; twoPhase=1; break;
            case PlantFlowKind.RichAmine:
            case PlantFlowKind.LeanAmine:
            case PlantFlowKind.LiquidCrudeMethanol:
            case PlantFlowKind.MethanolProduct: liquid=1; break;
            case PlantFlowKind.RecycleGas:
                a=new Color(.10f,1f,.22f); b=new Color(.86f,.94f,1f); count=2; break;
        }
        target.GetPropertyBlock(block);
        block.SetColor(FlowColor,flowColor); block.SetColor(A,a); block.SetColor(B,b); block.SetColor(C,c);
        Vector3 fractions=speciesFractions;
        float total=Mathf.Max(.0001f,fractions.x+fractions.y+fractions.z);
        fractions/=total;
        block.SetVector(Fractions,new Vector4(fractions.x,fractions.y,fractions.z,0f));
        block.SetFloat(Count,count); block.SetFloat(Tiling,density); block.SetFloat(Alpha,pipeAlpha);
        block.SetFloat(Intensity,isFlowing?flowIntensity:0); block.SetFloat(Ghost,isGhostSupply?1:0);
        block.SetFloat(Liquid,liquid); block.SetFloat(TwoPhase,twoPhase); block.SetFloat(Offset,offset);
        target.SetPropertyBlock(block);
    }
    void Ensure(){ if(target==null)target=GetComponent<Renderer>(); if(block==null)block=new MaterialPropertyBlock(); }
}
