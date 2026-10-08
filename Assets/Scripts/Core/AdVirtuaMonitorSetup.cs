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
    // 2026-09-25 社長指示(dec-20260925-085)「宝石ゲームは起動・ゲーム開始でADVIRTUAを読まない」。
    // 初回プレイ終了(共通リザルトを閉じた時点)で解禁し、2回目のプレイ開始から広告を生成・表示する。
    // シーン上の Ad-VirtuaV3 は WarukyureBuilder が非アクティブで保存するため、解禁までSDKは起動しない。
    private static bool unlocked;

    // ===== ADVIRTUA hide=退避＋一時停止（2026-10-08 dec-20261007-087） =====
    // 表示切替に SetActive(false) を使うと SDK の AdPlay.OnDisable で video-request →
    // Prepare → RequestNextSequenceVideo のコルーチンが止まり、HasPrepared が true のまま
    // 残って再有効化後も再生が再開しない（＝広告が出なくなる）欠陥があった。
    // 非表示は active のままカメラ背後（viewport z<0）へ退避＋VideoPlayer.Pause() に置き換える。
    // 退避中は SDK の可視判定（0.2秒ごと・coverage=0）に通らず impression は送られない。
    private static bool _hidden;
    private static VideoPlayer _adVideoPlayer;
    private static bool _videoStartedHooked;
    private static MonoBehaviour _adPlay;   // MoviePlayStatus は SDK 非公開型のため reflection で読む

    /// <summary>初回プレイ終了後に呼ぶ。以後の Show() で広告を生成・表示する。</summary>
    public static void Unlock()
    {
        unlocked = true;
    }

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

            // 退避中に次の動画が始まったら Pause するための購読（二重購読は HookVideoStarted 内で防止）。
            HookVideoStarted();
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
        if (!unlocked) return;
        if (!_hidden)
        {
            // 初回（root がまだ一度も有効化されていない）を含む従来の有効化経路。
            Layout();
            adVirtuaRoot.SetActive(true);
            return;
        }
        // 2回目以降: 退避から元位置へ戻し、SDK が Playing 状態なら再生を再開する。
        _hidden = false;
        Layout();
        Physics.SyncTransforms();
        EnsureVideoPlayer();
        if (IsMovieStatusPlaying() && _adVideoPlayer != null
            && _adVideoPlayer.isPrepared && !_adVideoPlayer.isPlaying)
        {
            _adVideoPlayer.Play();
        }
        SetAuxiliaryUiVisible(true);
        Debug.Log("[AdVirtuaMonitorSetup] show: restore");
    }

    public static void Hide()
    {
        NotifySurface(true);
        if (adVirtuaRoot == null) return;
        // 解禁前・まだ一度も表示されていない時は何もしない
        // （root は Setup で既に非アクティブ＝SDK を起動させない）。
        if (!adVirtuaRoot.activeSelf || _hidden) return;
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[AdVirtuaMonitorSetup] hide skipped: camera not found.");
            return;
        }
        _hidden = true;
        ApplyRetreatPosition();
        Physics.SyncTransforms();
        EnsureVideoPlayer();
        if (_adVideoPlayer != null && _adVideoPlayer.isPlaying) _adVideoPlayer.Pause();
        SetAuxiliaryUiVisible(false);
        Debug.Log("[AdVirtuaMonitorSetup] hide: retreat+pause");
    }

    /// <summary>カメラ背後（WorldToViewportPoint の z<0）へ置く。可視判定に通らない。</summary>
    private static void ApplyRetreatPosition()
    {
        if (adVirtuaRoot == null || cam == null) return;
        adVirtuaRoot.transform.position = cam.transform.position - cam.transform.forward * MonitorCameraDistance;
    }

    /// <summary>シーン破棄時にハンドラと退避状態を必ず解除する。</summary>
    public static void ReleaseHidden()
    {
        _hidden = false;
        UnhookVideoStarted();
        _adVideoPlayer = null;
        _adPlay = null;
    }

    private static void EnsureVideoPlayer()
    {
        if (_adVideoPlayer == null && adVirtuaRoot != null)
        {
            _adVideoPlayer = adVirtuaRoot.GetComponentInChildren<VideoPlayer>(true);
            if (_adVideoPlayer == null)
                Debug.LogWarning("[AdVirtuaMonitorSetup] VideoPlayer not found under Ad-VirtuaV3 root.");
        }
    }

    private static void HookVideoStarted()
    {
        EnsureVideoPlayer();
        if (_adVideoPlayer == null || _videoStartedHooked) return;
        _adVideoPlayer.started += OnAdVideoStarted;
        _videoStartedHooked = true;
    }

    private static void UnhookVideoStarted()
    {
        if (_videoStartedHooked && _adVideoPlayer != null)
            _adVideoPlayer.started -= OnAdVideoStarted;
        _videoStartedHooked = false;
    }

    // 退避中に次の動画が始まったら即 Pause する（Pause 中に SDK が新動画を Play する経路の塞ぎ）。
    private static void OnAdVideoStarted(VideoPlayer vp)
    {
        if (_hidden) vp.Pause();
    }

    /// <summary>SDK の AdPlay.MoviePlayStatus が Playing か。SDK 型は非公開のため reflection。</summary>
    private static bool IsMovieStatusPlaying()
    {
        EnsureAdPlay();
        if (_adPlay == null) return false;
        try
        {
            var p = _adPlay.GetType().GetProperty("MoviePlayStatus",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var v = p != null && p.CanRead ? p.GetValue(_adPlay) : null;
            return v != null && v.ToString() == "Playing";
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdVirtuaMonitorSetup] MoviePlayStatus read failed: {ex.Message}");
            return false;
        }
    }

    private static void EnsureAdPlay()
    {
        if (_adPlay != null || adVirtuaRoot == null) return;
        foreach (var mb in adVirtuaRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            var p = mb.GetType().GetProperty("MoviePlayStatus",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead)
            {
                _adPlay = mb;
                return;
            }
        }
        Debug.LogWarning("[AdVirtuaMonitorSetup] MoviePlayStatus provider not found under Ad-VirtuaV3 root.");
    }

    /// <summary>PR表記など広告に付随するUIの表示切替（adVirtuaRoot 以外の active は触らない）。</summary>
    private static void SetAuxiliaryUiVisible(bool visible)
    {
        if (adVirtuaRoot == null) return;
        foreach (Transform t in adVirtuaRoot.GetComponentsInChildren<Transform>(true))
        {
            var n = t.name;
            if (n.StartsWith("AdPr") || n.IndexOf("PrLabel", StringComparison.OrdinalIgnoreCase) >= 0)
                t.gameObject.SetActive(visible);
        }
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

        // 退避中（非表示のタイトル）は Layout が広告をカメラ前へ戻さない。
        if (_hidden) ApplyRetreatPosition();
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

    void OnDestroy()
    {
        AdVirtuaMonitorSetup.ReleaseHidden();
    }
}
