using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class WebGLBuilder
{
    [MenuItem("Build/WebGL Build client")]
    public static void Build()
    {
        AdVirtuaFrontmostValidator.ValidateAdVirtuaFrontmost();

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/Main.unity" };
        options.locationPathName = "/Users/suzukimasahiro/Desktop/warukyure/client";
        options.target = BuildTarget.WebGL;
        options.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("WebGL build succeeded.");
        }
        else
        {
            Debug.LogError("WebGL build failed.");
            EditorApplication.Exit(1);
        }
    }
}
