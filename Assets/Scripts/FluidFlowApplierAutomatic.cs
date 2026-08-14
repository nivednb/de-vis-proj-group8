using UnityEngine;

public class FluidFlowApplierAutomatic : MonoBehaviour
{
    private static bool hasRun = false;

    private void Awake()
    {
        if (!hasRun)
        {
            FluidFlowApplier.ApplyFluidFlow();
            hasRun = true;
        }
    }
}
