using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Ad_Virtua.Editor
{
    [InitializeOnLoad]
    public class DependencyInstaller
    {
        private const string RequiredPackageName = "com.unity.ads.ios-support";
        private const string RequiredVersion = "1.0.0";
        private static ListRequest listRequest;
        private static AddRequest addRequest;

        static DependencyInstaller()
        {
            EditorApplication.update += CheckDependencies;
        }

        private static void CheckDependencies()
        {
            // Execute only once
            EditorApplication.update -= CheckDependencies;

            // Get package list
            listRequest = Client.List(true);
            EditorApplication.update += OnListRequestComplete;
        }

        private static void OnListRequestComplete()
        {
            if (!listRequest.IsCompleted) return;

            EditorApplication.update -= OnListRequestComplete;

            if (listRequest.Status == StatusCode.Success)
            {
                bool installed = listRequest.Result.Any(p => p.name == RequiredPackageName);

                if (!installed)
                {
                    // Confirm auto-installation
                    bool autoInstall = EditorUtility.DisplayDialog(
                        "[Ad-Virtua] Dependency Package Installation",
                        $"Required packages for Ad-Virtua iOS functionality are missing.\n\n" +
                        $"Package: {RequiredPackageName}\n" +
                        $"Purpose: IDFA retrieval on iOS 14\n\n" +
                        "Install automatically?",
                        "Install",
                        "Install Manually Later");

                    if (autoInstall)
                    {
                        Debug.Log($"[Ad-Virtua] Auto-installing {RequiredPackageName}...");
                        addRequest = Client.Add($"{RequiredPackageName}@{RequiredVersion}");
                        EditorApplication.update += OnAddRequestComplete;
                    }
                    else
                    {
                        Debug.LogWarning($"[Ad-Virtua] Manual installation of {RequiredPackageName} is required." +
                                       "\nPlease search for 'iOS 14 Advertising Support' in Window > Package Manager > Unity Registry.");
                    }
                }
            }
            else
            {
                Debug.LogError($"[Ad-Virtua] Failed to retrieve package list: {listRequest.Error?.message}");
            }
        }

        private static void OnAddRequestComplete()
        {
            if (!addRequest.IsCompleted) return;

            EditorApplication.update -= OnAddRequestComplete;

            if (addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[Ad-Virtua] {RequiredPackageName} installation completed successfully!");
            }
            else
            {
                Debug.LogError($"[Ad-Virtua] Failed to install {RequiredPackageName}: {addRequest.Error?.message}");
                EditorUtility.DisplayDialog(
                    "[Ad-Virtua] Installation Failed",
                    $"Automatic installation failed.\n\n" +
                    $"Please install manually:\n" +
                    $"Window > Package Manager > Unity Registry > 'iOS 14 Advertising Support'",
                    "OK");
            }
        }
    }
}