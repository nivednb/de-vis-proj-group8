using UnityEngine;
using System.Collections.Generic;

public class FluidFlowApplier : MonoBehaviour
{
    public static void ApplyFluidFlow()
    {
        Shader shader = Shader.Find("Custom/FluidFlow");
        if (shader == null)
        {
            Debug.LogError("FluidFlow shader not found!");
            return;
        }

        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
        int count = 0;

        foreach (MeshRenderer r in renderers)
        {
            Transform parent = r.transform.parent;
            if (parent == null || parent.name != "Pipings") continue;

            string name = r.gameObject.name.ToLower();
            Color color = new Color(0, 1, 0, 1);

            if (name.Contains("h2")) color = new Color(0, 1, 0, 1);
            else if (name.Contains("co2")) color = new Color(0.1f, 0.2f, 0.6f, 1);
            else if (name.Contains("richam")) color = new Color(0, 1, 1, 1);
            else if (name.Contains("leanam")) color = new Color(0.5f, 1, 0.5f, 1);
            else if (name.Contains("syngas") || name.Contains("mixedf") || name.Contains("recyclega")) color = new Color(1, 1, 0, 1);
            else if (name.Contains("reactore")) color = new Color(1, 0, 0, 1);
            else if (name.Contains("crudeme") || name.Contains("liq_crude")) color = new Color(0, 0.7f, 1, 1);
            else if (name.Contains("methanolp")) color = new Color(1, 0, 1, 1);

            Material mat = new Material(shader);
            mat.SetColor("_FlowColor", color);
            mat.SetFloat("_ScrollSpeed", 2f);
            mat.SetFloat("_FlowDensity", 3f);
            mat.SetFloat("_Opacity", 0.7f);

            r.material = mat;
            count++;
        }

        Debug.Log("Applied FluidFlow to " + count + " pipes");
    }
}
