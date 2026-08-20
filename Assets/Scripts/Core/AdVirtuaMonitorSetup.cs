using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Ad-Virtua モニターのサイズ/配置を画面幅100%・16:9 逆算で決定する。
/// カメラが perspective / orthographic のいずれでも、実行時の実カメラパラメータを使う。
/// </summary>
public static class AdVirtuaMonitorSetup
{
    public const float MonitorCameraDistance = 5f;   // カメラからモニターまでの距離
    public const float CanvasPlaneDistance = 10f;    // UI Canvas が配置されるカメラ距離

    private const float ReferenceCanvasWidth = 720f;
    private const float ReferenceCanvasHeight = 1224f;
    private const float DefaultBandTopPx = 0f;       // 予約プレースホルダと同じ「画面最上部から0px」
    private const int AdVirtuaLayer = 8;

    private static Camera cam;
    private static GameObject adVirtuaRoot;
    private static float bandTopPx;

    /// <summary>
    /// Ad-VirtuaV3 ルートを名前で探す。active/inactive どちらでも取得する。
    /// </summary>
    private static GameObject FindAdVirtuaRoot()
    {
        var root = GameObject.Find("Ad-VirtuaV3");
        if (root == null)
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (t.parent == null && t.name == "Ad-VirtuaV3")
                {
                    root = t.gameObject;
                    break;
                }
            }
        }
        return root;
    }

    /// <summary>
    /// Ad-Virtua モニターの初期配置を行う。ゲーム開始直後は非表示にする。
    /// </summary>
    public static void Setup(float bandTop = DefaultBandTopPx)
    {
        bandTopPx = bandTop;

        adVirtuaRoot = FindAdVirtuaRoot();
        if (adVirtuaRoot != null)
        {
            adVirtuaRoot.SetActive(false);
            // Ad-Virtua 専用 Layer へ統一（審査対策・常に可視を保証）
            adVirtuaRoot.layer = AdVirtuaLayer;
            foreach (Transform t in adVirtuaRoot.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = AdVirtuaLayer;
        }

        try
        {
            var oldCam = GameObject.Find("AdVirtuaDisplayCamera");
            if (oldCam != null)
            {
                oldCam.SetActive(false);
                UnityEngine.Object.Destroy(oldCam);
            }

            cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[AdVirtuaMonitorSetup] Main Camera not found.");
                return;
            }
            cam.cullingMask |= (1 << AdVirtuaLayer);

            if (adVirtuaRoot == null)
            {
                Debug.LogWarning("[AdVirtuaMonitorSetup] Ad-VirtuaV3 not found.");
                return;
            }

            Layout();

            int boundCount = 0;
            var mbs = adVirtuaRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in mbs)
            {
                if (mb == null)
                {
                    Debug.LogWarning("[AdVirtuaMonitorSetup] Missing SDK component (skipped).");
                    continue;
                }

                try
                {
                    var type = mb.GetType();
                    var targetCameraField = type.GetField("targetCamera",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (targetCameraField != null && targetCameraField.FieldType == typeof(Camera))
                    {
                        targetCameraField.SetValue(mb, cam);

                        var enableConversionField = type.GetField("enableConversion",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (enableConversionField != null && enableConversionField.FieldType == typeof(bool))
                        {
                            enableConversionField.SetValue(mb, true);
                        }
                        else
                        {
                            Debug.LogWarning($"[AdVirtuaMonitorSetup] enableConversion not found on {type.Name}.");
                        }

                        var unitIdField = type.GetField("advirtuaUnitId",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (unitIdField != null && unitIdField.FieldType == typeof(string))
                        {
                            var unitId = unitIdField.GetValue(mb) as string;
                            if (string.IsNullOrEmpty(unitId))
                            {
                                Debug.Log("[AdVirtuaMonitorSetup] advirtuaUnitId is empty. Test ads / placeholder will be used until the production Unit ID is configured.");
                            }
                        }

                        boundCount++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AdVirtuaMonitorSetup] SDK binding skipped: {ex.Message}");
                }
            }

            if (boundCount == 0)
            {
                Debug.LogWarning("[AdVirtuaMonitorSetup] No AdPlay component with 'targetCamera' field was found. SDK binding may be broken.");
            }
        }
        catch (Exception ex)
        {
            if (adVirtuaRoot != null) adVirtuaRoot.SetActive(false);
            Debug.LogWarning($"[AdVirtuaMonitorSetup] Setup aborted safely: {ex.Message}");
        }
    }

    public static void Show()
    {
        if (adVirtuaRoot == null)
        {
            Debug.LogWarning("[AdVirtuaMonitorSetup] Ad-VirtuaV3 not set. Call Setup() first.");
            return;
        }
        Layout();
        adVirtuaRoot.SetActive(true);
    }

    public static void Hide()
    {
        if (adVirtuaRoot == null) return;
        adVirtuaRoot.SetActive(false);
    }

    /// <summary>
    /// 画面サイズに応じて Ad-Virtua モニターを配置する。
    /// ViewportWidth方式: カメラの視錐台の幅を100%使用し、高さは幅 * 9/16。
    /// </summary>
    public static void Layout()
    {
        if (cam == null || adVirtuaRoot == null) return;

        float d = MonitorCameraDistance;
        float worldH;
        float worldW;

        if (cam.orthographic)
        {
            worldH = cam.orthographicSize * 2f;
            worldW = worldH * cam.aspect;
        }
        else
        {
            worldH = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            worldW = worldH * cam.aspect;
        }

        float monW = worldW;
        float monH = monW * (9f / 16f);

        // CanvasScaler: reference 720x1224 / matchWidthOrHeight = 0 （幅基準）
        float scaleFactor = (float)Screen.width / ReferenceCanvasWidth;
        float bandTopActualPx = bandTopPx * scaleFactor;
        float worldPerActualPx = worldH / Screen.height;
        float topOffsetFromCenter = worldH * 0.5f - bandTopActualPx * worldPerActualPx;

        float cy = cam.transform.position.y + topOffsetFromCenter - monH * 0.5f;

        Vector3 pos = cam.transform.position + cam.transform.forward * d;
        pos.x = cam.transform.position.x;
        pos.y = cy;
        adVirtuaRoot.transform.position = pos;

        adVirtuaRoot.transform.localScale = new Vector3(monW, monH, 1f);
        adVirtuaRoot.transform.localRotation = Quaternion.identity;
    }
}

/// <summary>
/// 画面回転・リサイズを監視し、Layout() を再実行する。
/// </summary>
public class AdVirtuaResizeWatcher : MonoBehaviour
{
    private int lastW;
    private int lastH;

    void Start()
    {
        lastW = Screen.width;
        lastH = Screen.height;
    }

    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            AdVirtuaMonitorSetup.Layout();
        }
    }
}
