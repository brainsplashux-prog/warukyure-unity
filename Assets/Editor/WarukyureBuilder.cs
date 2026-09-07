using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WarukyureBuilder
{
    [MenuItem("Warukyure/Build WebGL")]
    public static void BuildWebGL()
    {
        // 1. 既存シーンの Ad-Virtua 構造を事前検査
        AdVirtuaFrontmostValidator.ValidateAdVirtuaFrontmost();

        string clientOutPath = "/Users/suzukimasahiro/Desktop/warukyure/client";

        // Ensure PoiLoader cache-buster variable is empty so deploy script adds ?v=.
        string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates/PoiLoader/index.html");
        if (File.Exists(templatePath))
        {
            string html = File.ReadAllText(templatePath);
            html = html.Replace("var cb = \"?v=2.11.0\";", "var cb = \"\";");
            File.WriteAllText(templatePath, html);
        }

        // Configure player
        PlayerSettings.companyName = "poicasi";
        PlayerSettings.productName = "warukyure";
        PlayerSettings.bundleVersion = "0.0.39";
        PlayerSettings.defaultWebScreenWidth = 720;
        PlayerSettings.defaultWebScreenHeight = 1224;

        // Ensure WebGL build target
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        // Import and configure art
        string artPath = "Assets/Resources/art_final.png";
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(artPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        else
        {
            Debug.LogError("[WarukyureBuilder] art_final importer not found.");
            EditorApplication.Exit(1);
            return;
        }

        // Create scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        scene.name = "Main";

        GameObject boardGO = new GameObject("Board");
        boardGO.AddComponent<WarukyureBoard>();
        SceneManager.MoveGameObjectToScene(boardGO, scene);

        // Ad-Virtua モニターをシーンに配置
        GameObject adPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ad-Virtua/Ad-VirtuaV3.prefab");
        if (adPrefab == null)
        {
            Debug.LogError("[WarukyureBuilder] Ad-VirtuaV3.prefab not found.");
            EditorApplication.Exit(1);
            return;
        }
        GameObject adGO = GameObject.Instantiate(adPrefab);
        adGO.name = "Ad-VirtuaV3";
        SceneManager.MoveGameObjectToScene(adGO, scene);

        // 生成直後のシーン構造を Ad-Virtua 最前面検査
        AdVirtuaFrontmostValidator.ValidateCurrentScene();

        string scenesDir = Path.Combine(Application.dataPath, "Scenes");
        Directory.CreateDirectory(scenesDir);
        string sceneFullPath = Path.Combine(scenesDir, "Main.unity");
        EditorSceneManager.SaveScene(scene, sceneFullPath);

        // BuildPlayer expects project-relative scene paths
        string scenePath = "Assets/Scenes/Main.unity";

        // Clean previous build: ローカル削除は禁止。既存出力を trash-manual へ退避する。
        if (Directory.Exists(clientOutPath))
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "trash-manual", $"{date}-warukyure-client");
            string dest = baseDir;
            int suffix = 0;
            while (Directory.Exists(dest))
            {
                suffix++;
                dest = $"{baseDir}-{suffix}";
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            Directory.Move(clientOutPath, dest);
            Debug.Log("[WarukyureBuilder] Moved existing client output to " + dest);
        }

        // Build
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = clientOutPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[WarukyureBuilder] Build failed: " + report.summary.result);
            EditorApplication.Exit(1);
        }

        Debug.Log("[WarukyureBuilder] Build succeeded at " + clientOutPath);
    }
}
