using UnityEngine;

public class FlowFollower : MonoBehaviour
{
    public FlowPath path;
    public float speed = 2f;

    private int targetIndex = 1;

    void Start()
    {
        if (path != null && path.Count > 0)
            transform.position = path.GetWaypoint(0).position;
    }

    void Update()
    {
        if (path == null || path.Count < 2) return;

        Transform target = path.GetWaypoint(targetIndex);

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.position) < 0.03f)
        {
            targetIndex++;

            if (targetIndex >= path.Count)
            {
                targetIndex = 1;
                transform.position = path.GetWaypoint(0).position;
            }
        }
    }
}