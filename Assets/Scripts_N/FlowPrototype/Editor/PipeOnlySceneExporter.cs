#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only utility that extracts the existing pipe objects from the active
/// whole-plant scene into a separate lightweight pipe-only scene.
///
/// This preserves the real pipe layout instead of rebuilding an approximate
/// layout from code. It keeps references to the existing pipe meshes/materials,
/// so it is much lighter than duplicating the full plant scene.
/// </summary>
public static class PipeOnlySceneExporter
{
    private const string PipeOnlyScenePath = "Assets/Scenes/PipeOnlyFlowPrototype.unity";
    private const string PipeOnlyPackagePath = "PipeOnlyFlowPrototype.unitypackage";
    private static readonly HashSet<string> ExactPipeObjectNames = new HashSet<string>
    {
        "Cylinder",
        "Cylinder (2)",
        "Cylinder (3)",
        "Cylinder (4)",
        "Cylinder (5)",
        "Cylinder (6)",
        "Cylinder (7)",
        "Cylinder (8)",
        "Cylinder (9)",
        "Cylinder (10)",
        "Cylinder (11)",
        "Cylinder (12)",
        "Cylinder (13)",
        "Cylinder (15)",
        "Cylinder (16)",
        "Cylinder (17)",
        "Cylinder (18)",
        "Cylinder (19)",
        "Cylinder (20)",
        "Cylinder (21)",
        "Cylinder (22)",
        "Cylinder (23)",
        "Cylinder (24)",
        "Cylinder (25)",
        "Cylinder (26)",
        "Cylinder (27)",
        "Cylinder (28)",
        "Cylinder (29)",
        "Cylinder (30)",
        "Cylinder (31)",
        "Cylinder (32)",
        "Cylinder (33)",
        "Cylinder (34)",
        "Cylinder (35)",
        "pipe bend (1)",
        "pipe bend (2)",
        "pipe bend (3)",
        "pipe bend (4)",
        "pipe bend (5)",
        "pipe bend (6)",
        "pipe bend (7)",
        "pipe bend (8)",
        "pipe bend (9)",
        "pipe bend (10)",
        "pipe bend (12)",
        "pipe bend (13)",
        "pipe bend (14)",
        "pipe bend (15)",
        "pipe bend (16)",
        "pipe bend (17)",
        "pipe bend (18)",
        "pipe bend (19)",
        "pipe bend (20)",
        "pipe bend (21)",
        "pipe bend (22)",
        "pipe bend (23)",
        "pipe bend (24)",
        "pipe bend (25)",
        "pipe bend (26)",
        "pipe bend (27)",
        "pipe bend (28)",
        "pipe bend (29)",
        "t junction"
    };

    [MenuItem("Tools/Nived/Flow Prototype/Create Pipe-Only Scene From Current Scene")]
    public static void CreatePipeOnlySceneFromCurrentScene()
    {
        Scene sourceScene = SceneManager.GetActiveScene();
        if (!sourceScene.IsValid())
        {
            EditorUtility.DisplayDialog("Pipe-Only Export", "No valid source scene is open.", "OK");
            return;
        }

        List<GameObject> sourcePipeRoots = FindPipeRoots(sourceScene);
        if (sourcePipeRoots.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Pipe-Only Export",
                "No pipe objects were found. Expected names like 'Piping', 'pipe bend', 'Cylinder (...)', or 't junction'.",
                "OK");
            return;
        }

        Scene pipeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("PipeOnly_Extracted_From_" + sourceScene.name);

        foreach (GameObject sourceObject in sourcePipeRoots)
        {
            GameObject clone = Object.Instantiate(sourceObject);
            clone.name = RemoveCloneSuffix(sourceObject.name);
            clone.transform.SetParent(root.transform, true);
            clone.transform.position = sourceObject.transform.position;
            clone.transform.rotation = sourceObject.transform.rotation;
            clone.transform.localScale = sourceObject.transform.lossyScale;
            StripUnwantedComponents(clone);
        }

        AddPrototypeFlowRuntime(root);
        AddSimpleEnvironment();
        AddCameraAndLighting();
        AddInstructionLabel();

        EditorSceneManager.SaveScene(pipeScene, PipeOnlyScenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Pipe-Only Export Complete",
            "Created pipe-only scene:\n" + PipeOnlyScenePath + "\n\nOpen it and press Play to test the flow without the full plant model.",
            "OK");
    }

    [MenuItem("Tools/Nived/Flow Prototype/Export Pipe-Only UnityPackage")]
    public static void ExportPipeOnlyUnityPackage()
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(PipeOnlyScenePath))
        {
            EditorUtility.DisplayDialog(
                "Pipe-Only Package",
                "PipeOnlyFlowPrototype.unity was not found. Create the pipe-only scene first.",
                "OK");
            return;
        }

        AssetDatabase.ExportPackage(
            PipeOnlyScenePath,
            PipeOnlyPackagePath,
            ExportPackageOptions.IncludeDependencies | ExportPackageOptions.Recurse);

        EditorUtility.DisplayDialog(
            "Pipe-Only Package Exported",
            "Exported package:\n" + PipeOnlyPackagePath + "\n\nThis can be imported into a separate Unity project if needed.",
            "OK");
    }

    private static List<GameObject> FindPipeRoots(Scene sourceScene)
    {
        List<GameObject> rootsToCopy = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();
        GameObject[] sceneRoots = sourceScene.GetRootGameObjects();

        foreach (GameObject root in sceneRoots)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (!IsPipeCandidate(transform))
                {
                    continue;
                }

                GameObject topMostPipe = FindTopMostPipeCandidate(transform).gameObject;
                if (seen.Add(topMostPipe))
                {
                    rootsToCopy.Add(topMostPipe);
                }
            }
        }

        rootsToCopy.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return rootsToCopy;
    }

    private static Transform FindTopMostPipeCandidate(Transform transform)
    {
        Transform current = transform;
        Transform best = transform;

        while (current.parent != null)
        {
            current = current.parent;
            if (IsStrongPipeContainer(current))
            {
                best = current;
            }
        }

        return best;
    }

    private static bool IsStrongPipeContainer(Transform transform)
    {
        string lower = transform.name.ToLowerInvariant();
        return lower == "piping" || lower == "pipes" || lower == "pipe system" || lower == "pipe network";
    }

    private static bool IsPipeCandidate(Transform transform)
    {
        string lower = transform.name.ToLowerInvariant();

        if (lower.Contains("generated") ||
            lower.Contains("flow") ||
            lower.Contains("camera") ||
            lower.Contains("canvas") ||
            lower.Contains("light"))
        {
            return false;
        }

        if (IsStrongPipeContainer(transform))
        {
            return true;
        }

        return ExactPipeObjectNames.Contains(transform.name);
    }

    private static void StripUnwantedComponents(GameObject target)
    {
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }

        foreach (MonoBehaviour behaviour in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
            {
                continue;
            }

            if (behaviour is AutoWholePlantFlowRuntime)
            {
                continue;
            }

            Object.DestroyImmediate(behaviour);
        }
    }

    private static void AddPrototypeFlowRuntime(GameObject root)
    {
        AutoWholePlantFlowRuntime flowRuntime = root.AddComponent<AutoWholePlantFlowRuntime>();
        flowRuntime.globalSpeed = 1.15f;
        flowRuntime.particleSize = 0.19f;
        flowRuntime.lineWidth = 0.09f;
        flowRuntime.pipeAlpha = 0.18f;
        flowRuntime.makePipesTransparent = true;
        flowRuntime.showThinGuideLines = true;
        flowRuntime.scaleFlowWithProcessQuantities = false;
    }

    private static void AddSimpleEnvironment()
    {
        Material floorMaterial = CreateMaterial("PipeOnly Dark Floor", new Color(0.06f, 0.07f, 0.08f, 1f));
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "PipeOnly Simple Floor";
        floor.transform.position = new Vector3(4f, -0.16f, -1f);
        floor.transform.localScale = new Vector3(46f, 0.08f, 18f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        Object.DestroyImmediate(floor.GetComponent<Collider>());
    }

    private static void AddCameraAndLighting()
    {
        GameObject cameraObject = new GameObject("PipeOnly Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(8f, 24f, 38f);
        camera.transform.rotation = Quaternion.Euler(32f, 180f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 34f;
        camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        GameObject lightObject = new GameObject("PipeOnly Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(45f, -35f, 20f);
    }

    private static void AddInstructionLabel()
    {
        GameObject labelObject = new GameObject("PipeOnly Instructions");
        labelObject.transform.position = new Vector3(-15f, 3.2f, -7f);
        labelObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = "Pipe-only flow prototype\nSame pipe layout, no heavy plant model";
        label.fontSize = 64;
        label.characterSize = 0.09f;
        label.anchor = TextAnchor.MiddleLeft;
        label.alignment = TextAlignment.Left;
        label.color = Color.white;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = name,
            color = color
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }

    private static string RemoveCloneSuffix(string name)
    {
        return name.Replace("(Clone)", string.Empty).Trim();
    }
}
#endif
