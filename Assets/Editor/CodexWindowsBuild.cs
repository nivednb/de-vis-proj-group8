using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Repeatable Windows build entry point for the educational digital twin.</summary>
public static class CodexWindowsBuild
{
    private const string DefaultOutput = "Builds/Windows/PtMeOH-DigitalTwin.exe";

    [MenuItem("Tools/Nived/Build Windows Application")]
    public static void BuildFromMenu()
    {
        BuildWindows();
    }

    public static void BuildWindows()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && File.Exists(scene.path))
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled, valid scenes are configured in Build Settings.");
        }

        string configuredOutput = Environment.GetEnvironmentVariable("PTMEOH_BUILD_PATH");
        string output = string.IsNullOrWhiteSpace(configuredOutput) ? DefaultOutput : configuredOutput;
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("Build output has no parent directory."));

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.StrictMode
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"PtMeOH Windows build result: {summary.result}; errors: {summary.totalErrors}; warnings: {summary.totalWarnings}; output: {output}");

        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Windows build failed with {summary.totalErrors} errors and {summary.totalWarnings} warnings.");
        }
    }
}
