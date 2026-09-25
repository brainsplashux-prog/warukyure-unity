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

    // 2026-09-15 社長指摘「タイトルが上に引っ張られてロゴがLv帯に隠れる」対応。
    // StartHit下端(1074)から画像下端(DesignH=1224)までの150pxは何も描かれていない空白
    // （edge-detection実測: ロゴ本体の輪郭密度が上昇し始めるのは design-y≈155-165px、
    //   それ以降キャラ絵まで輪郭密度は高いまま＝空白はStartHit下端より下の150pxだけ）。
    // 実可視デザイン高さH(=Screen.height*720/Screen.width)が1224未満の機種では、
    // その空白分だけ画像一式(TitleBg/StartHit/エラー文=root配下すべて一体)を下へずらし、
    // ロゴ側の可視域を広げる。StartHitは画像と完全に一体で動くため
    // 「今と全く同じ状態で100%可視・押下可能」は不変（中身の相対配置は無変更）。
    // 数値の全根拠: harness/reports/20260915-warukyure-stage-top.md
    static readonly float StartHitBottom = StartHit.y + StartHit.height;      // 1074
    static readonly float TitleBottomSlack = DesignH - StartHitBottom;        // 150

    static float ComputeTitleLift()
    {
        if (Screen.width <= 0) return 0f;
        float visibleDesignH = Screen.height * DesignW / Screen.width; // CanvasScaler実測換算
        float overflow = Mathf.Max(0f, DesignH - visibleDesignH);       // 旧方式でロゴ側が隠れる量
        return Mathf.Min(TitleBottomSlack, overflow);                   // 空白(150)の範囲内でだけ下げる
    }

    public static bool IsShowing { get; private set; }

    Canvas canvas;
    WarukyureBoard board;
    RectTransform root;
    RectTransform startHitRect;
    Text errorText;
    Camera hitCamera;
    float liftScreenW, liftScreenH;

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
        // 2026-09-15 社長指示（全幅化・START 100%可視）: 下端アンカーにする。
        // CanvasScalerはmatchWidthOrHeight=0(幅基準)固定のため、実測の可視デザイン高さは
        // 端末のアスペクト比次第で1224未満になる（WarukyureBoard.CreateBoardRoot()と同じ理由）。
        // 上端アンカーのままだと下端にあるSTARTボタン当たり判定が可視範囲外に落ちるため、
        // 背景画像・当たり判定を一体のまま下端へ吸着させ、はみ出しはSTARTより離れた上端側
        // (キャラ絵の上部等)で吸収する。中身(bg/StartHit/エラー文)の相対配置は無変更。
        // 詳細: harness/reports/20260915-warukyure-noreload-fullwidth.md
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0f, 0f);
        // 2026-09-15: 実可視高さHが1224未満の機種では下端固定(0)のままだとロゴがLv帯側で
        // 隠れるため、StartHit下端の空白(最大150)だけ上へ食い込ませず下へ動かす
        // （=キャンバス内でのrootの位置を下げる。中身の相対配置・当たり判定は無変更）。
        root.anchoredPosition = new Vector2(0f, -ComputeTitleLift());
        root.sizeDelta = new Vector2(DesignW, DesignH);
        root.SetAsLastSibling(); // 常に最前面
        liftScreenW = Screen.width;
        liftScreenH = Screen.height;

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
        Poicasi.Chrome.PoicasiChromeBridge.SetScreen(false); // タイトル表示中はヘッダーA+B
    }

    void Update()
    {
        if (root == null) return;
        // 2026-09-15: リサイズ/回転/iOSツールバー表示切替でHが変わってもズレたままにしない。
        // 既存の AdVirtuaResizeWatcher と同じ Screen.width/height ポーリング方式。
        if (!Mathf.Approximately(Screen.width, liftScreenW) || !Mathf.Approximately(Screen.height, liftScreenH))
        {
            liftScreenW = Screen.width;
            liftScreenH = Screen.height;
            root.anchoredPosition = new Vector2(0f, -ComputeTitleLift());
        }

        if (!IsShowing || startHitRect == null) return;
        if (hitCamera == null) ResolveCamera();

        // 2026-09-25 是正（社長「押しても何も反応しない…エラーならポップアップ」）:
        // 接続待ちが SessionTimeout を超えたら、画面内の小さな文字ではなく
        // 既存の poierr（board.FailSession 経由）で知らせ、再試行／戻るを出す。
        if (board != null && !board.IsSessionReady && !board.IsSessionFailed)
        {
            sessionWaitTimer += Time.deltaTime;
            if (sessionWaitTimer >= SessionTimeout)
            {
                sessionWaitTimer = 0f;
                board.FailSession("E-TIMEOUT");
            }
        }
        else
        {
            sessionWaitTimer = 0f;
            if (errorShown)
            {
                errorShown = false;
                if (errorText != null) errorText.gameObject.SetActive(false);
            }
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

        // sessionReady 前は残高・ミッション未取得のまま盤面へ入るのを防ぐため閉じない。
        // 2026-09-25 是正: 以前はここで無言 return していた（START を押しても無反応）。
        // 接続失敗済みなら poierr を出し直し、接続中なら「通信中…」を出す。
        if (board == null) return;
        if (!board.IsSessionReady)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(startHitRect, pos, hitCamera)) return;
            if (board.IsSessionFailed) board.ShowSessionError();
            else ShowConnectingText();
            return;
        }

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
        errorText.text = "通信中…";

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(360f, -612f);
        rt.sizeDelta = new Vector2(600f, 80f);

        go.SetActive(false);
    }

    void ShowConnectingText()
    {
        errorShown = true;
        if (errorText == null) return;
        errorText.text = "通信中…";
        errorText.gameObject.SetActive(true);
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
        Poicasi.Chrome.PoicasiChromeBridge.SetScreen(false); // タイトル再表示中はヘッダーA+B
    }

    void Close()
    {
        IsShowing = false;
        if (root != null) root.gameObject.SetActive(false);
        AdVirtuaMonitorSetup.Show();
        Poicasi.Chrome.PoicasiChromeBridge.SetScreen(true); // タイトルを閉じてプレイ開始 → ヘッダーAのみ
    }
}
