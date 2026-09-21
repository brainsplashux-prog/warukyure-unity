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
    // 2026-09-21 社長原文「タイトルに宝石を４つ並べる、宝石をタップするとリストが出る、
    // レートを選択したらスタートボタンをタップする、そのレートでゲーム開始」。
    // 選択状態・残高・PlayerPrefsの正本は WarukyureBoard 側(JemSelectedGemIndex等)。
    Font jemFont;
    readonly Image[] jemGemBg = new Image[4];
    readonly Image[] jemGemIcons = new Image[4];
    readonly Text[] jemGemBalanceTexts = new Text[4];
    readonly Button[] jemGemButtons = new Button[4];
    GameObject jemRatePanel;
    Image jemRatePanelGemIcon;
    Text jemRatePanelBalance;
    Button[] jemRateButtons;
    Image[] jemRateBg;
    int jemRatePanelGemIndex = -1;
    int jemRatePanelSelectedRate = -1;   // パネル内で選択中のレート(-1=未選択)
    Button jemRatePanelStartButton;      // パネル内「出陣」ボタン(ポイ之信BuildJemRatePanelと同じ構成)
    Image jemRatePanelStartBg;
    Text noticeText;
    float noticeTimer;
    const float NoticeSeconds = 1.8f;
    static readonly int[] JemRates = { 1, 2, 5, 10, 20, 50, 100 };
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
        BuildJemUi();
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
        // 残高取得・選択状態の変化をポーリングで反映する（板絵・当たり判定の
        // 生ポインタ方式と同じく、イベントに頼らず毎フレーム現状を描き直す）。
        UpdateJemVisuals();
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

#if JEM_BUILD
        // レート選択パネル表示中は背面の START へ貫通させない
        // （パネル内ボタンは uGUI Button が処理する。ここは生ポインタ読みのため明示ガード）。
        if (jemRatePanel != null && jemRatePanel.activeSelf) return;
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
    // START タップ時の JEM ゲート。宝石未選択・残高不明・残高<選択レートでは盤面へ入らない
    // （消費は盤面SPIN以降。ここでは「選んだ宝石・レートで入場してよいか」だけ判定する）。
    bool TryStartJemGame()
    {
        board.RequestJemBalances();
        if (!board.JemBalancesReady)
        {
            ShowNotice("残高を確認しています。少し待ってください");
            return false;
        }
        if (board.JemSelectedGemIndex < 0)
        {
            ShowNotice("宝石をタップしてレートをえらんでください");
            return false;
        }
        int? bal = board.GetJemGemBalance(board.JemSelectedGemIndex);
        if (!bal.HasValue || bal.Value <= 0)
        {
            ShowNotice("その宝石は持っていません");
            return false;
        }
        if (bal.Value < board.JemSelectedRate)
        {
            // 保存済みレートが残高を超える時は、そのまま消費させずレート選択を要求する。
            OpenJemRatePanel(board.JemSelectedGemIndex);
            ShowNotice("残高にあわせてレートをえらんでください");
            return false;
        }
        return true;
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
        if (errorText != null) errorText.gameObject.SetActive(true);
    }

#if JEM_BUILD
    // ----------------- JEM 宝石/レート選択UI -----------------
    // レイアウト(2026-09-21レビュー是正): 4種の宝石ボタンはSTART当たり判定
    // (y=924..1074)より上・通知テキスト(y=864..916)より下の帯 y=720..856 に横並びで置く。
    // 初回差分の y=1080..1216 は TitleBottomSlack(=ComputeTitleLiftが画面外へ逃がす
    // 下端150px)と同じ区間だったため、可視デザイン高さが1224未満の機種では全て画面外
    // だった。y=720..856 は rootが最大150px下がった状態(可視高さ1074)でも可視範囲
    // design-y[0..1074]に完全に収まり、START hit・通知文とも重ならない。
    // 原画の絵・START位置は一切変えず、UIのみ前面に重ねる。
    void BuildJemUi()
    {
        jemFont = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (jemFont == null) jemFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        const float gemRowY = 720f, gemW = 160f, gemH = 136f, gemGap = 16f;
        float gemStartX = (DesignW - 4f * gemW - 3f * gemGap) / 2f; // =16
        for (int i = 0; i < 4; i++)
        {
            float x = gemStartX + i * (gemW + gemGap);
            GameObject cell = new GameObject("TitleGem" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cell.transform.SetParent(root, false);
            RectTransform crt = cell.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(x, -gemRowY);
            crt.sizeDelta = new Vector2(gemW, gemH);

            Image bg = cell.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            jemGemBg[i] = bg;

            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(cell.transform, false);
            RectTransform irt = iconGO.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, -4f);
            irt.sizeDelta = new Vector2(100f, 100f);
            Image icon = iconGO.GetComponent<Image>();
            icon.sprite = board != null ? board.GetJemGemSprite(i) : null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            jemGemIcons[i] = icon;

            Text bal = JemText(cell.transform, "Balance", new Vector2(0f, 104f), new Vector2(gemW, 30f), 24, Color.white);
            bal.fontStyle = FontStyle.Bold;
            bal.text = "×-";
            jemGemBalanceTexts[i] = bal;

            Button btn = cell.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.interactable = false; // 残高取得までは選択不可(=残高不明から消費を始めない)
            int captured = i;
            btn.onClick.AddListener(() => OnJemGemTapped(captured));
            jemGemButtons[i] = btn;
        }

        BuildJemRatePanel();
        CreateNoticeText();
    }

    // 宝石タップ → レート選択パネル。選択の確定自体は盤面SPINではなくタイトルSTART。
    void BuildJemRatePanel()
    {
        GameObject overlay = new GameObject("JemRatePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(root, false);
        RectTransform ort = overlay.GetComponent<RectTransform>();
        ort.anchorMin = new Vector2(0f, 1f);
        ort.anchorMax = new Vector2(0f, 1f);
        ort.pivot = new Vector2(0f, 1f);
        ort.anchoredPosition = Vector2.zero;
        ort.sizeDelta = new Vector2(DesignW, DesignH);
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        dim.raycastTarget = true; // 背面の宝石ボタン・STARTへのタップを吸収(閉じるのは「とじる」のみ)
        jemRatePanel = overlay;

        const float cardW = 660f, cardH = 480f;
        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(overlay.transform, false);
        RectTransform cart = card.GetComponent<RectTransform>();
        cart.anchorMin = new Vector2(0f, 1f);
        cart.anchorMax = new Vector2(0f, 1f);
        cart.pivot = new Vector2(0f, 1f);
        cart.anchoredPosition = new Vector2((DesignW - cardW) / 2f, -(DesignH - cardH) / 2f);
        cart.sizeDelta = new Vector2(cardW, cardH);
        Image cardImg = card.GetComponent<Image>();
        cardImg.color = new Color(0.05f, 0.08f, 0.18f, 0.95f);
        cardImg.raycastTarget = true;

        Text title = JemText(card.transform, "Title", new Vector2(0f, 16f), new Vector2(cardW - 40f, 36f), 26, new Color(1f, 0.82f, 0.24f));
        title.fontStyle = FontStyle.Bold;
        title.text = "レートをえらぶ";

        GameObject iconGO = new GameObject("GemIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(card.transform, false);
        RectTransform girt = iconGO.GetComponent<RectTransform>();
        girt.anchorMin = new Vector2(0f, 1f);
        girt.anchorMax = new Vector2(0f, 1f);
        girt.pivot = new Vector2(0f, 1f);
        girt.anchoredPosition = new Vector2((cardW - 96f) / 2f, -60f);
        girt.sizeDelta = new Vector2(96f, 96f);
        jemRatePanelGemIcon = iconGO.GetComponent<Image>();
        jemRatePanelGemIcon.preserveAspect = true;
        jemRatePanelGemIcon.raycastTarget = false;

        jemRatePanelBalance = JemText(card.transform, "Balance", new Vector2(0f, 164f), new Vector2(cardW - 40f, 30f), 22, Color.white);

        jemRateButtons = new Button[JemRates.Length];
        jemRateBg = new Image[JemRates.Length];
        const float rateW = 80f, rateH = 64f, rateY = 210f, rateGap = 10f;
        float rateStartX = (cardW - JemRates.Length * rateW - (JemRates.Length - 1) * rateGap) / 2f;
        for (int i = 0; i < JemRates.Length; i++)
        {
            int rate = JemRates[i];
            float x = rateStartX + i * (rateW + rateGap);
            GameObject rgo = new GameObject("Rate" + rate, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            rgo.transform.SetParent(card.transform, false);
            RectTransform rrt = rgo.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.anchoredPosition = new Vector2(x, -rateY);
            rrt.sizeDelta = new Vector2(rateW, rateH);
            Image rbg = rgo.GetComponent<Image>();
            rbg.color = new Color(0.20f, 0.24f, 0.40f, 1f);
            jemRateBg[i] = rbg;

            Text label = JemText(rgo.transform, "Label", Vector2.zero, new Vector2(rateW, rateH), 24, Color.white);
            label.fontStyle = FontStyle.Bold;
            label.text = "×" + rate;

            Button rbtn = rgo.GetComponent<Button>();
            rbtn.targetGraphic = rbg;
            int capturedRate = rate;
            rbtn.onClick.AddListener(() => OnJemRateTapped(capturedRate));
            jemRateButtons[i] = rbtn;
        }

        Text hint = JemText(card.transform, "Hint", new Vector2(0f, 288f), new Vector2(cardW - 40f, 28f), 20, new Color(0.8f, 0.85f, 0.95f));
        hint.text = "レートをえらんで 出陣 をおしてください";

        // パネル内START(出陣)。ポイ之信 BuildJemRatePanel と同じ構成:
        // レートボタンは選択+保存だけで開始せず、このボタンでだけ盤面へ入る。
        // レート未選択の間は押せない(interactable=false)。
        GameObject sgo = new GameObject("Start", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        sgo.transform.SetParent(card.transform, false);
        RectTransform srt = sgo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(0f, 1f);
        srt.pivot = new Vector2(0f, 1f);
        srt.anchoredPosition = new Vector2((cardW - 320f) / 2f, -330f);
        srt.sizeDelta = new Vector2(320f, 76f);
        jemRatePanelStartBg = sgo.GetComponent<Image>();
        jemRatePanelStartBg.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        Text sLabel = JemText(sgo.transform, "Label", Vector2.zero, new Vector2(320f, 76f), 30, Color.white);
        sLabel.fontStyle = FontStyle.Bold;
        sLabel.text = "出陣";
        jemRatePanelStartButton = sgo.GetComponent<Button>();
        jemRatePanelStartButton.targetGraphic = jemRatePanelStartBg;
        jemRatePanelStartButton.interactable = false;
        jemRatePanelStartButton.onClick.AddListener(OnJemRateStartPressed);

        GameObject cgo = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cgo.transform.SetParent(card.transform, false);
        RectTransform clrt = cgo.GetComponent<RectTransform>();
        clrt.anchorMin = new Vector2(0f, 1f);
        clrt.anchorMax = new Vector2(0f, 1f);
        clrt.pivot = new Vector2(0f, 1f);
        clrt.anchoredPosition = new Vector2((cardW - 200f) / 2f, -416f);
        clrt.sizeDelta = new Vector2(200f, 56f);
        Image cbg = cgo.GetComponent<Image>();
        cbg.color = new Color(0.32f, 0.32f, 0.34f, 1f);
        Text cLabel = JemText(cgo.transform, "Label", Vector2.zero, new Vector2(200f, 56f), 24, Color.white);
        cLabel.text = "とじる";
        Button cbtn = cgo.GetComponent<Button>();
        cbtn.targetGraphic = cbg;
        cbtn.onClick.AddListener(() => { WarukyureSfx.PlayTap(); CloseJemRatePanel(); });

        overlay.SetActive(false);
    }

    void CreateNoticeText()
    {
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

    Text JemText(Transform parent, string name, Vector2 posFromTopCenter, Vector2 size, int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posFromTopCenter.x, -posFromTopCenter.y - size.y / 2f);
        rt.sizeDelta = size;
        Text t = go.AddComponent<Text>();
        t.font = jemFont;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    void OnJemGemTapped(int index)
    {
        WarukyureSfx.PlayTap();
        if (board == null || !board.SelectJemGemForTitle(index)) return;
        OpenJemRatePanel(index);
    }

    void OpenJemRatePanel(int index)
    {
        jemRatePanelGemIndex = index;
        if (jemRatePanel == null) return;
        // 初期選択=保存済みレート(ポイ之信と同じ)。残高がそのレートに満たない時は
        // 未選択で開き、パネル内STARTは押せないままにする。
        int? bal = board != null ? board.GetJemGemBalance(index) : null;
        jemRatePanelSelectedRate = bal.HasValue && board.JemSelectedRate > 0
            && bal.Value >= board.JemSelectedRate ? board.JemSelectedRate : -1;
        jemRatePanel.SetActive(true);
        UpdateJemVisuals();
    }

    void CloseJemRatePanel()
    {
        if (jemRatePanel != null) jemRatePanel.SetActive(false);
        jemRatePanelGemIndex = -1;
        jemRatePanelSelectedRate = -1;
    }

    // レートタップ＝選択+保存(社長原文「選択したら保存」)。賭け・開始はしない。
    // パネルは閉じず、パネル内「出陣」でだけ盤面へ進む(ポイ之信と同じ構成)。
    void OnJemRateTapped(int rate)
    {
        WarukyureSfx.PlayTap();
        if (board == null || jemRatePanelGemIndex < 0) return;
        int? bal = board.GetJemGemBalance(jemRatePanelGemIndex);
        if (!bal.HasValue || bal.Value < rate) return; // 残高不足のレートは選べない(二重防御)
        if (!board.SelectJemRateForTitle(rate)) return;
        board.SelectJemGemForTitle(jemRatePanelGemIndex); // パネルを開いた宝石を選択確定として維持
        jemRatePanelSelectedRate = rate;
        UpdateJemVisuals();
    }

    // パネル内「出陣」= 選択中の宝石・レートで盤面へ入る。
    // 連打での二重開始防止のため押した瞬間にボタンを無効化する。
    void OnJemRateStartPressed()
    {
        WarukyureSfx.PlayTap();
        if (board == null || !board.IsSessionReady) return;
        if (jemRatePanelGemIndex < 0 || jemRatePanelSelectedRate < 0) return;
        int? bal = board.GetJemGemBalance(jemRatePanelGemIndex);
        if (!bal.HasValue || bal.Value < jemRatePanelSelectedRate) return; // 二重防御
        if (board.JemSelectedGemIndex != jemRatePanelGemIndex
            && !board.SelectJemGemForTitle(jemRatePanelGemIndex)) return;
        if (jemRatePanelStartButton != null) jemRatePanelStartButton.interactable = false;
        CloseJemRatePanel();
        Close(); // タイトルを閉じてゲーム画面へ(開始経路はこことタイトル原画STARTの2本)
    }

    // 残高・選択状態を毎フレーム反映する。表示だけを書き換え、状態は board 側が正本。
    void UpdateJemVisuals()
    {
        if (board == null || jemGemButtons == null) return;
        for (int i = 0; i < 4; i++)
        {
            int? bal = board.GetJemGemBalance(i);
            bool ready = bal.HasValue;
            bool canSelect = ready && bal.Value > 0;
            bool selected = i == board.JemSelectedGemIndex;
            if (jemGemButtons[i] != null) jemGemButtons[i].interactable = canSelect;
            if (jemGemBalanceTexts[i] != null)
                jemGemBalanceTexts[i].text = ready ? "×" + bal.Value.ToString("N0") : "×-";
            if (jemGemIcons[i] != null)
                jemGemIcons[i].color = canSelect ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (jemGemBg[i] != null)
                jemGemBg[i].color = selected ? new Color(1f, 0.78f, 0.25f, 0.9f)
                    : canSelect ? new Color(0f, 0f, 0f, 0.45f)
                    : new Color(0f, 0f, 0f, 0.2f);
        }

        if (jemRatePanel != null && jemRatePanel.activeSelf && jemRatePanelGemIndex >= 0)
        {
            int? bal = board.GetJemGemBalance(jemRatePanelGemIndex);
            int balance = bal ?? 0;
            if (jemRatePanelGemIcon != null)
                jemRatePanelGemIcon.sprite = board.GetJemGemSprite(jemRatePanelGemIndex);
            if (jemRatePanelBalance != null)
                jemRatePanelBalance.text = bal.HasValue ? "所持: ×" + balance.ToString("N0") : "所持: ×-";
            for (int i = 0; i < JemRates.Length; i++)
            {
                bool affordable = bal.HasValue && balance >= JemRates[i];
                bool isSelected = JemRates[i] == jemRatePanelSelectedRate;
                if (jemRateButtons[i] != null) jemRateButtons[i].interactable = affordable;
                if (jemRateBg[i] != null)
                    jemRateBg[i].color = !affordable ? new Color(0.25f, 0.25f, 0.25f, 0.6f)
                        : isSelected ? new Color(1f, 0.72f, 0.10f, 1f)   // 選択中レートを強調(前回保存値が既定)
                        : new Color(0.20f, 0.24f, 0.40f, 1f);
            }
            // パネル内STARTは、パネルを開いた宝石が選択済みかつ利用可能なレートが
            // 選ばれている時だけ押せる(宝石未選択・レート未選択では開始しない)。
            bool canStart = jemRatePanelSelectedRate > 0 && bal.HasValue
                && balance >= jemRatePanelSelectedRate
                && board.JemSelectedGemIndex == jemRatePanelGemIndex;
            if (jemRatePanelStartButton != null && jemRatePanelStartButton.interactable != canStart)
            {
                jemRatePanelStartButton.interactable = canStart;
                if (jemRatePanelStartBg != null)
                    jemRatePanelStartBg.color = canStart ? new Color(1f, 0.72f, 0.10f, 1f)
                        : new Color(0.4f, 0.4f, 0.4f, 0.6f);
            }
        }
    }
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
        // タイトル再表示時: パネルは閉じた状態へ戻し、残高未取得なら取得を再要求する。
        // 前回選択したレートは PlayerPrefs から復元済みで既定強調される(再読み込み不要)。
        if (jemRatePanel != null) jemRatePanel.SetActive(false);
        jemRatePanelGemIndex = -1;
        jemRatePanelSelectedRate = -1;
        noticeTimer = 0f;
        if (noticeText != null) noticeText.gameObject.SetActive(false);
        if (board != null) board.RequestJemBalances();
#endif
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
