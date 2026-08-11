using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-shot, repeatable migration of stable runtime-authored structures to
/// editable project prefabs. Dynamic simulation and route topology remain in code.
/// </summary>
public static class RuntimePrefabConversion
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RuntimeFolder = "Assets/Prefabs_N/Runtime";
    private const string UiFolder = RuntimeFolder + "/UI";
    private const string SystemsFolder = RuntimeFolder + "/Systems";
    private const string EnvironmentFolder = "Assets/Prefabs_N/Environment";
    private const string EnvironmentMaterialFolder = "Assets/Materials_N/Environment";
    private const string OverlayPrefabPath = UiFolder + "/SafetyWarningOverlay.prefab";
    private const string ServicesPrefabPath = SystemsFolder + "/PlantRuntimeServices.prefab";
    private const string EnvironmentPrefabPath = EnvironmentFolder + "/IndustrialPlantEnvironment.prefab";

    [MenuItem("Tools/Power-to-Methanol/Convert Runtime Structures to Prefabs")]
    public static void Convert()
    {
        EnsureFolder(RuntimeFolder);
        EnsureFolder(UiFolder);
        EnsureFolder(SystemsFolder);
        EnsureFolder(EnvironmentFolder);
        EnsureFolder(EnvironmentMaterialFolder);

        GameObject overlayPrefab = CreateSafetyOverlayPrefab();
        GameObject servicesPrefab = CreateRuntimeServicesPrefab(overlayPrefab);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveLegacyServiceComponents();
        ReplaceNamedRoot("Plant Runtime Services", servicesPrefab);
        BakeEnvironmentPrefab();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("RUNTIME_PREFAB_CONVERSION_OK: runtime services, warning UI, and industrial environment are prefab-backed.");
    }

    private static GameObject CreateSafetyOverlayPrefab()
    {
        GameObject root = new GameObject("Safety Warning Overlay");
        try
        {
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 72;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1536f, 1024f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = new GameObject("Warning Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(16f, -88f);
            panelRect.sizeDelta = new Vector2(710f, 48f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.raycastTarget = false;
            panelImage.color = new Color(0.12f, 0.04f, 0.02f, 0.82f);

            GameObject textObject = new GameObject("Warning Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.color = Color.white;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 8f);
            textRect.offsetMax = new Vector2(-14f, -8f);
            panel.SetActive(false);

            return PrefabUtility.SaveAsPrefabAsset(root, OverlayPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateRuntimeServicesPrefab(GameObject overlayPrefab)
    {
        GameObject root = new GameObject("Plant Runtime Services");
        try
        {
            root.AddComponent<PlantProcessSimulator>();
            root.AddComponent<FinalPlantFlowRuntime>();
            root.AddComponent<IcodosDashboardRuntime>();
            root.AddComponent<InteractiveModulePanelRuntime>();
            SafetyWarningRuntime warning = root.AddComponent<SafetyWarningRuntime>();
            PlantEnvironmentBuilder environment = root.AddComponent<PlantEnvironmentBuilder>();

            SerializedObject warningObject = new SerializedObject(warning);
            warningObject.FindProperty("overlayPrefab").objectReferenceValue = overlayPrefab;
            warningObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject environmentObject = new SerializedObject(environment);
            environmentObject.FindProperty("rebuildOnStart").boolValue = false;
            environmentObject.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, ServicesPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void RemoveLegacyServiceComponents()
    {
        Type[] serviceTypes =
        {
            typeof(PlantProcessSimulator), typeof(FinalPlantFlowRuntime), typeof(IcodosDashboardRuntime),
            typeof(InteractiveModulePanelRuntime), typeof(SafetyWarningRuntime), typeof(PlantEnvironmentBuilder)
        };

        foreach (Type type in serviceTypes)
        {
            UnityEngine.Object[] components = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (UnityEngine.Object item in components)
            {
                Component component = item as Component;
                if (component != null && !EditorUtility.IsPersistent(component))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }
    }

    private static void ReplaceNamedRoot(string rootName, GameObject prefab)
    {
        GameObject existing = GameObject.Find(rootName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        PrefabUtility.InstantiatePrefab(prefab);
    }

    private static void BakeEnvironmentPrefab()
    {
        GameObject generated = GameObject.Find("Generated_Plant_Environment_N");
        if (generated != null)
        {
            UnityEngine.Object.DestroyImmediate(generated);
        }

        GameObject temporaryBuilderObject = new GameObject("Environment Prefab Baker");
        PlantEnvironmentBuilder builder = temporaryBuilderObject.AddComponent<PlantEnvironmentBuilder>();
        builder.BuildEnvironment();
        UnityEngine.Object.DestroyImmediate(temporaryBuilderObject);

        generated = GameObject.Find("Generated_Plant_Environment_N");
        if (generated == null)
        {
            throw new InvalidOperationException("PlantEnvironmentBuilder did not create its expected root.");
        }

        PersistGeneratedMaterials(generated);
        generated.name = "Industrial Plant Environment";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(generated, EnvironmentPrefabPath);
        UnityEngine.Object.DestroyImmediate(generated);
        PrefabUtility.InstantiatePrefab(prefab);
    }

    private static void PersistGeneratedMaterials(GameObject root)
    {
        Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                Material source = materials[index];
                if (source == null || AssetDatabase.Contains(source))
                {
                    continue;
                }

                if (!replacements.TryGetValue(source, out Material persisted))
                {
                    string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(source.name) ? "EnvironmentMaterial" : source.name);
                    string path = AssetDatabase.GenerateUniqueAssetPath(EnvironmentMaterialFolder + "/" + safeName + ".mat");
                    persisted = new Material(source);
                    persisted.name = safeName;
                    AssetDatabase.CreateAsset(persisted, path);
                    replacements.Add(source, persisted);
                }

                materials[index] = persisted;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }
        return value.Replace(" (Instance)", string.Empty);
    }

    private static void EnsureFolder(string folder)
    {
        string[] segments = folder.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }
            current = next;
        }
    }
}
