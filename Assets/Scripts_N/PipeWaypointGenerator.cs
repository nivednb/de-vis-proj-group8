using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;

public class PipeWaypointGenerator : EditorWindow
{
    int pointsPerPipe = 5;

    [MenuItem("Tools/Flow/Generate Pipe Waypoints")]
    static void ShowWindow()
    {
        GetWindow<PipeWaypointGenerator>("Pipe Waypoints");
    }

    void OnGUI()
    {
        GUILayout.Label("Generate Waypoints Along Selected Pipes", EditorStyles.boldLabel);

        pointsPerPipe = EditorGUILayout.IntSlider(
            "Points Per Pipe",
            pointsPerPipe,
            2,
            10);

        if (GUILayout.Button("Generate"))
        {
            Generate();
        }
    }

    void Generate()
    {
        Transform[] selected = Selection.transforms;

        if (selected.Length == 0)
        {
            Debug.LogWarning("No pipes selected.");
            return;
        }

        GameObject parent = new GameObject("Generated_FlowPath");

        int count = 0;

        foreach (Transform pipe in selected)
        {
            Renderer r = pipe.GetComponent<Renderer>();

            if (r == null)
                continue;

            float length =
                Mathf.Max(
                    r.bounds.size.x,
                    Mathf.Max(r.bounds.size.y, r.bounds.size.z));

            Vector3 direction = pipe.up;

            Vector3 start =
                pipe.position - direction * length * 0.5f;

            Vector3 end =
                pipe.position + direction * length * 0.5f;

            for (int i = 0; i < pointsPerPipe; i++)
            {
                float t = (float)i / (pointsPerPipe - 1);

                GameObject p = new GameObject("P" + count.ToString("000"));

                p.transform.position =
                    Vector3.Lerp(start, end, t);

                p.transform.SetParent(parent.transform);

                count++;
            }
        }

        Debug.Log("Generated " + count + " waypoints.");
    }
}
#endif
