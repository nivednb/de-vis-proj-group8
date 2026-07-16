using UnityEngine;

/// <summary>
/// Creates the final flow simulation controller automatically when Play starts.
/// This avoids manual scene setup on machines that can barely open the project.
/// </summary>
public static class FinalPlantFlowBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeController()
    {
        if (Object.FindObjectOfType<FinalPlantFlowRuntime>() != null)
        {
            return;
        }

        GameObject controller = new GameObject("Final MSc Flow Simulation Runtime");
        controller.AddComponent<FinalPlantFlowRuntime>();
        Object.DontDestroyOnLoad(controller);
    }
}
