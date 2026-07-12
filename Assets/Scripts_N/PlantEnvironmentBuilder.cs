using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Adds a lightweight industrial plant environment around the existing
/// Power-to-Methanol process equipment.
///
/// The environment is generated from Unity primitives so the scene can look
/// more like a real plant without adding heavy external asset dependencies.
/// </summary>
[DisallowMultipleComponent]
public class PlantEnvironmentBuilder : MonoBehaviour
{
    private const string EnvironmentRootName = "Generated_Plant_Environment_N";

    [Header("Generation")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private float minimumSiteWidth = 78f;
    [SerializeField] private float minimumSiteDepth = 46f;
    [SerializeField] private float sitePadding = 16f;
    [SerializeField] private float groundThickness = 0.25f;
    [SerializeField] private float environmentYOffset = -0.08f;

    [Header("Industrial Details")]
    [SerializeField] private bool createPerimeterFence = true;
    [SerializeField] private bool createPipeRack = true;
    [SerializeField] private bool createRoadsAndMarkings = true;
    [SerializeField] private bool createUtilityZone = true;
    [SerializeField] private bool createStorageContainment = true;
    [SerializeField] private bool createIndustrialBackground = true;

    private Material concreteMaterial;
    private Material asphaltMaterial;
    private Material safetyYellowMaterial;
    private Material steelMaterial;
    private Material fenceMaterial;
    private Material grassMaterial;
    private Material controlRoomMaterial;
    private Material pipeBlueMaterial;
    private Material pipeGreenMaterial;
    private Material pipePurpleMaterial;
    private Material hazardMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildRuntimeEnvironment()
    {
        if (FindFirstObjectByType<PlantEnvironmentBuilder>() != null)
        {
            return;
        }

        var builderObject = new GameObject("Plant Environment Builder (Runtime)");
        var builder = builderObject.AddComponent<PlantEnvironmentBuilder>();
        builder.rebuildOnStart = false;
        builder.BuildEnvironment();
    }

    private void Start()
    {
        if (Application.isPlaying && rebuildOnStart)
        {
            BuildEnvironment();
        }
    }

    [ContextMenu("Build Plant Environment")]
    public void BuildEnvironment()
    {
        ClearEnvironment();
        CreateMaterials();

        Bounds plantBounds = CalculatePlantBounds();
        Vector3 center = plantBounds.center;
        float siteWidth = Mathf.Max(minimumSiteWidth, plantBounds.size.x + sitePadding * 2f);
        float siteDepth = Mathf.Max(minimumSiteDepth, plantBounds.size.z + sitePadding * 2f);
        // Keep the generated site at floor level. Some runtime objects such as flow
        // lines and UI helpers have high renderer bounds, which previously caused a
        // giant slab to be generated through the middle of the plant.
        float baseY = Mathf.Min(plantBounds.min.y + environmentYOffset, -0.12f);

        GameObject root = new GameObject(EnvironmentRootName);

        CreateGroundAndPads(root.transform, center, siteWidth, siteDepth, baseY);

        if (createRoadsAndMarkings)
        {
            CreateRoadsAndSafetyMarkings(root.transform, center, siteWidth, siteDepth, baseY);
        }

        if (createPerimeterFence)
        {
            CreatePerimeterFence(root.transform, center, siteWidth, siteDepth, baseY);
        }

        if (createPipeRack)
        {
            CreatePipeRack(root.transform, center, siteWidth, siteDepth, baseY);
        }

        if (createUtilityZone)
        {
            CreateUtilityZone(root.transform, center, siteWidth, siteDepth, baseY);
        }

        if (createStorageContainment)
        {
            CreateStorageContainment(root.transform, center, siteWidth, siteDepth, baseY);
        }

        if (createIndustrialBackground)
        {
            CreateIndustrialBackground(root.transform, center, siteWidth, siteDepth, baseY);
        }

        CreateSiteSign(root.transform, center, siteWidth, siteDepth, baseY);
        SuppressLargeFloatingPlanes(baseY);
    }

    [ContextMenu("Clear Plant Environment")]
    public void ClearEnvironment()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject existing in allObjects)
        {
            if (existing == null)
            {
                continue;
            }

            if (existing.name == EnvironmentRootName ||
                existing.name == "Generated Plant Environment" ||
                existing.name.StartsWith("Generated_Plant_Environment"))
            {
                if (Application.isPlaying)
                {
                    Destroy(existing);
                }
                else
                {
                    DestroyImmediate(existing);
                }
            }
        }
    }

    private Bounds CalculatePlantBounds()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || ShouldIgnoreRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds || bounds.size.magnitude < 1f)
        {
            bounds = new Bounds(Vector3.zero, new Vector3(minimumSiteWidth * 0.55f, 10f, minimumSiteDepth * 0.55f));
        }

        return bounds;
    }

    private bool ShouldIgnoreRenderer(Renderer renderer)
    {
        Transform current = renderer.transform;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName.Contains(EnvironmentRootName) ||
                objectName.Contains("Generated Whole Plant Flow") ||
                objectName.Contains("Generated Interactive Module") ||
                objectName.Contains("Generated Reactor Detail") ||
                objectName.Contains("Generated Plant Process") ||
                objectName.Contains("Plant Environment Builder") ||
                objectName.Contains("FlowParticle") ||
                objectName.Contains("Flow_") ||
                objectName.Contains("ThinGuide_") ||
                objectName.Contains("Particle") ||
                objectName.Contains("Label") ||
                objectName.Contains("TMP") ||
                objectName.Contains("Canvas"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void CreateGroundAndPads(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        CreateCube("Site Grass Apron", root, new Vector3(center.x, baseY - 0.18f, center.z),
            new Vector3(siteWidth + 18f, groundThickness, siteDepth + 16f), grassMaterial);

        CreateCube("Main Concrete Plant Slab", root, new Vector3(center.x, baseY, center.z),
            new Vector3(siteWidth, groundThickness, siteDepth), concreteMaterial);

        float padY = baseY + groundThickness * 0.55f;
        CreateCube("Electrolyzer Capture Equipment Pad", root,
            new Vector3(center.x - siteWidth * 0.25f, padY, center.z),
            new Vector3(siteWidth * 0.36f, 0.08f, siteDepth * 0.58f), concreteMaterial);

        CreateCube("Synthesis Reactor Equipment Pad", root,
            new Vector3(center.x + siteWidth * 0.12f, padY, center.z),
            new Vector3(siteWidth * 0.28f, 0.08f, siteDepth * 0.58f), concreteMaterial);

        CreateCube("Separation Storage Equipment Pad", root,
            new Vector3(center.x + siteWidth * 0.36f, padY, center.z),
            new Vector3(siteWidth * 0.24f, 0.08f, siteDepth * 0.58f), concreteMaterial);
    }

    private void CreateIndustrialBackground(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float rearZ = center.z + siteDepth * 0.58f;
        float farZ = center.z + siteDepth * 0.78f;
        float midZ = center.z + siteDepth * 0.48f;
        float leftX = center.x - siteWidth * 0.42f;
        float rightX = center.x + siteWidth * 0.42f;

        CreateCube("Background Process Hall", root,
            new Vector3(leftX + 7f, baseY + 3.2f, rearZ),
            new Vector3(12f, 6.4f, 5.5f), controlRoomMaterial);
        CreateCube("Background Process Hall Roof", root,
            new Vector3(leftX + 7f, baseY + 6.55f, rearZ),
            new Vector3(12.8f, 0.28f, 6.1f), steelMaterial);

        CreateCube("Background Pipe Bridge A", root,
            new Vector3(center.x, baseY + 6.2f, rearZ - 1.8f),
            new Vector3(siteWidth * 0.74f, 0.28f, 0.28f), steelMaterial);
        CreateCube("Background Pipe Bridge B", root,
            new Vector3(center.x, baseY + 5.55f, rearZ - 2.35f),
            new Vector3(siteWidth * 0.68f, 0.22f, 0.22f), steelMaterial);

        CreateHorizontalCylinder("Background Steam Header", root,
            new Vector3(center.x, baseY + 6.65f, rearZ - 1.2f),
            0.12f, siteWidth * 0.7f, pipeBlueMaterial, true);
        CreateHorizontalCylinder("Background Utility Header", root,
            new Vector3(center.x, baseY + 5.95f, rearZ - 2.7f),
            0.1f, siteWidth * 0.62f, pipeGreenMaterial, true);

        CreateCube("Rear Industrial Boundary Wall", root,
            new Vector3(center.x, baseY + 1.45f, midZ + 2.5f),
            new Vector3(siteWidth * 0.92f, 2.9f, 0.28f), controlRoomMaterial);

        CreateCube("Left Workshop Building", root,
            new Vector3(leftX + 3.2f, baseY + 2.15f, midZ - 3.5f),
            new Vector3(7.8f, 4.3f, 6.4f), controlRoomMaterial);
        CreateCube("Right Compressor Shelter", root,
            new Vector3(rightX - 6.5f, baseY + 2.55f, midZ - 2.2f),
            new Vector3(9.5f, 5.1f, 5.2f), controlRoomMaterial);
        CreateCube("Right Compressor Shelter Roof", root,
            new Vector3(rightX - 6.5f, baseY + 5.25f, midZ - 2.2f),
            new Vector3(10.2f, 0.25f, 5.8f), steelMaterial);

        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(leftX + 10f, rightX - 10f, i / 5f);
            float z = midZ + (i % 2 == 0 ? -0.6f : 1.2f);
            CreateCube("Background Cable Tray Post", root,
                new Vector3(x, baseY + 2.25f, z),
                new Vector3(0.16f, 4.5f, 0.16f), steelMaterial);
        }

        CreateCube("Background Cable Tray", root,
            new Vector3(center.x, baseY + 4.6f, midZ + 0.3f),
            new Vector3(siteWidth * 0.76f, 0.18f, 0.35f), hazardMaterial);

        for (int i = 0; i < 5; i++)
        {
            float x = Mathf.Lerp(leftX + 18f, rightX - 10f, i / 4f);
            float height = i % 2 == 0 ? 8.5f : 6.8f;
            CreateCylinder("Background Utility Vessel", root,
                new Vector3(x, baseY + height * 0.5f, farZ),
                0.85f, height, steelMaterial, Quaternion.identity);
            CreateCube("Background Vessel Platform", root,
                new Vector3(x, baseY + height * 0.68f, farZ),
                new Vector3(2.4f, 0.16f, 2.4f), safetyYellowMaterial);
        }

        for (int i = 0; i < 4; i++)
        {
            float x = rightX - 2.5f - i * 2.2f;
            CreateCylinder("Background Vent Stack", root,
                new Vector3(x, baseY + 5.2f, rearZ + 2.8f),
                0.2f, 10.4f, steelMaterial, Quaternion.identity);
            CreateCylinder("Background Stack Cap", root,
                new Vector3(x, baseY + 10.65f, rearZ + 2.8f),
                0.32f, 0.35f, hazardMaterial, Quaternion.identity);
        }

        CreateRefineryTankFarm(root, center, siteWidth, siteDepth, baseY);
        CreateDensePipeCorridors(root, center, siteWidth, siteDepth, baseY);
        CreateScaffoldAndServiceFrames(root, center, siteWidth, siteDepth, baseY);
    }

    private void CreateRefineryTankFarm(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float rearZ = center.z + siteDepth * 0.67f;
        float frontZ = center.z - siteDepth * 0.42f;
        float leftX = center.x - siteWidth * 0.43f;
        float rightX = center.x + siteWidth * 0.43f;

        for (int i = 0; i < 4; i++)
        {
            float x = leftX + 3.2f + i * 3.4f;
            float radius = i % 2 == 0 ? 0.9f : 0.72f;
            float height = i % 2 == 0 ? 4.4f : 3.5f;
            CreateCylinder("Left Background Storage Tank", root,
                new Vector3(x, baseY + height * 0.5f, rearZ - 2.2f),
                radius, height, steelMaterial, Quaternion.identity);
            CreateCylinder("Left Background Tank Roof", root,
                new Vector3(x, baseY + height + 0.12f, rearZ - 2.2f),
                radius * 1.03f, 0.24f, hazardMaterial, Quaternion.identity);
            CreateCube("Left Tank Bund Wall", root,
                new Vector3(x, baseY + 0.25f, rearZ - 0.95f),
                new Vector3(2.2f, 0.5f, 0.16f), concreteMaterial);
        }

        for (int i = 0; i < 5; i++)
        {
            float x = rightX - 2.8f - i * 2.5f;
            float height = 3.2f + (i % 3) * 0.7f;
            CreateCylinder("Right Background Vertical Vessel", root,
                new Vector3(x, baseY + height * 0.5f, frontZ),
                0.55f, height, steelMaterial, Quaternion.identity);
            CreateHorizontalCylinder("Right Vessel Connection Header", root,
                new Vector3(x, baseY + height * 0.72f, frontZ + 1.05f),
                0.07f, 2.0f, pipeBlueMaterial, false);
        }
    }

    private void CreateDensePipeCorridors(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float rearZ = center.z + siteDepth * 0.53f;
        float frontZ = center.z - siteDepth * 0.31f;
        float leftX = center.x - siteWidth * 0.44f;
        float rightX = center.x + siteWidth * 0.44f;
        float pipeLength = siteWidth * 0.82f;

        for (int row = 0; row < 3; row++)
        {
            float z = rearZ - row * 1.05f;
            float y = baseY + 3.9f + row * 0.42f;
            Material pipeMaterial = row == 0 ? pipeBlueMaterial : row == 1 ? pipeGreenMaterial : pipePurpleMaterial;
            CreateHorizontalCylinder("Dense Rear Pipe Corridor", root,
                new Vector3(center.x, y, z), 0.075f, pipeLength, pipeMaterial, true);
        }

        for (int i = 0; i < 9; i++)
        {
            float x = Mathf.Lerp(leftX + 2f, rightX - 2f, i / 8f);
            CreateCube("Dense Pipe Rack Upright", root,
                new Vector3(x, baseY + 2.0f, rearZ - 1.05f),
                new Vector3(0.16f, 4.0f, 0.16f), steelMaterial);
            CreateCube("Dense Pipe Rack Crossbeam", root,
                new Vector3(x, baseY + 4.18f, rearZ - 1.05f),
                new Vector3(0.22f, 0.14f, 2.6f), steelMaterial);
        }

        for (int i = 0; i < 5; i++)
        {
            float x = center.x - siteWidth * 0.34f + i * siteWidth * 0.17f;
            CreateHorizontalCylinder("Foreground Utility Pipe", root,
                new Vector3(x, baseY + 1.05f, frontZ),
                0.065f, siteDepth * 0.22f, i % 2 == 0 ? pipeGreenMaterial : pipeBlueMaterial, false);
            CreateCube("Foreground Pipe Support", root,
                new Vector3(x, baseY + 0.5f, frontZ),
                new Vector3(0.18f, 1.0f, 0.18f), steelMaterial);
        }
    }

    private void CreateScaffoldAndServiceFrames(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float rearZ = center.z + siteDepth * 0.41f;
        float leftX = center.x - siteWidth * 0.37f;
        float rightX = center.x + siteWidth * 0.34f;

        CreateServiceFrame(root, new Vector3(leftX, baseY, rearZ), 5.5f, 3.4f, 5.4f);
        CreateServiceFrame(root, new Vector3(rightX, baseY, rearZ - 1.8f), 6.6f, 3.8f, 4.8f);

        CreateCube("Background Control Building Wide Block", root,
            new Vector3(center.x - siteWidth * 0.08f, baseY + 2.2f, rearZ + 5.2f),
            new Vector3(14f, 4.4f, 4.6f), controlRoomMaterial);
        CreateCube("Background Control Building Roof", root,
            new Vector3(center.x - siteWidth * 0.08f, baseY + 4.55f, rearZ + 5.2f),
            new Vector3(14.6f, 0.24f, 5.1f), steelMaterial);

        for (int i = 0; i < 6; i++)
        {
            float x = center.x - 6f + i * 2.4f;
            CreateCube("Small Equipment Plinth", root,
                new Vector3(x, baseY + 0.32f, rearZ + 1.9f),
                new Vector3(1.25f, 0.64f, 1.1f), concreteMaterial);
            CreateCube("Small Skid Package", root,
                new Vector3(x, baseY + 0.92f, rearZ + 1.9f),
                new Vector3(1.0f, 0.72f, 0.85f), steelMaterial);
        }
    }

    private void CreateServiceFrame(Transform root, Vector3 basePosition, float width, float depth, float height)
    {
        float postHalfHeight = height * 0.5f;
        for (int ix = 0; ix < 2; ix++)
        {
            for (int iz = 0; iz < 2; iz++)
            {
                float x = basePosition.x + (ix == 0 ? -width * 0.5f : width * 0.5f);
                float z = basePosition.z + (iz == 0 ? -depth * 0.5f : depth * 0.5f);
                CreateCube("Refinery Scaffold Post", root,
                    new Vector3(x, basePosition.y + postHalfHeight, z),
                    new Vector3(0.16f, height, 0.16f), steelMaterial);
            }
        }

        CreateCube("Refinery Scaffold Deck", root,
            new Vector3(basePosition.x, basePosition.y + height * 0.55f, basePosition.z),
            new Vector3(width + 0.4f, 0.12f, depth + 0.4f), safetyYellowMaterial);
        CreateCube("Refinery Scaffold Top Beam X", root,
            new Vector3(basePosition.x, basePosition.y + height, basePosition.z - depth * 0.5f),
            new Vector3(width, 0.14f, 0.14f), steelMaterial);
        CreateCube("Refinery Scaffold Top Beam X", root,
            new Vector3(basePosition.x, basePosition.y + height, basePosition.z + depth * 0.5f),
            new Vector3(width, 0.14f, 0.14f), steelMaterial);
        CreateCube("Refinery Scaffold Top Beam Z", root,
            new Vector3(basePosition.x - width * 0.5f, basePosition.y + height, basePosition.z),
            new Vector3(0.14f, 0.14f, depth), steelMaterial);
        CreateCube("Refinery Scaffold Top Beam Z", root,
            new Vector3(basePosition.x + width * 0.5f, basePosition.y + height, basePosition.z),
            new Vector3(0.14f, 0.14f, depth), steelMaterial);
    }

    private void SuppressLargeFloatingPlanes(float baseY)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || ShouldIgnoreRenderer(renderer))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            bool largeFlatSlab = bounds.size.x > 24f && bounds.size.z > 8f && bounds.size.y < 1.4f;
            bool floatingAbovePlant = bounds.center.y > baseY + 2.0f;
            bool likelyScenePlane = LooksLikeLoosePlane(renderer.transform);

            if (largeFlatSlab && floatingAbovePlant && likelyScenePlane)
            {
                renderer.gameObject.SetActive(false);
                Debug.Log($"PlantEnvironmentBuilder: disabled likely floating plane '{renderer.gameObject.name}' at y={bounds.center.y:F2}.");
            }
        }
    }

    private bool LooksLikeLoosePlane(Transform transformToCheck)
    {
        Transform current = transformToCheck;
        while (current != null)
        {
            string objectName = current.name.ToLowerInvariant();
            if (objectName.Contains("cube") ||
                objectName.Contains("plane") ||
                objectName.Contains("slab") ||
                objectName.Contains("ground") ||
                objectName.Contains("pad"))
            {
                return true;
            }

            if (objectName.Contains("reactor") ||
                objectName.Contains("absorber") ||
                objectName.Contains("desorber") ||
                objectName.Contains("compressor") ||
                objectName.Contains("condenser") ||
                objectName.Contains("distillation") ||
                objectName.Contains("tank") ||
                objectName.Contains("pipe") ||
                objectName.Contains("piping"))
            {
                return false;
            }

            current = current.parent;
        }

        return false;
    }

    private void CreateRoadsAndSafetyMarkings(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float roadY = baseY + groundThickness * 0.72f;
        float frontZ = center.z - siteDepth * 0.37f;
        float rearZ = center.z + siteDepth * 0.36f;

        CreateCube("Front Service Road", root, new Vector3(center.x, roadY, frontZ),
            new Vector3(siteWidth * 0.92f, 0.06f, 4.2f), asphaltMaterial);

        CreateCube("Rear Maintenance Road", root, new Vector3(center.x, roadY, rearZ),
            new Vector3(siteWidth * 0.82f, 0.06f, 3.2f), asphaltMaterial);

        CreateCube("Left Access Road", root, new Vector3(center.x - siteWidth * 0.43f, roadY, center.z),
            new Vector3(3.2f, 0.06f, siteDepth * 0.62f), asphaltMaterial);

        CreateCube("Process Walkway", root, new Vector3(center.x, roadY + 0.05f, center.z - siteDepth * 0.19f),
            new Vector3(siteWidth * 0.72f, 0.06f, 1.1f), safetyYellowMaterial);

        CreateCube("Central Operator Walkway", root, new Vector3(center.x, roadY + 0.05f, center.z + siteDepth * 0.07f),
            new Vector3(siteWidth * 0.7f, 0.06f, 0.9f), safetyYellowMaterial);

        for (int i = 0; i < 12; i++)
        {
            float x = center.x - siteWidth * 0.42f + i * siteWidth * 0.076f;
            float z = frontZ;
            CreateCube("Road Lane Dash", root, new Vector3(x, roadY + 0.07f, z),
                new Vector3(2.4f, 0.03f, 0.12f), hazardMaterial);
        }
    }

    private void CreatePerimeterFence(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float halfWidth = siteWidth * 0.5f;
        float halfDepth = siteDepth * 0.5f;
        float fenceY = baseY + 1.1f;
        float postSpacing = 5f;

        CreateFenceLine(root, new Vector3(center.x - halfWidth, fenceY, center.z - halfDepth),
            new Vector3(center.x + halfWidth, fenceY, center.z - halfDepth), postSpacing, true);
        CreateFenceLine(root, new Vector3(center.x - halfWidth, fenceY, center.z + halfDepth),
            new Vector3(center.x + halfWidth, fenceY, center.z + halfDepth), postSpacing, true);
        CreateFenceLine(root, new Vector3(center.x - halfWidth, fenceY, center.z - halfDepth),
            new Vector3(center.x - halfWidth, fenceY, center.z + halfDepth), postSpacing, false);
        CreateFenceLine(root, new Vector3(center.x + halfWidth, fenceY, center.z - halfDepth),
            new Vector3(center.x + halfWidth, fenceY, center.z + halfDepth), postSpacing, false);
    }

    private void CreateFenceLine(Transform root, Vector3 start, Vector3 end, float spacing, bool alongX)
    {
        float length = Vector3.Distance(start, end);
        int postCount = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
        Vector3 direction = (end - start).normalized;

        for (int i = 0; i < postCount; i++)
        {
            Vector3 postPosition = start + direction * (length * i / (postCount - 1));
            CreateCylinder("Fence Post", root, postPosition, 0.05f, 2.2f, fenceMaterial, Quaternion.identity);
        }

        Vector3 railCenter = (start + end) * 0.5f;
        Vector3 railSize = alongX ? new Vector3(length, 0.08f, 0.08f) : new Vector3(0.08f, 0.08f, length);
        CreateCube("Upper Fence Rail", root, railCenter + Vector3.up * 0.75f, railSize, fenceMaterial);
        CreateCube("Lower Fence Rail", root, railCenter + Vector3.up * 0.05f, railSize, fenceMaterial);
    }

    private void CreatePipeRack(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float rackZ = center.z + siteDepth * 0.24f;
        float startX = center.x - siteWidth * 0.38f;
        float endX = center.x + siteWidth * 0.38f;
        float rackY = baseY + 3.2f;
        float postSpacing = 7f;
        int posts = Mathf.Max(3, Mathf.RoundToInt((endX - startX) / postSpacing));

        for (int i = 0; i <= posts; i++)
        {
            float x = Mathf.Lerp(startX, endX, i / (float)posts);
            CreateCube("Pipe Rack Left Post", root, new Vector3(x, baseY + 1.6f, rackZ - 1.1f),
                new Vector3(0.18f, 3.2f, 0.18f), steelMaterial);
            CreateCube("Pipe Rack Right Post", root, new Vector3(x, baseY + 1.6f, rackZ + 1.1f),
                new Vector3(0.18f, 3.2f, 0.18f), steelMaterial);
            CreateCube("Pipe Rack Cross Beam", root, new Vector3(x, rackY, rackZ),
                new Vector3(0.22f, 0.16f, 2.6f), steelMaterial);
        }

        float pipeLength = endX - startX;
        CreateHorizontalCylinder("Pipe Rack H2 Header", root, new Vector3(center.x, rackY + 0.28f, rackZ - 0.72f),
            0.09f, pipeLength, pipeGreenMaterial, true);
        CreateHorizontalCylinder("Pipe Rack CO2 Header", root, new Vector3(center.x, rackY + 0.52f, rackZ),
            0.09f, pipeLength, pipeBlueMaterial, true);
        CreateHorizontalCylinder("Pipe Rack Methanol Header", root, new Vector3(center.x, rackY + 0.76f, rackZ + 0.72f),
            0.09f, pipeLength, pipePurpleMaterial, true);
    }

    private void CreateUtilityZone(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float utilityX = center.x - siteWidth * 0.36f;
        float utilityZ = center.z + siteDepth * 0.33f;

        CreateCube("Control Room Block", root, new Vector3(utilityX, baseY + 1.05f, utilityZ),
            new Vector3(5.2f, 2.1f, 3.4f), controlRoomMaterial);
        CreateCube("Control Room Roof", root, new Vector3(utilityX, baseY + 2.22f, utilityZ),
            new Vector3(5.6f, 0.22f, 3.8f), steelMaterial);

        CreateCube("Electrical Utility Skid", root, new Vector3(utilityX + 6.2f, baseY + 0.65f, utilityZ - 0.2f),
            new Vector3(3.8f, 1.3f, 2.1f), steelMaterial);

        for (int i = 0; i < 4; i++)
        {
            CreateCylinder("Utility Vent Stack", root,
                new Vector3(utilityX + 8.6f + i * 0.55f, baseY + 1.15f, utilityZ + 1.55f),
                0.12f, 2.3f, steelMaterial, Quaternion.identity);
        }
    }

    private void CreateStorageContainment(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        float bundX = center.x + siteWidth * 0.34f;
        float bundZ = center.z - siteDepth * 0.14f;
        float wallY = baseY + 0.32f;
        float width = siteWidth * 0.23f;
        float depth = siteDepth * 0.25f;

        CreateCube("Storage Containment Floor", root, new Vector3(bundX, baseY + 0.06f, bundZ),
            new Vector3(width, 0.08f, depth), concreteMaterial);
        CreateCube("Storage Bund North Wall", root, new Vector3(bundX, wallY, bundZ + depth * 0.5f),
            new Vector3(width, 0.56f, 0.22f), hazardMaterial);
        CreateCube("Storage Bund South Wall", root, new Vector3(bundX, wallY, bundZ - depth * 0.5f),
            new Vector3(width, 0.56f, 0.22f), hazardMaterial);
        CreateCube("Storage Bund East Wall", root, new Vector3(bundX + width * 0.5f, wallY, bundZ),
            new Vector3(0.22f, 0.56f, depth), hazardMaterial);
        CreateCube("Storage Bund West Wall", root, new Vector3(bundX - width * 0.5f, wallY, bundZ),
            new Vector3(0.22f, 0.56f, depth), hazardMaterial);
    }

    private void CreateSiteSign(Transform root, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        Vector3 signPosition = new Vector3(center.x - siteWidth * 0.36f, baseY + 1.35f, center.z - siteDepth * 0.43f);
        CreateCube("Site Sign Backplate", root, signPosition, new Vector3(7.5f, 1.5f, 0.12f), steelMaterial);

        GameObject textObject = new GameObject("Site Sign Text");
        textObject.transform.SetParent(root);
        textObject.transform.position = signPosition + new Vector3(0f, 0.02f, -0.08f);
        textObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = "PtMeOH Plant Area";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.12f;
        textMesh.color = Color.white;
    }

    private GameObject CreateCube(string objectName, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        ApplyMaterial(cube, material);
        return cube;
    }

    private GameObject CreateCylinder(string objectName, Transform parent, Vector3 position, float radius, float height, Material material, Quaternion rotation)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = objectName;
        cylinder.transform.SetParent(parent);
        cylinder.transform.position = position;
        cylinder.transform.rotation = rotation;
        cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        ApplyMaterial(cylinder, material);
        return cylinder;
    }

    private void CreateHorizontalCylinder(string objectName, Transform parent, Vector3 position, float radius, float length, Material material, bool alongX)
    {
        Quaternion rotation = alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
        CreateCylinder(objectName, parent, position, radius, length, material, rotation);
    }

    private void ApplyMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void CreateMaterials()
    {
        concreteMaterial = CreateMaterial("Generated Concrete", new Color(0.46f, 0.48f, 0.48f));
        asphaltMaterial = CreateMaterial("Generated Asphalt", new Color(0.09f, 0.10f, 0.11f));
        safetyYellowMaterial = CreateMaterial("Generated Safety Yellow", new Color(1.0f, 0.72f, 0.08f));
        steelMaterial = CreateMaterial("Generated Brushed Steel", new Color(0.55f, 0.59f, 0.62f));
        fenceMaterial = CreateMaterial("Generated Fence Dark Steel", new Color(0.18f, 0.21f, 0.23f));
        grassMaterial = CreateMaterial("Generated Distant Ground", new Color(0.18f, 0.29f, 0.20f));
        controlRoomMaterial = CreateMaterial("Generated Control Room Blue Grey", new Color(0.18f, 0.26f, 0.32f));
        pipeBlueMaterial = CreateMaterial("Generated CO2 Pipe Blue", new Color(0.16f, 0.62f, 0.95f));
        pipeGreenMaterial = CreateMaterial("Generated H2 Pipe Green", new Color(0.24f, 0.82f, 0.38f));
        pipePurpleMaterial = CreateMaterial("Generated Methanol Pipe Purple", new Color(0.55f, 0.28f, 0.88f));
        hazardMaterial = CreateMaterial("Generated Hazard Marking", new Color(0.95f, 0.64f, 0.07f));
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        return material;
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Nived/Build Plant Environment")]
    private static void BuildEnvironmentFromMenu()
    {
        PlantEnvironmentBuilder builder = FindFirstObjectByType<PlantEnvironmentBuilder>();
        if (builder == null)
        {
            GameObject builderObject = new GameObject("Plant Environment Builder");
            builder = builderObject.AddComponent<PlantEnvironmentBuilder>();
        }

        builder.BuildEnvironment();
        EditorUtility.SetDirty(builder);
    }
#endif
}
