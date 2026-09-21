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

#if JEM_BUILD
    // ----------------- JEM 宝石/レート選択 -----------------
    // 2026-09-21 社長指示: 宝石行・レートパネルはブラウザ共通部品 jem-selector が描く。
    // Unity側の個別UI(宝石ボタン・レートパネル・宝石画像パス・固定4配列)は廃止し、
    // ここには通知テキストと原画STARTタップのゲートだけを残す。
    // 選択状態・残高map・PlayerPrefsの正本は WarukyureBoard 側(JemSelectedAssetCode等)。
    Font jemFont;
    Text noticeText;
    float noticeTimer;
    const float NoticeSeconds = 1.8f;
    bool jemSelectorError;   // 共通selectorのロード/契約失敗(fail-closed。旧UIへ戻さない)
#endif

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
#if JEM_BUILD
        CreateNoticeText();
        // 共通selectorのロード＋mountを開始する（ロード完了後にJS側から
        // WarukyureBoard.OnJemSelectorReady へ通知が来る）。
        if (board != null) board.MountJemSelector();
#endif
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

        if (!errorShown && board != null && !board.IsSessionReady)
        {
            sessionWaitTimer += Time.deltaTime;
            if (sessionWaitTimer >= SessionTimeout)
                ShowConnectionError();
        }

        ReadTap();
#if JEM_BUILD
        if (noticeTimer > 0f)
        {
            noticeTimer -= Time.deltaTime;
            if (noticeTimer <= 0f && noticeText != null) noticeText.gameObject.SetActive(false);
        }
        // 残高取得・選択状態の変化を共通UIへ反映する（板絵・当たり判定の
        // 生ポインタ方式と同じく、イベントに頼らず毎フレーム差分送信する）。
        if (board != null) board.PushJemSelectorState();
#endif
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
        // 共通selectorのロード失敗表示中はタップで再ロードを試みる。
        bool selectorErr = false;
#if JEM_BUILD
        selectorErr = jemSelectorError;
#endif
        if (errorShown || selectorErr)
        {
#if JEM_BUILD
            if (jemSelectorError)
            {
                jemSelectorError = false;
                if (board != null) board.MountJemSelector();
            }
#endif
            if (errorShown)
            {
                if (board != null) board.RetrySession();
                errorShown = false;
                sessionWaitTimer = 0f;
            }
            if (errorText != null) errorText.gameObject.SetActive(false);
            return;
        }

        // sessionReady 前は残高・ミッション未取得のまま盤面へ入るのを防ぐため閉じない。
        if (board == null || !board.IsSessionReady) return;

#if JEM_BUILD
        // 共通レートパネル表示中は背面の START へ貫通させない
        // （パネルはDOM側が描く。ここは生ポインタ読みのため明示ガード）。
        if (board.IsJemSelectorPanelOpen()) return;
#endif

        if (RectTransformUtility.RectangleContainsScreenPoint(startHitRect, pos, hitCamera))
        {
#if JEM_BUILD
            if (!TryStartJemGame()) return;
#endif
            Close();
        }
    }

#if JEM_BUILD
    // START タップ時の JEM ゲート。共通selector未ロード/失敗・宝石未選択・残高不明・
    // 残高<選択レートでは盤面へ入らない
    // （消費は盤面SPIN以降。ここでは「選んだ宝石・レートで入場してよいか」だけ判定する）。
    bool TryStartJemGame()
    {
        if (board.JemSelectorFailed || jemSelectorError)
        {
            ShowNotice("宝石選択の読み込みに失敗しました。タップでリトライ");
            return false;
        }
        board.RequestJemBalances();
        if (!board.JemBalancesReady || !board.JemSelectorReady)
        {
            ShowNotice("残高を確認しています。少し待ってください");
            return false;
        }
        if (string.IsNullOrEmpty(board.JemSelectedAssetCode))
        {
            // 未選択のままでは開始しない。共通UIの宝石行が既に画面上に出ているため
            // ここではパネル誘導の通知だけ行う（別ルールで即開始しない）。
            ShowNotice("宝石をタップしてレートをえらんでください");
            return false;
        }
        int? bal = board.GetJemGemBalance(board.JemSelectedAssetCode);
        if (!bal.HasValue || bal.Value <= 0)
        {
            ShowNotice("その宝石は持っていません");
            return false;
        }
        if (bal.Value < board.JemSelectedRate)
        {
            // 保存済みレートが残高を超える時は、そのまま消費させずレート選択を要求する。
            ShowNotice("残高にあわせてレートをえらんでください");
            return false;
        }
        return true;
    }

    // 共通selectorの「出陣」からの開始(WarukyureBoard.OnJemStart 経由)。
    // 成功したらタイトルを閉じてゲーム画面へ。失敗時はfalseを返し、呼び出し側が
    // releaseStart()で再試行可能に戻す。
    public bool CloseFromJemSelector()
    {
        if (root == null || !IsShowing) return false;
        if (board == null || !board.IsSessionReady) return false;
        Close();
        return true;
    }

    // 共通selectorのロード/契約失敗を明示する（開始は拒否。旧UIへは戻さない）。
    public void OnJemSelectorFailed(string message)
    {
        jemSelectorError = true;
        if (errorText != null)
        {
            errorText.text = "宝石選択の読み込みに失敗しました。タップでリトライ";
            errorText.gameObject.SetActive(true);
        }
    }
#endif

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
        if (errorText != null)
        {
            errorText.text = "通信状況を確認してタップでリトライ";
            errorText.gameObject.SetActive(true);
        }
    }

#if JEM_BUILD
    // ----------------- JEM 通知テキスト -----------------
    // 宝石行・レートパネルのDOM/CSSは共通部品(shared jem-selector)が描くため、
    // Unity側には通知テキストだけを残す（共通UIの選択状態を壊さない範囲の表示物）。
    void CreateNoticeText()
    {
        jemFont = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (jemFont == null) jemFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject go = new GameObject("TitleNotice", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(root, false);
        noticeText = go.AddComponent<Text>();
        noticeText.font = jemFont;
        noticeText.fontSize = 26;
        noticeText.fontStyle = FontStyle.Bold;
        noticeText.alignment = TextAnchor.MiddleCenter;
        noticeText.color = Color.white;
        noticeText.raycastTarget = false;
        // START直上(y=868..920)に出す。背景が暗い帯なので白文字＋影で読めるようにする。
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -864f);
        rt.sizeDelta = new Vector2(680f, 52f);
        go.SetActive(false);
    }

    void ShowNotice(string message)
    {
        if (noticeText == null) return;
        noticeText.text = message;
        noticeText.gameObject.SetActive(true);
        noticeTimer = NoticeSeconds;
    }

    // WarukyureBoard(FailJemSelectorStart)からの通知表示用。
    public void ShowJemNotice(string message) => ShowNotice(message);
#endif

    public void Reopen()
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        if (root.parent != null) root.SetAsLastSibling();
        IsShowing = true;
        errorShown = false;
        sessionWaitTimer = 0f;
        if (errorText != null) errorText.gameObject.SetActive(false);
#if JEM_BUILD
        // タイトル再表示時: 共通UIを再表示し、残高未取得なら取得を再要求する。
        // 開いていたレートパネルは共通側の hide() で閉じられる。
        // 前回選択したレートは PlayerPrefs から復元済みで既定強調される(再読み込み不要)。
        noticeTimer = 0f;
        if (noticeText != null) noticeText.gameObject.SetActive(false);
        if (board != null)
        {
            board.MountJemSelector();   // 未ロード/失敗後の再試行としても機能する
            board.ShowJemSelector();
            board.RequestJemBalances();
        }
#endif
        AdVirtuaMonitorSetup.Hide();
        Poicasi.Chrome.PoicasiChromeBridge.SetScreen(false); // タイトル再表示中はヘッダーA+B
    }

    void Close()
    {
        IsShowing = false;
#if JEM_BUILD
        // タイトルを閉じたら共通UIも隠す（開いているパネルも共通側で閉じられる）。
        if (board != null) board.HideJemSelector();
#endif
        if (root != null) root.gameObject.SetActive(false);
        AdVirtuaMonitorSetup.Show();
        Poicasi.Chrome.PoicasiChromeBridge.SetScreen(true); // タイトルを閉じてプレイ開始 → ヘッダーAのみ
    }
}
