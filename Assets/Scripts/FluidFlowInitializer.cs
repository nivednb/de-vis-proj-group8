using UnityEngine;

public class FluidFlowInitializer : MonoBehaviour
{
    private void Awake()
    {
        // Apply shader when scene loads
        FluidFlowApplier.ApplyFluidFlow();
        Destroy(this.gameObject);
    }
}
