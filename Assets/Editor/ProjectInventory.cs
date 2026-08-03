using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectInventory
{
    private const string OutputPath = "Logs/deep-project-inventory.tsv";

    [MenuItem("Tools/Power-to-Methanol/Export Project Inventory")]
    public static void Export()
    {
        Directory.CreateDirectory("Logs");
        var output = new StringBuilder(1024 * 1024);
        output.AppendLine("scope\tassetPath\thierarchyPath\tactive\tcomponent\tmesh\tmaterial\tshader\tboundsCenter\tboundsSize\tprefabSource");

        foreach (var sceneSetting in EditorBuildSettings.scenes.Where(scene => scene.enabled))
        {
            var scene = EditorSceneManager.OpenScene(sceneSetting.path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                AppendHierarchy(output, "SCENE", scene.path, root.transform);
        }

        foreach (var prefabPath in AssetDatabase.FindAssets("t:Prefab")
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AppendHierarchy(output, "PREFAB", prefabPath, root.transform);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (var modelPath in AssetDatabase.FindAssets("t:Model")
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Distinct()
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath).Where(asset => asset != null))
            {
                if (asset is Mesh mesh)
                    AppendRow(output, "MODEL_MESH", modelPath, asset.name, true, "Mesh", mesh.name, "", "",
                        mesh.bounds.center, mesh.bounds.size, "");
                else if (asset is Material material)
                    AppendRow(output, "MODEL_MATERIAL", modelPath, asset.name, true, "Material", "", material.name,
                        material.shader != null ? material.shader.name : "", Vector3.zero, Vector3.zero, "");
            }
        }

        foreach (var materialPath in AssetDatabase.FindAssets("t:Material")
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Distinct()
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                AppendRow(output, "MATERIAL_ASSET", materialPath, material.name, true, "Material", "", material.name,
                    material.shader != null ? material.shader.name : "", Vector3.zero, Vector3.zero, "");
        }

        File.WriteAllText(OutputPath, output.ToString(), new UTF8Encoding(false));
        Debug.Log($"Deep project inventory exported to {Path.GetFullPath(OutputPath)}");
    }

    private static void AppendHierarchy(StringBuilder output, string scope, string assetPath, Transform transform)
    {
        var hierarchyPath = GetHierarchyPath(transform);
        var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject);
        var prefabPath = prefabSource != null ? AssetDatabase.GetAssetPath(prefabSource) : "";
        var components = transform.GetComponents<Component>();

        if (components.Length == 1)
            AppendRow(output, scope, assetPath, hierarchyPath, transform.gameObject.activeInHierarchy, "Transform",
                "", "", "", Vector3.zero, Vector3.zero, prefabPath);

        foreach (var component in components.Where(component => component != null && !(component is Transform)))
        {
            if (component is Renderer renderer)
            {
                var mesh = GetRendererMesh(renderer);
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                    AppendRendererRow(output, scope, assetPath, hierarchyPath, transform, renderer, mesh, null, prefabPath);
                else
                    foreach (var material in materials)
                        AppendRendererRow(output, scope, assetPath, hierarchyPath, transform, renderer, mesh, material, prefabPath);
            }
            else
            {
                AppendRow(output, scope, assetPath, hierarchyPath, transform.gameObject.activeInHierarchy,
                    component.GetType().FullName, "", "", "", Vector3.zero, Vector3.zero, prefabPath);
            }
        }

        for (var i = 0; i < transform.childCount; i++)
            AppendHierarchy(output, scope, assetPath, transform.GetChild(i));
    }

    private static void AppendRendererRow(StringBuilder output, string scope, string assetPath, string hierarchyPath,
        Transform transform, Renderer renderer, Mesh mesh, Material material, string prefabPath)
    {
        AppendRow(output, scope, assetPath, hierarchyPath, transform.gameObject.activeInHierarchy,
            renderer.GetType().FullName, mesh != null ? mesh.name : "", material != null ? material.name : "",
            material != null && material.shader != null ? material.shader.name : "",
            renderer.bounds.center, renderer.bounds.size, prefabPath);
    }

    private static Mesh GetRendererMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned)
            return skinned.sharedMesh;
        var filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        for (var current = transform; current != null; current = current.parent)
            names.Push(current.name);
        return string.Join("/", names);
    }

    private static void AppendRow(StringBuilder output, string scope, string assetPath, string hierarchyPath,
        bool active, string component, string mesh, string material, string shader, Vector3 center, Vector3 size,
        string prefabSource)
    {
        output.Append(Clean(scope)).Append('\t')
            .Append(Clean(assetPath)).Append('\t')
            .Append(Clean(hierarchyPath)).Append('\t')
            .Append(active ? "1" : "0").Append('\t')
            .Append(Clean(component)).Append('\t')
            .Append(Clean(mesh)).Append('\t')
            .Append(Clean(material)).Append('\t')
            .Append(Clean(shader)).Append('\t')
            .Append(Format(center)).Append('\t')
            .Append(Format(size)).Append('\t')
            .Append(Clean(prefabSource)).AppendLine();
    }

    private static string Format(Vector3 value) => $"{value.x:F5},{value.y:F5},{value.z:F5}";
    private static string Clean(string value) => (value ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
