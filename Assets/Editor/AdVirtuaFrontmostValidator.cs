// Ad-Virtua「常に最前面」自動検査
// 正本ルール: ~/.claude/manuals/ad-monetization.md §「Ad-Virtuaは常に最前面（最高z-order/描画順）を維持する」
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AdVirtuaFrontmostValidator
{
    private static readonly string[] AdVirtuaScenePaths =
    {
        "Assets/Scenes/Main.unity",
    };

    private const string AdVirtuaRootName = "Ad-VirtuaV3";
    private const int AdVirtuaLayer = 8;

    [MenuItem("Warukyure/Validate Ad-Virtua Frontmost")]
    public static void ValidateAdVirtuaFrontmost()
    {
        var violations = new List<string>();

        foreach (var scenePath in AdVirtuaScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                violations.Add($"{scenePath}: シーンを開けない");
                continue;
            }
            ValidateScene(scene, scenePath, violations);
        }

        if (violations.Count > 0)
        {
            var msg = "[warukyure][advirtua] 最前面検査 FAIL (" + violations.Count + "件):\n  - " +
                      string.Join("\n  - ", violations) +
                      "\n正本ルール: ~/.claude/manuals/ad-monetization.md §Ad-Virtuaは常に最前面";
            Debug.LogError(msg);
            throw new System.InvalidOperationException(msg);
        }

        Debug.Log("[warukyure][advirtua] 最前面検査 PASS: 対象シーン=" +
                  string.Join(",", AdVirtuaScenePaths));
    }

    /// <summary>
    /// 現在アクティブなシーンを検査する（ビルド中の新規生成シーン用）。
    /// </summary>
    public static void ValidateCurrentScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var violations = new List<string>();
        if (!scene.IsValid())
        {
            violations.Add("アクティブシーンが無効");
        }
        else
        {
            ValidateScene(scene, scene.path, violations);
        }

        if (violations.Count > 0)
        {
            var msg = "[warukyure][advirtua] 最前面検査 FAIL (" + violations.Count + "件):\n  - " +
                      string.Join("\n  - ", violations) +
                      "\n正本ルール: ~/.claude/manuals/ad-monetization.md §Ad-Virtuaは常に最前面";
            Debug.LogError(msg);
            throw new System.InvalidOperationException(msg);
        }

        Debug.Log($"[warukyure][advirtua] 最前面検査 PASS: シーン={scene.path}");
    }

    private static void ValidateScene(Scene scene, string scenePath, List<string> violations)
    {
        GameObject adRoot = null;
        Camera mainCam = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == AdVirtuaRootName) adRoot = root;
            var cam = root.GetComponentInChildren<Camera>(true);
            if (mainCam == null && cam != null && cam.CompareTag("MainCamera")) mainCam = cam;
        }

        if (adRoot == null)
        {
            violations.Add($"{scenePath}: シーンルート直下に {AdVirtuaRootName} が無い（広告そのものが出ない）");
            return;
        }
        if (mainCam == null)
        {
            violations.Add($"{scenePath}: MainCamera が無い");
            return;
        }

        if ((mainCam.cullingMask & (1 << AdVirtuaLayer)) == 0)
        {
            violations.Add($"{scenePath}: MainCamera.cullingMask に Layer{AdVirtuaLayer}(AdVirtuaLayer) が含まれない");
        }

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.isRootCanvas) continue;
            if (canvas.transform.IsChildOf(adRoot.transform)) continue;

            // ScreenSpaceOverlay = 0, ScreenSpaceCamera = 1
            if ((int)canvas.renderMode == 0)
            {
                violations.Add($"{scenePath}: Canvas '{canvas.name}' が ScreenSpaceOverlay。" +
                               "Overlay は必ず広告の手前に描画されるため禁止（ScreenSpaceCamera へ変更する）");
                continue;
            }
            if ((int)canvas.renderMode == 1)
            {
                if (canvas.worldCamera == null)
                {
                    violations.Add($"{scenePath}: Canvas '{canvas.name}' の worldCamera が未設定（Overlay と同じ挙動になる）");
                }
                if (canvas.planeDistance <= AdVirtuaMonitorSetup.MonitorCameraDistance)
                {
                    violations.Add($"{scenePath}: Canvas '{canvas.name}' の planeDistance={canvas.planeDistance} が " +
                                   $"広告距離 {AdVirtuaMonitorSetup.MonitorCameraDistance} 以下＝UI が広告の手前に来る");
                }
            }
        }

        float adZ = mainCam.transform.position.z + AdVirtuaMonitorSetup.MonitorCameraDistance;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null || r.transform.IsChildOf(adRoot.transform)) continue;
            if (r is SpriteRenderer || r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                float z = r.transform.position.z;
                if (z < adZ)
                {
                    violations.Add($"{scenePath}: '{r.name}'({r.GetType().Name}) が z={z} で広告(z={adZ})より手前にある");
                }
            }
        }
    }
}
