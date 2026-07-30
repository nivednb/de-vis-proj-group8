using System.Collections.Generic;
using UnityEngine;

public class FlowPath : MonoBehaviour
{
    public List<Transform> waypoints = new List<Transform>();

    public int Count => waypoints.Count;

    public Transform GetWaypoint(int index)
    {
        return waypoints[index];
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        Gizmos.color = Color.green;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;

            Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            Gizmos.DrawSphere(waypoints[i].position, 0.05f);
        }
    }
}