using UnityEngine;

/// <summary>
/// Optional per-pipe correction layer.
///
/// Add this to any pipe if the automatic route detection is mostly right but
/// one object needs a different direction, color, speed, density, opacity, or
/// flow state. This lets teammates fix individual mistakes from the Inspector
/// without editing code.
/// </summary>
[DisallowMultipleComponent]
public class PipeFlowManualOverride : MonoBehaviour
{
    [Header("Enable Manual Override")]
    public bool useOverride = false;

    [Header("Flow Type")]
    public bool overrideFlowKind = false;
    public PlantFlowKind flowKind = PlantFlowKind.MixedFeed;

    [Header("Appearance")]
    public bool overrideColor = false;
    public Color color = Color.white;
    public bool overrideDensity = false;
    [Min(0.5f)] public float density = 22f;
    public bool overridePipeAlpha = false;
    [Range(0f, 1f)] public float pipeAlpha = 0.22f;
    public bool overrideFlowIntensity = false;
    [Range(0f, 2f)] public float flowIntensity = 1.25f;

    [Header("Movement")]
    public bool overrideSpeed = false;
    [Min(0f)] public float speed = 2.2f;
    public bool overrideDirection = false;
    public bool reverseDirection = false;

    [Header("State")]
    public bool forceOff = false;
    public string engineeringNote;
}
