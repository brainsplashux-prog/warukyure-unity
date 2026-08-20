using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace Ad_Virtua.Editor
{
    /// <summary>
    /// Checks required Android settings for Ad-Virtua before build.
    /// Does not modify any files, only shows warnings via dialog.
    /// </summary>
    public class AndroidBuildChecker : IPreprocessBuildWithReport
    {
        private const string GradlePath = "Plugins/Android/mainTemplate.gradle";
        private const string ManifestPath = "Plugins/Android/AndroidManifest.xml";
        private const string PropertiesPath = "Plugins/Android/gradleTemplate.properties";

        private const string RequiredGradleDependency = "play-services-ads-identifier";
        private const string RequiredManifestPermission = "com.google.android.gms.permission.AD_ID";
        private const string RequiredAndroidX = "android.useAndroidX=true";
        private const string RequiredJetifier = "android.enableJetifier=true";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            var warnings = new System.Text.StringBuilder();

            // mainTemplate.gradle check
            CheckFile(GradlePath, RequiredGradleDependency,
                "mainTemplate.gradle not found.\n" +
                "Please enable 'Custom Main Gradle Template' in\n" +
                "Project Settings > Player > Publishing Settings.",
                "mainTemplate.gradle is missing the play-services-ads-identifier dependency.\n" +
                "Please add the following line to the dependencies block:\n" +
                "implementation 'com.google.android.gms:play-services-ads-identifier:18.0.1'",
                warnings);

            // AndroidManifest.xml check
            CheckFile(ManifestPath, RequiredManifestPermission,
                "AndroidManifest.xml not found.\n" +
                "Please enable 'Custom Main Manifest' in\n" +
                "Project Settings > Player > Publishing Settings.",
                "AndroidManifest.xml is missing the AD_ID permission.\n" +
                "Please add the following line under the <manifest> tag:\n" +
                "<uses-permission android:name=\"com.google.android.gms.permission.AD_ID\"/>",
                warnings);

            // gradleTemplate.properties check
            CheckFile(PropertiesPath, RequiredAndroidX,
                "gradleTemplate.properties not found.\n" +
                "Please enable 'Custom Gradle Properties Template' in\n" +
                "Project Settings > Player > Publishing Settings.",
                "gradleTemplate.properties is missing AndroidX settings.\n" +
                "Please add the following lines:\n" +
                "android.useAndroidX=true\n" +
                "android.enableJetifier=true",
                warnings);

            if (warnings.Length > 0)
            {
                bool cancel = !EditorUtility.DisplayDialog(
                    "[Ad-Virtua] Android Build Warning",
                    "Some required settings for Ad-Virtua are missing.\n" +
                    "ADID (Advertising ID) retrieval may not work correctly.\n\n" +
                    warnings.ToString() +
                    "\nFor details, please refer to the Ad-Virtua documentation.",
                    "Continue Build",
                    "Cancel Build");

                if (cancel)
                {
                    throw new BuildFailedException("[Ad-Virtua] Build canceled due to missing Android settings.");
                }
            }
        }

        private static void CheckFile(string relativePath, string requiredContent,
            string missingFileMessage, string missingContentMessage,
            System.Text.StringBuilder warnings)
        {
            string fullPath = Path.Combine(Application.dataPath, relativePath);

            if (!File.Exists(fullPath))
            {
                warnings.AppendLine("---");
                warnings.AppendLine(missingFileMessage);
                return;
            }

            string content = File.ReadAllText(fullPath);
            if (!content.Contains(requiredContent))
            {
                warnings.AppendLine("---");
                warnings.AppendLine(missingContentMessage);
            }
        }
    }
}
