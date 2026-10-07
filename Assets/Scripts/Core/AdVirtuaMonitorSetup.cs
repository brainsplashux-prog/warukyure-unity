using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Video;

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
    // ===== ADVIRTUA keep-prepared（連続プレイ時の再リクエスト防止 2026-10-07 dec-20261007-075） =====
    // 手本: ColorSort 7463097（docs/handoff/advirtua-keep-prepared-20260930）。
    // タイトルへ戻る時に SetActive(false) すると SDK の準備（video-request → Prepare）が止まり、
    // 次のプレイで最初からやり直しになる＝広告が出る前にプレイが終わり売上が立たない。
    // 未準備の間はタイトルでも active を維持し、カメラ背後（viewport z<0）へ退避させて準備を続行する。
    // 退避中はカメラ外のため SDK の可視判定に通らず、start/viewable impression は送られない。
    // 準備完了→SDK が Play() した瞬間（VideoPlayer.started）に即 SetActive(false) する。
    private static bool _retreated;
    private static VideoPlayer _adVideoPlayer;
    private static bool _videoStartedHooked;

    /// <summary>テスト差し替え用。null なら SDK の公開 getter HasPrepared を reflection で読む。</summary>
    public static Func<bool> HasPreparedProbe;
    /// <summary>テスト差し替え用。null なら VideoPlayer.isPlaying を読む。</summary>
    public static Func<bool> IsPlayingProbe;

    /// <summary>退避中（active のままカメラ背後に置いて準備を続行している）か。</summary>
    public static bool IsRetreated => _retreated;

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

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PoiSetSurface(int nonGame);
#endif

    /// <summary>
    /// 共通クロームの面(surface)をホストJSへ通知する。
    /// Show()=ゲーム面("play")／Hide()=非ゲーム面("non-game")。
    /// TitleScreen の4経路すべてが Show/Hide 経由でここを通るため、
    /// 面通知の呼び出し忘れによるステージずれ事故を構造的に防ぐ。
    /// </summary>
    private static void NotifySurface(bool nonGame)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PoiSetSurface(nonGame ? 1 : 0);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdVirtuaMonitorSetup] PoiSetSurface failed: {ex.Message}");
        }
#endif
    }

    public static void Show()
    {
        // Show()は「ゲーム面へ移る」という宣言であり、ADVIRTUAの実体(adVirtuaRoot)が
        // 存在するかどうかとは独立。null経路でも面通知だけは必ず行う。
        NotifySurface(false);
        if (adVirtuaRoot == null)
        {
            Debug.LogWarning("[AdVirtuaMonitorSetup] Ad-VirtuaV3 not set. Call Setup() first.");
            return;
        }
        CancelRetreat();
        Layout();
        adVirtuaRoot.SetActive(true);
    }

    public static void Hide()
    {
        NotifySurface(true);
        if (adVirtuaRoot == null) return;
        // 既に非表示の時は従来どおり。
        if (!adVirtuaRoot.activeSelf || _retreated)
        {
            if (!_retreated) adVirtuaRoot.SetActive(false);
            return;
        }
        if (IsPrepared())
        {
            // 準備済み: 従来どおり無効化。次のプレイでは hasPrepared=true 経路で Play() のみ。
            adVirtuaRoot.SetActive(false);
            return;
        }
        EnterRetreat();
    }

    /// <summary>SDK の HasPrepared を取得する。HasPreparedProbe 差し替えでテスト可能。</summary>
    public static bool IsPrepared()
    {
        if (HasPreparedProbe != null) return HasPreparedProbe();
        if (adVirtuaRoot == null) return false;
        foreach (var mb in adVirtuaRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            var t = mb.GetType();
            var p = t.GetProperty("HasPrepared",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(bool) && p.CanRead)
                return (bool)p.GetValue(mb);
            var f = t.GetField("HasPrepared",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool))
                return (bool)f.GetValue(mb);
        }
        return false;
    }

    private static bool IsVideoPlaying()
    {
        if (IsPlayingProbe != null) return IsPlayingProbe();
        EnsureVideoPlayer();
        return _adVideoPlayer != null && _adVideoPlayer.isPlaying;
    }

    /// <summary>未準備のまま非表示要求が来た時: active のままカメラ背後へ退避させる。</summary>
    private static void EnterRetreat()
    {
        if (cam == null)
        {
            // カメラを取れない異常系では退避位置を決められないため従来動作に倒す。
            adVirtuaRoot.SetActive(false);
            return;
        }
        _retreated = true;
        HookVideoStarted();
        ApplyRetreatPosition();
        Debug.Log("[AdVirtuaMonitorSetup] retreat: ad kept active behind camera while preparing");
    }

    /// <summary>カメラ背後（WorldToViewportPoint の z<0）へ置く。可視判定に通らない。</summary>
    private static void ApplyRetreatPosition()
    {
        if (adVirtuaRoot == null || cam == null) return;
        adVirtuaRoot.transform.position = cam.transform.position - cam.transform.forward * MonitorCameraDistance;
    }

    /// <summary>プレイ復帰時: 退避を解除する（位置は呼び出し側の Layout() で元へ戻る）。</summary>
    private static void CancelRetreat()
    {
        if (!_retreated) return;
        _retreated = false;
        UnhookVideoStarted();
        Debug.Log("[AdVirtuaMonitorSetup] retreat cancelled");
    }

    /// <summary>
    /// 退避中に再生が始まった（=準備完了→SDKがPlay()した）時の無効化。
    /// VideoPlayer.started ハンドラと RetreatGuardTick の両方から呼ばれる。
    /// </summary>
    public static void DeactivateRetreatedAd()
    {
        _retreated = false;
        UnhookVideoStarted();
        if (adVirtuaRoot == null) return;
        adVirtuaRoot.SetActive(false);
        Layout();
        Debug.Log("[AdVirtuaMonitorSetup] retreated ad deactivated after prepare/play");
    }

    /// <summary>
    /// 退避中の保険。AdVirtuaResizeWatcher.LateUpdate から毎フレーム呼ぶ。
    /// started を取りこぼしても HasPrepared/isPlaying が true になった次のフレームで無効化する。
    /// </summary>
    public static void RetreatGuardTick()
    {
        if (!_retreated || adVirtuaRoot == null) return;
        if (IsPrepared() || IsVideoPlaying()) DeactivateRetreatedAd();
    }

    /// <summary>シーン破棄時にハンドラと退避状態を必ず解除する。</summary>
    public static void ReleaseRetreat()
    {
        _retreated = false;
        UnhookVideoStarted();
        _adVideoPlayer = null;
    }

    private static void EnsureVideoPlayer()
    {
        if (_adVideoPlayer == null && adVirtuaRoot != null)
            _adVideoPlayer = adVirtuaRoot.GetComponentInChildren<VideoPlayer>(true);
    }

    private static void HookVideoStarted()
    {
        EnsureVideoPlayer();
        if (_adVideoPlayer == null)
        {
            Debug.LogWarning("[AdVirtuaMonitorSetup] VideoPlayer not found — started hook unavailable");
            return;
        }
        if (_videoStartedHooked) return;
        _adVideoPlayer.started += OnAdVideoStarted;
        _videoStartedHooked = true;
    }

    private static void UnhookVideoStarted()
    {
        if (_videoStartedHooked && _adVideoPlayer != null)
            _adVideoPlayer.started -= OnAdVideoStarted;
        _videoStartedHooked = false;
    }

    // prepareCompleted は SDK が解除・再登録するため順序に頼れない。
    // started は SDK が hasPrepared=true にして Play() した後にしか来ないので順序に依存しない。
    private static void OnAdVideoStarted(VideoPlayer vp)
    {
        if (_retreated) DeactivateRetreatedAd();
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

        // 退避中（未準備のタイトル）は Layout が広告をカメラ前へ戻さない。
        if (_retreated) ApplyRetreatPosition();
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

    void LateUpdate()
    {
        // 退避中の保険: started を取りこぼしても準備完了/再生開始を次フレームで拾う。
        AdVirtuaMonitorSetup.RetreatGuardTick();
    }

    void OnDestroy()
    {
        AdVirtuaMonitorSetup.ReleaseRetreat();
    }
}
