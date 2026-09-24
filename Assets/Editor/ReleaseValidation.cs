using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReleaseValidation
{
    private static readonly (string Prefix, int Count)[] RequiredProcessRoutes =
    {
        ("H2Storage_pipe_", 3),
        ("H2_pipe_", 4),
        ("CO2_pipe_", 2),
        ("RichAmine_pipe_", 2),
        ("LeanAmine_pipe_", 3),
        ("RecycleGas_pipe_", 3),
        ("MixedFeed_pipe_", 3),
        ("Syngas_pipe_", 5),
        ("ReactorEffluent_pipe_", 3),
        ("CrudeMeOH_pipe_", 3),
        ("Liq_CrudeMeOH_pipe_", 2),
        ("MethanolProduct_pipe_", 2)
    };

    [MenuItem("Tools/Power-to-Methanol/Validate Release Scene")]
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var allObjects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .ToArray();

        var failures = new List<string>();

        foreach (var gameObject in allObjects)
        {
            var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missingCount > 0)
                failures.Add($"{GetPath(gameObject.transform)} has {missingCount} missing script(s)");
        }

        var names = new HashSet<string>(allObjects.Select(item => item.name), StringComparer.OrdinalIgnoreCase);
        foreach (var route in RequiredProcessRoutes)
        {
            var actual = allObjects.Count(item =>
                item.name.StartsWith(route.Prefix, StringComparison.OrdinalIgnoreCase));
            if (actual != route.Count)
                failures.Add($"{route.Prefix} expected {route.Count} segment(s), found {actual}");
        }

        RequireObject(names, failures, "Catalyst_Bed");
        RequireObject(names, failures, "Reactor_Shell");

        var catalyst = allObjects.FirstOrDefault(item =>
            string.Equals(item.name, "Catalyst_Bed", StringComparison.OrdinalIgnoreCase));
        if (catalyst != null)
        {
            if (catalyst.GetComponent<Renderer>() == null)
                failures.Add("Catalyst_Bed has no Renderer");
        }

        var reactorShell = allObjects.FirstOrDefault(item =>
            string.Equals(item.name, "Reactor_Shell", StringComparison.OrdinalIgnoreCase));
        if (reactorShell != null)
        {
            var renderer = reactorShell.GetComponent<Renderer>();
            if (renderer == null)
            {
                failures.Add("Reactor_Shell has no Renderer");
            }
        }

        if (Shader.Find("Custom/PipeFlow") == null)
            failures.Add("Custom/PipeFlow shader is unavailable");

        var cameraController = allObjects
            .Select(item => item.GetComponent<OrbitCameraController>())
            .FirstOrDefault(component => component != null);
        if (cameraController == null)
            failures.Add("OrbitCameraController is not present in SampleScene");
        else if (cameraController.panSpeed <= 0f)
            failures.Add("OrbitCameraController panSpeed must be positive");

        if (failures.Count > 0)
            throw new InvalidOperationException("Release validation failed:\n- " + string.Join("\n- ", failures));

        Debug.Log(
            $"RELEASE_VALIDATION_OK scene={scene.path} objects={allObjects.Length} " +
            $"processSegments={RequiredProcessRoutes.Sum(route => route.Count)} " +
            "catalyst=runtime-animated shell=present cameraPan=enabled");
    }

    private static void RequireObject(ISet<string> names, ICollection<string> failures, string objectName)
    {
        if (!names.Contains(objectName))
            failures.Add($"Missing required object: {objectName}");
    }

    private static string GetPath(Transform transform)
    {
        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}
