using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Legacy: applies the old FluidFlow shader to all pipes when called explicitly.
///
/// This used to hook EditorSceneManager.sceneOpened. The build pipeline opens scenes too, so
/// every player build silently baked FluidFlow materials (which render magenta under URP)
/// into the pipes. Pipe materials are now owned by FinalPlantFlowRuntime, so the hook is gone.
/// </summary>
public class AutoApplyFluidFlow : MonoBehaviour
{
    public static void ApplyFluidFlowToPipes()
    {
        Shader fluidFlowShader = Shader.Find("Custom/FluidFlow");
        if (fluidFlowShader == null)
        {
            Debug.LogError("❌ FluidFlow shader not found! Make sure Assets/Shaders/FluidFlow.shader exists.");
            return;
        }

        // Color mapping by pipe type
        Dictionary<string, Color> pipeColors = new Dictionary<string, Color>()
        {
            { "H2_pipe", new Color(0, 1, 0, 1) },              // GREEN
            { "H2Storage_pipe", new Color(0, 1, 0, 1) },       // GREEN
            { "CO2_pipe", new Color(0.1f, 0.2f, 0.6f, 1) },   // DARK BLUE
            { "RichAmine_pipe", new Color(0, 1, 1, 1) },       // CYAN
            { "LeanAmine_pipe", new Color(0.5f, 1, 0.5f, 1) }, // LIGHT GREEN
            { "Syngas_pipe", new Color(1, 1, 0, 1) },          // YELLOW
            { "RecycleGas_pipe", new Color(1, 1, 0, 1) },      // YELLOW
            { "MixedFeed_pipe", new Color(1, 1, 0, 1) },       // YELLOW
            { "ReactorEffluent_pipe", new Color(1, 0, 0, 1) }, // RED
            { "CrudeMeOH_pipe", new Color(0, 0.7f, 1, 1) },    // LIGHT BLUE
            { "Liq_CrudeMeOH_pipe", new Color(0, 0.7f, 1, 1) },// LIGHT BLUE
            { "MethanolProduct_pipe", new Color(1, 0, 1, 1) }, // MAGENTA
        };

        MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
        int applied = 0;

        foreach (MeshRenderer renderer in allRenderers)
        {
            // Check if parent is Pipings
            if (renderer.transform.parent == null || renderer.transform.parent.name != "Pipings")
                continue;

            string pipeName = renderer.gameObject.name;
            Color flowColor = new Color(0, 1, 0, 1); // Default green

            // Match pipe type and assign color
            foreach (var kvp in pipeColors)
            {
                if (pipeName.Contains(kvp.Key))
                {
                    flowColor = kvp.Value;
                    break;
                }
            }

            // Create new material with FluidFlow shader
            Material mat = new Material(fluidFlowShader);
            mat.SetColor("_FlowColor", flowColor);
            mat.SetFloat("_ScrollSpeed", 2.0f);
            mat.SetFloat("_FlowDensity", 3.0f);
            mat.SetFloat("_Opacity", 0.7f);

            renderer.material = mat;
            applied++;
        }

        if (applied > 0)
        {
            Debug.Log($"✅ FluidFlow applied to {applied} pipes!");
        }
    }
}
