using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class GradleFileChecker : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
        {
            CheckGradleFile();
        }
    }

    private void CheckGradleFile()
    {
        string gradleFilePath = Path.Combine(Application.dataPath, "Plugins", "Android", "mainTemplate.gradle");

        if (!File.Exists(gradleFilePath))
        {
            if (!EditorUtility.DisplayDialog("[Ad-Virtua]File Missing",
                "The required file is missing.For detail, read 'Assets>Ad-Virtua>ReadMe'",
                "Continue", "Cancel Build"))
            {
                throw new BuildFailedException("Build canceled due to missing Gradle file.");
            }
        }
    }
}