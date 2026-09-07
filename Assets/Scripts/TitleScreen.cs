using UnityEngine;
using UnityEngine.UI;

// タイトル画面。承認済み原画 title_bg_v4.png（720x1224）を Canvas 設計空間いっぱいに
// 表示し、画像内に焼き込まれた START ボタン位置へのタップで閉じる。
// 押下判定は D-14裁定に従い Button.onClick に依存せず、旧 Input System の生ポインタ座標
// ＋ RectTransformUtility.RectangleContainsScreenPoint で行う（SoundMuteButton.cs と同じ方式。
// Canvas は ScreenSpaceCamera のため第3引数には canvas.worldCamera を渡す。null 固定は不可）。
public class TitleScreen : MonoBehaviour
{
    // Canvas 設計空間（WarukyureBoard.SetupCanvas の referenceResolution と一致）
    const float DesignW = 720f;
    const float DesignH = 1224f;

    // title_bg_v4.png 内の START ボタン実測位置（上端からのpx）。
    // design-approved/title/approved.png 実測: x=110, y=924, w=500, h=150
    static readonly Rect StartHit = new Rect(110f, 924f, 500f, 150f);

    public static bool IsShowing { get; private set; }

    Canvas canvas;
    WarukyureBoard board;
    RectTransform root;
    RectTransform startHitRect;
    Text errorText;
    Camera hitCamera;

    const float SessionTimeout = 10f;
    float sessionWaitTimer = 0f;
    bool errorShown = false;

    public void Init(Canvas c, WarukyureBoard b)
    {
        canvas = c;
        board = b;

        // 同梱重量を減らすため、PNG(非圧縮テクスチャ)ではなくJPEGバイト列を実行時にデコードする。
        TextAsset jpg = Resources.Load<TextAsset>("title_bg_v4");
        Texture2D tex = null;
        if (jpg != null)
        {
            tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(jpg.bytes)) tex = null;
        }
        if (tex == null)
        {
            // 画像が無いまま全面オーバーレイを出すと抜け出す手段が無いため、
            // タイトル自体を出さず従来どおり盤面＋ADVIRTUA表示へフォールバックする。
            Debug.LogError("[Warukyure][TITLE] title_bg_v4 texture not found. Skipping title screen.");
            IsShowing = false;
            AdVirtuaMonitorSetup.Show();
            return;
        }

        GameObject rootGO = new GameObject("TitleRoot", typeof(RectTransform));
        rootGO.transform.SetParent(canvas.transform, false);
        root = rootGO.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(DesignW, DesignH);
        root.SetAsLastSibling(); // 常に最前面

        GameObject bgGO = new GameObject("TitleBg",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        bgGO.transform.SetParent(root, false);
        RectTransform bg = bgGO.GetComponent<RectTransform>();
        bg.anchorMin = new Vector2(0f, 1f);
        bg.anchorMax = new Vector2(0f, 1f);
        bg.pivot = new Vector2(0f, 1f);
        bg.anchoredPosition = Vector2.zero;
        bg.sizeDelta = new Vector2(DesignW, DesignH);
        RawImage img = bgGO.GetComponent<RawImage>();
        img.texture = tex;
        // EventSystem/GraphicRaycaster が存在するため、true にしてタイトル背面の
        // 盤面ボタン（BET/SPIN/ヘルプ等の onClick 副作用）への貫通タップを防ぐ。
        // 当たり判定自体は生ポインタ方式なのでこの設定に依存しない。
        img.raycastTarget = true;

        // START ボタンの当たり判定だけを持つ透明 RectTransform（表示物は無し）
        GameObject hitGO = new GameObject("TitleStartHit", typeof(RectTransform));
        hitGO.transform.SetParent(root, false);
        startHitRect = hitGO.GetComponent<RectTransform>();
        startHitRect.anchorMin = new Vector2(0f, 1f);
        startHitRect.anchorMax = new Vector2(0f, 1f);
        startHitRect.pivot = new Vector2(0f, 1f);
        startHitRect.anchoredPosition = new Vector2(StartHit.x, -StartHit.y);
        startHitRect.sizeDelta = new Vector2(StartHit.width, StartHit.height);

        CreateErrorText();
        sessionWaitTimer = 0f;
        errorShown = false;

        ResolveCamera();

        // タイトル表示中は ADVIRTUA を出さない
        // （game-layout-standard.md: ADVIRTUA を出せるのは実際に遊んでいるゲーム画面のみ）。
        AdVirtuaMonitorSetup.Hide();
        IsShowing = true;
    }

    void Update()
    {
        if (!IsShowing || startHitRect == null) return;
        if (hitCamera == null) ResolveCamera();

        if (!errorShown && board != null && !board.IsSessionReady)
        {
            sessionWaitTimer += Time.deltaTime;
            if (sessionWaitTimer >= SessionTimeout)
                ShowConnectionError();
        }

        ReadTap();
    }

    void ReadTap()
    {
        bool down = false;
        Vector2 pos = Vector2.zero;

        if (Input.GetMouseButtonDown(0))
        {
            down = true;
            pos = Input.mousePosition;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            down = true;
            pos = Input.GetTouch(0).position;
        }

        if (!down) return;

        // セッション未確立かつエラー表示中はタップで再接続を試みる。
        if (errorShown)
        {
            if (board != null) board.RetrySession();
            errorShown = false;
            sessionWaitTimer = 0f;
            if (errorText != null) errorText.gameObject.SetActive(false);
            return;
        }

        // sessionReady 前は残高・ミッション未取得のまま盤面へ入るのを防ぐため閉じない。
        if (board == null || !board.IsSessionReady) return;

        if (RectTransformUtility.RectangleContainsScreenPoint(startHitRect, pos, hitCamera))
            Close();
    }

    // ScreenSpaceOverlay では null、ScreenSpaceCamera/WorldSpace では
    // canvas.worldCamera → Camera.main の順でフォールバック（SoundMuteButton と同じ）。
    void ResolveCamera()
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            hitCamera = null;
            return;
        }
        hitCamera = canvas.worldCamera;
        if (hitCamera == null) hitCamera = Camera.main;
    }

    void CreateErrorText()
    {
        GameObject go = new GameObject("TitleErrorText", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(root, false);
        errorText = go.AddComponent<Text>();
        errorText.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (errorText.font == null) errorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        errorText.fontSize = 24;
        errorText.alignment = TextAnchor.MiddleCenter;
        errorText.color = Color.white;
        errorText.text = "通信状況を確認してタップでリトライ";

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(360f, -612f);
        rt.sizeDelta = new Vector2(600f, 80f);

        go.SetActive(false);
    }

    void ShowConnectionError()
    {
        errorShown = true;
        if (errorText != null) errorText.gameObject.SetActive(true);
    }

    public void Reopen()
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        if (root.parent != null) root.SetAsLastSibling();
        IsShowing = true;
        errorShown = false;
        sessionWaitTimer = 0f;
        if (errorText != null) errorText.gameObject.SetActive(false);
        AdVirtuaMonitorSetup.Hide();
    }

    void Close()
    {
        IsShowing = false;
        if (root != null) root.gameObject.SetActive(false);
        AdVirtuaMonitorSetup.Show();
    }
}
