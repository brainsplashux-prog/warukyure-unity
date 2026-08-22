using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class WarukyureBoard : MonoBehaviour
{
    // ----------------- configuration -----------------
    const string API_URL = "https://b5yl9sml5l.execute-api.ap-northeast-1.amazonaws.com/";
    const string TOKEN_KEY = "warukyure_token";
    const int WAGER_PER_BET = 100;
    const float RUN_DURATION = 2.0f;
    const float HOLD_DURATION = 0.5f;
    const int MIN_PATH_STEPS = 35;
    const string API_RETRY_MSG = "通信エラー。もう一度お試しください";

    // 社長指示(2026-08-19)の3段階ルーレット
    const float LAMP_SPEED_FAST = 20f;  // マス/秒。従来＝約40マスを2.0秒＝約20マス/秒 と等速
    const float LAMP_SPEED_MID  = 10f;  // 「今のスピードの半分」
    const float LAMP_SPEED_SLOW = 5f;   // 「1秒間に5マス」
    const int   LAMP_LAPS_FAST  = 2;    // 高速で2周
    const float LAMP_LAPS_MID   = 0.5f; // 「ちょっと早い」が長いので半周で遅いに切り替える（2026-08-19 社長指示）

    // ----------------- UI references -----------------
    private Canvas canvas;
    private RectTransform lampRect;
    private Text walletText;
    private GameObject resultPanel;
    private RectTransform resultPanelRect;
    private CanvasGroup resultPanelGroup;
    private Text resultPanelText;
    private readonly Button[] betButtons = new Button[5];
    private readonly Image[] betButtonImages = new Image[5];
    private Button spinButton;
    private Text spinButtonText;
    private Image spinButtonImage;

    // ----------------- state -----------------
    private string token;
    private int wallet;
    private int lastNet;
    private int ballMask;
    private string currentRunId;
    private bool isRunning;
    // #16: init(token/state) が確定するまで BET/SPIN を受け付けない。
    private bool sessionReady;
    private bool skipRequested;
    private readonly List<float> lampSegSpeeds = new List<float>();
    private readonly List<Vector2> lampSizes = new List<Vector2>();
    private readonly List<string> lampTracks = new List<string>();
    private readonly List<string> lampCells = new List<string>();
    private readonly Dictionary<string, Texture2D> lampTex = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, GameObject> cellDimmerObjects = new Dictionary<string, GameObject>();
    private readonly HashSet<int> selectedBets = new HashSet<int>();
    private ResolveResponse lastResult;
    private readonly string[] betLabels = { "2", "4", "6", "8", "20" };
    private readonly string[] ballNames = { "うさぎ", "ねこ", "くま", "ことり" };
    // 城下コレクションパネルの4玉（art_final_v4 はパネル内が空。玉は実行時に重ねる）
    private readonly RawImage[] collectionBalls = new RawImage[4];
    private readonly Texture2D[] ballTexOn = new Texture2D[4];
    private readonly Texture2D[] ballTexOff = new Texture2D[4];
    private readonly string[] jpAwardLabels = { "3000", "1000", "30000", "1000", "5000" };
    private Coroutine overlayRoutine;
    private long lastErrorCode = 0;
    private string lastErrorBody = null;
    private string currentLampCellId = null;

    // ----------------- JACKPOT challenge overlay -----------------
    private GameObject jackpotPanel;
    private CanvasGroup jackpotPanelGroup;
    private RectTransform[] jackpotLampRects = new RectTransform[5];
    private Text[] jackpotLampTexts = new Text[5];
    private RectTransform jackpotIndicatorRect;
    private Text jackpotAwardText;
    private RectTransform adVirtuaRect;

    // ----------------- poifx bridge -----------------
    private bool poiFxPending;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PoiFxJackpot(string tier, int amount, string unit, string gameObjectName, string onDoneMethod);

    [DllImport("__Internal")]
    private static extern void PoiFxSkip();

    // poicasi-auth ブリッジ（Assets/Plugins/WebGL/WarukyureAuth.jslib）
    [DllImport("__Internal")]
    private static extern IntPtr WkTakePaCode();

    [DllImport("__Internal")]
    private static extern void WkFreePaCode(IntPtr ptr);
#endif

    // URL hash から受け取った認可コードを 1 回だけ取り出す。無ければ null。
    private static string TakePaCode()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        IntPtr ptr = WkTakePaCode();
        if (ptr == IntPtr.Zero) return null;
        string code = Marshal.PtrToStringUTF8(ptr);
        WkFreePaCode(ptr);
        return code;
#else
        return null;
#endif
    }

    void Awake()
    {
        // WebGL のメインループを requestAnimationFrame ベースにし、
        // ブラウザのスタイル更新／CSS アニメーション開始機会を確保する。
#if UNITY_WEBGL && !UNITY_EDITOR
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
#endif
    }

    void Update()
    {
        // 累積プレイ時間の加算。本ゲームはタイトル/選択画面を持たず盤面がそのままプレイ画面なので、
        // クロスプロモのポップアップが開いている間だけ非加算とする（タブ非アクティブは PoiPlayTime 側で除外）。
        PoiPlayTime.Tick(!CrossPromoPopupUI.IsOpen);
    }

    void Start()
    {
        gameObject.name = "WarukyureBoard";
        SetupCanvas();
        CreateBoardImage();
        CreateCellDimmers();
        CreateLamp();
        CreateAdVirtuaPlaceholder();
        CreateAdPrLabel(); // ad-monetization.md L85 ステマ規制/景表法 PR表記
        AdVirtuaMonitorSetup.Setup();
        gameObject.AddComponent<AdVirtuaResizeWatcher>();
        CreateHeaderText();
        CreateHelpButton();
        CreateBetButtons();
        CreateSpinButton();
        CreateResultOverlay();
        CreateJackpotChallengeUI();
        gameObject.AddComponent<SoundMuteButton>(); // game-layout-standard.md §2b 共通サウンドミュートボタン

        StartCoroutine(InitSession());
        StartCoroutine(TryDebugForceFx());
        AdVirtuaMonitorSetup.Show();
    }

    // ----------------- setup -----------------
    void SetupCanvas()
    {
        GameObject canvasGO = new GameObject("Canvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 10f;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1224);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.0f;

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject eventGO = new GameObject("EventSystem");
        eventGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    void CreateBoardImage()
    {
        Texture2D tex = Resources.Load<Texture2D>("art_final_v4");
        if (tex == null)
        {
            Debug.LogError("[Warukyure] art_final_v4 texture not found.");
            return;
        }

        GameObject go = new GameObject("Board");
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(0, -405);
        rt.sizeDelta = new Vector2(720, 819);

        go.AddComponent<CanvasRenderer>();
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;

        CreateCollectionBalls(rt);
    }

    // art_final_v2 実測: パネル内枠 x463..587 / y233..331。2x2 のセル中心に直径42ptの玉を置く。
    static readonly Vector2[] CollectionBallPos =
    {
        new Vector2(494.25f, 257.75f), new Vector2(556.75f, 257.75f),
        new Vector2(494.25f, 306.25f), new Vector2(556.75f, 306.25f)
    };
    const float CollectionBallSize = 42f;

    void CreateCollectionBalls(RectTransform boardRect)
    {
        for (int i = 0; i < 4; i++)
        {
            ballTexOn[i] = Resources.Load<Texture2D>("ball_c" + i);
            ballTexOff[i] = Resources.Load<Texture2D>("ball_g" + i);

            GameObject bgo = new GameObject("CollectionBall" + i);
            bgo.transform.SetParent(boardRect, false);
            RectTransform brt = bgo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(CollectionBallSize, CollectionBallSize);
            brt.anchoredPosition = new Vector2(CollectionBallPos[i].x, -CollectionBallPos[i].y);

            bgo.AddComponent<CanvasRenderer>();
            RawImage bimg = bgo.AddComponent<RawImage>();
            bimg.raycastTarget = false;
            collectionBalls[i] = bimg;
        }
        UpdateCollectionPanel();
    }

    // 未取得はグレー（影だけ）、取得済みはキャラ色の玉。ballMask の各ビットで切替える。
    void UpdateCollectionPanel()
    {
        for (int i = 0; i < 4; i++)
        {
            if (collectionBalls[i] == null) continue;
            bool got = (ballMask & (1 << i)) != 0;
            Texture2D t = got ? ballTexOn[i] : ballTexOff[i];
            if (t != null) collectionBalls[i].texture = t;
            collectionBalls[i].enabled = (t != null);
        }
    }

    // マスの実寸（art_final.png のピクセル実測。outer 34x34 / ring4 36x36 / loop2 26x29・角丸r=5）
    static Vector2 CellSizeForTrack(string track)
    {
        switch (track)
        {
            case "loop2":  return new Vector2(26f, 29f);
            case "ring4":  return new Vector2(36f, 36f);
            case "castle": return new Vector2(36f, 36f); // 暗色は付けないがランプは通る
            default:       return new Vector2(34f, 34f); // outer
        }
    }

    // 角丸矩形テクスチャ。マス絵の角丸(r=5)に合わせる。4xスーパーサンプルでアンチエイリアス。
    Texture2D CreateRoundedRectTexture(int w, int h, float radius, Color color)
    {
        const int SS = 4;
        int W = w * SS, H = h * SS;
        float r = radius * SS;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < SS; sy++)
                {
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float px2 = x * SS + sx + 0.5f;
                        float py2 = y * SS + sy + 0.5f;
                        float dx = Mathf.Max(r - px2, px2 - (W - r), 0f);
                        float dy = Mathf.Max(r - py2, py2 - (H - r), 0f);
                        if (dx * dx + dy * dy <= r * r) hit++;
                    }
                }
                Color c = color;
                c.a = color.a * (hit / (float)(SS * SS));
                px[y * w + x] = c;
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    Texture2D CreateCircleTexture(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * size + x] = d <= r ? color : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // art_final は焼き込み1枚絵のためマス単体オブジェクトが無い。
    // 各マスの実寸・角丸に完全一致させた半透明の黒を重ね、マスだけを暗くする（＝消灯状態）。
    // 城は暗くしない（2026-08-19 社長指示「城は黒いのなしで」）。
    void CreateCellDimmers()
    {
        Dictionary<string, Texture2D> dimTex = new Dictionary<string, Texture2D>();
        foreach (var kv in BoardData.CellCenters)
        {
            string track = BoardData.GetTrack(kv.Key);
            if (track == "castle") continue; // 城は暗くしない
            Vector2 s = CellSizeForTrack(track);
            if (!dimTex.ContainsKey(track))
                dimTex[track] = CreateRoundedRectTexture((int)s.x, (int)s.y, 5f, Color.white);
            GameObject go = new GameObject("dim_" + kv.Key);
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(kv.Value.x, -kv.Value.y);
            rt.sizeDelta = s;
            go.AddComponent<CanvasRenderer>();
            RawImage im = go.AddComponent<RawImage>();
            im.texture = dimTex[track];
            im.color = new Color(0f, 0f, 0f, 0.45f); // 消灯マス
            im.raycastTarget = false;
            cellDimmerObjects[kv.Key] = go;
        }
    }

    void CreateLamp()
    {
        GameObject go = new GameObject("Lamp");
        go.transform.SetParent(canvas.transform, false);
        lampRect = go.AddComponent<RectTransform>();
        lampRect.anchorMin = new Vector2(0, 1);
        lampRect.anchorMax = new Vector2(0, 1);
        lampRect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 start;
        BoardData.TryGetCenter("o_01", out start);
        lampRect.anchoredPosition = new Vector2(start.x, -start.y);
        lampRect.sizeDelta = new Vector2(34, 34);

        go.AddComponent<CanvasRenderer>();
        RawImage img = go.AddComponent<RawImage>();
        img.texture = LampTexFor("outer");
        img.raycastTarget = false;
        img.color = Color.white;

        if (cellDimmerObjects.TryGetValue("o_01", out GameObject dim)) { dim.SetActive(false); currentLampCellId = "o_01"; }
    }

    Texture2D LampTexFor(string track)
    {
        if (!lampTex.ContainsKey(track))
        {
            Vector2 s = CellSizeForTrack(track);
            lampTex[track] = CreateRoundedRectTexture((int)s.x, (int)s.y, 5f, new Color32(255, 225, 90, 120));
        }
        return lampTex[track];
    }

    void ApplyLampCell(int idx)
    {
        if (idx < 0 || idx >= lampCells.Count) return;
        string cellId = lampCells[idx];
        if (cellId == currentLampCellId) return;
        if (cellDimmerObjects.TryGetValue(cellId, out GameObject currentDim))
            currentDim.SetActive(false);
        if (!string.IsNullOrEmpty(currentLampCellId) && cellDimmerObjects.TryGetValue(currentLampCellId, out GameObject prevDim))
            prevDim.SetActive(true);
        lampRect.sizeDelta = lampSizes[idx];
        RawImage ri = lampRect.GetComponent<RawImage>();
        if (ri != null) ri.texture = LampTexFor(lampTracks[idx]);
        currentLampCellId = cellId;
        WarukyureSfx.PlayLampStep();
    }

    void CreateAdVirtuaPlaceholder()
    {
        GameObject go = new GameObject("AdVirtua");
        go.transform.SetParent(canvas.transform, false);
        adVirtuaRect = go.AddComponent<RectTransform>();
        adVirtuaRect.anchorMin = new Vector2(0, 1);
        adVirtuaRect.anchorMax = new Vector2(0, 1);
        adVirtuaRect.pivot = new Vector2(0, 1);
        adVirtuaRect.anchoredPosition = Vector2.zero;
        adVirtuaRect.sizeDelta = new Vector2(720, 405);

        // ダーク板は本物の Ad-Virtua 3D モニターへ置き換える。
        // 枠（位置・サイズ）の意味だけを保つ空の RectTransform として残す。
    }

    // 広告枠の「PR」表記（ステマ規制・景表法。正本 ~/.claude/manuals/ad-monetization.md L85）。
    // 🛑 Ad-Virtua は常に最前面（同 §「最前面ルール」・例外なし）のため、広告矩形の内側へ
    //    重ねることは構造的に不可能。よって広告帯(720x405)の直下＝枠に接する左隅へ常時表示する。
    //    サウンドボタン(§2b)が右隅・帯直下なので、左右で対になる位置になる。
    void CreateAdPrLabel()
    {
        const float bandBottom = 405f;
        const float margin = 18f;
        const float w = 56f;
        const float h = 28f;

        GameObject plate = new GameObject("AdPrLabelPlate");
        plate.transform.SetParent(canvas.transform, false);
        RectTransform prt = plate.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 1);
        prt.anchorMax = new Vector2(0, 1);
        prt.pivot = new Vector2(0, 1);
        prt.anchoredPosition = new Vector2(margin, -(bandBottom + margin));
        prt.sizeDelta = new Vector2(w, h);
        Image bg = plate.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.62f);
        bg.raycastTarget = false;

        GameObject label = new GameObject("AdPrLabelText");
        label.transform.SetParent(plate.transform, false);
        RectTransform lrt = label.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        Text t = label.AddComponent<Text>();
        t.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 20;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = "PR";
    }

    Text CreateText(string name, Vector2 pos, Vector2 size, TextAnchor align, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y);
        rt.sizeDelta = size;

        Text txt = go.AddComponent<Text>();
        txt.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.alignment = align;
        txt.color = Color.white;
        return txt;
    }

    void CreateHeaderText()
    {
        const float walletY = 1080f;
        const float bandHeight = 48f;

        // BET/SPIN ボタンに重ならないよう、盤面フレーム下辺より上に半透明黒帯を敷く。
        GameObject bandGO = new GameObject("WalletTextBand");
        bandGO.transform.SetParent(canvas.transform, false);
        RectTransform bandRT = bandGO.AddComponent<RectTransform>();
        bandRT.anchorMin = new Vector2(0, 1);
        bandRT.anchorMax = new Vector2(1, 1);
        bandRT.pivot = new Vector2(0.5f, 0.5f);
        bandRT.anchoredPosition = new Vector2(0f, -walletY);
        bandRT.sizeDelta = new Vector2(0f, bandHeight);
        Image bandImg = bandGO.AddComponent<Image>();
        bandImg.color = new Color(0f, 0f, 0f, 0.45f);
        bandImg.raycastTarget = false;

        walletText = CreateText("WalletText", new Vector2(360f, walletY), new Vector2(720f, 36f), TextAnchor.MiddleCenter, 18);
        walletText.resizeTextForBestFit = true;
        walletText.horizontalOverflow = HorizontalWrapMode.Overflow;
        walletText.verticalOverflow = VerticalWrapMode.Overflow;
        walletText.text = "残高 ---";
    }

    void CreateResultOverlay()
    {
        GameObject go = new GameObject("ResultPanel");
        go.transform.SetParent(canvas.transform, false);
        resultPanel = go;

        resultPanelRect = go.AddComponent<RectTransform>();
        resultPanelRect.anchorMin = new Vector2(0, 1);
        resultPanelRect.anchorMax = new Vector2(0, 1);
        resultPanelRect.pivot = new Vector2(0.5f, 0.5f);
        resultPanelRect.anchoredPosition = new Vector2(360, -755.5f);
        resultPanelRect.sizeDelta = new Vector2(500, 160);

        go.AddComponent<CanvasRenderer>();
        RawImage img = go.AddComponent<RawImage>();
        Texture2D white = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        white.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
        white.Apply();
        img.texture = white;
        img.color = new Color32(0, 0, 0, 165);
        img.raycastTarget = true;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => DismissResultOverlay());

        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.alpha = 0;
        group.blocksRaycasts = false;
        resultPanelGroup = group;

        GameObject txtGO = new GameObject("ResultText");
        txtGO.transform.SetParent(go.transform, false);
        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = Vector2.zero;

        resultPanelText = txtGO.AddComponent<Text>();
        resultPanelText.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (resultPanelText.font == null) resultPanelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resultPanelText.fontSize = 24;
        resultPanelText.alignment = TextAnchor.MiddleCenter;
        resultPanelText.color = Color.white;
        resultPanelText.text = "";

        go.SetActive(false);
    }

    void CreateJackpotChallengeUI()
    {
        // 画面B：通常Header A + AdVirtua最前面。JPチャレンジは盤面を覆うオーバーレイ。
        GameObject go = new GameObject("JackpotPanel");
        go.transform.SetParent(canvas.transform, false);
        jackpotPanel = go;

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(360, -814.5f);
        rt.sizeDelta = new Vector2(720, 819);

        go.AddComponent<CanvasRenderer>();
        RawImage bg = go.AddComponent<RawImage>();
        Texture2D black = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        black.SetPixels(new Color[] { Color.black, Color.black, Color.black, Color.black });
        black.Apply();
        bg.texture = black;
        bg.color = new Color32(0, 0, 0, 200);
        bg.raycastTarget = true;

        jackpotPanelGroup = go.AddComponent<CanvasGroup>();
        jackpotPanelGroup.alpha = 0f;
        jackpotPanelGroup.blocksRaycasts = false;

        // タップで SKIP
        Button jpBtn = go.AddComponent<Button>();
        jpBtn.targetGraphic = bg;
        jpBtn.onClick.AddListener(() => { if (isRunning) skipRequested = true; });

        // 5つのランプ（左から 3000, 1000, 30000, 1000, 5000）
        float startX = 85f;
        float spacing = 137.5f;
        float y = -409.5f;
        for (int i = 0; i < 5; i++)
        {
            GameObject lamp = new GameObject("JPLamp" + i);
            lamp.transform.SetParent(go.transform, false);
            RectTransform lrt = lamp.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(0, 1);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            float x = startX + i * spacing;
            lrt.anchoredPosition = new Vector2(x, y);
            lrt.sizeDelta = new Vector2(90, 90);
            jackpotLampRects[i] = lrt;

            CanvasRenderer cr = lamp.AddComponent<CanvasRenderer>();
            RawImage img = lamp.AddComponent<RawImage>();
            img.texture = CreateCircleTexture(128, new Color32(60, 60, 80, 230));
            img.raycastTarget = false;

            GameObject txtGO = new GameObject("JPLampText" + i);
            txtGO.transform.SetParent(lamp.transform, false);
            Text txt = txtGO.AddComponent<Text>();
            txt.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
            if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 22;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color32(200, 200, 200, 255);
            txt.text = jpAwardLabels[i] == "30000" ? "JACKPOT" : jpAwardLabels[i];
            jackpotLampTexts[i] = txt;

            RectTransform trt = txtGO.GetComponent<RectTransform>();
            if (trt == null) trt = txtGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = Vector2.zero;
        }

        // インジケータ
        GameObject ind = new GameObject("JPIndicator");
        ind.transform.SetParent(go.transform, false);
        jackpotIndicatorRect = ind.AddComponent<RectTransform>();
        jackpotIndicatorRect.anchorMin = new Vector2(0, 1);
        jackpotIndicatorRect.anchorMax = new Vector2(0, 1);
        jackpotIndicatorRect.pivot = new Vector2(0.5f, 0.5f);
        jackpotIndicatorRect.anchoredPosition = new Vector2(startX, y);
        jackpotIndicatorRect.sizeDelta = new Vector2(60, 60);
        ind.AddComponent<CanvasRenderer>();
        RawImage indImg = ind.AddComponent<RawImage>();
        indImg.texture = CreateCircleTexture(128, new Color32(255, 220, 80, 255));
        indImg.raycastTarget = false;

        // 獲得枚数テキスト
        GameObject awardGO = new GameObject("JPAwardText");
        awardGO.transform.SetParent(go.transform, false);
        RectTransform art = awardGO.AddComponent<RectTransform>();
        art.anchorMin = new Vector2(0, 1);
        art.anchorMax = new Vector2(0, 1);
        art.pivot = new Vector2(0.5f, 0.5f);
        art.anchoredPosition = new Vector2(360, -229.5f);
        art.sizeDelta = new Vector2(600, 60);
        jackpotAwardText = awardGO.AddComponent<Text>();
        jackpotAwardText.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (jackpotAwardText.font == null) jackpotAwardText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        jackpotAwardText.fontSize = 38;
        jackpotAwardText.alignment = TextAnchor.MiddleCenter;
        jackpotAwardText.color = new Color32(255, 220, 80, 255);
        jackpotAwardText.text = "";

        go.SetActive(false);
    }

    void CreateHelpButton()
    {
        Image img;
        AddButton("Help", new Vector2(660, 405 + 655), new Vector2(32, 32), () => ToggleHelp(), out img);
    }

    void CreateBetButtons()
    {
        // 帯を y=697..818 に焼き直したため、BETピルの実測外形は texture y=705..800（高さ96）
        float[] xs = new[] { 16f, 119f, 222f, 325f, 428f };
        for (int i = 0; i < 5; i++)
        {
            int bet = int.Parse(betLabels[i]);
            Image img;
            Button btn = AddButton("Bet" + betLabels[i], new Vector2(xs[i], 405 + 705), new Vector2(95, 96), () => ToggleBet(bet), out img);
            betButtons[i] = btn;
            // 光りは板絵のピル枠に合わせた角丸で出す（矩形ベタ塗りだと枠からはみ出て見える）
            betButtonImages[i] = AddGlowOverlay(btn.transform, new Vector2(76, 78), 12);
        }
    }

    void CreateSpinButton()
    {
        Image img;
        // SPINは板絵側で1.25倍に貼り直したため、外形は texture x=528..697 / y=719..786
        spinButton = AddButton("Spin", new Vector2(528, 405 + 719), new Vector2(170, 68), () => OnSpin(), out img);
        spinButtonImage = img;

        GameObject txtGO = new GameObject("SpinText");
        txtGO.transform.SetParent(spinButton.transform, false);
        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = Vector2.zero;

        spinButtonText = txtGO.AddComponent<Text>();
        spinButtonText.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (spinButtonText.font == null) spinButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        spinButtonText.fontSize = 32;
        spinButtonText.alignment = TextAnchor.MiddleCenter;
        spinButtonText.color = Color.white;
        spinButtonText.text = "SPIN";
    }

    /// <summary>角丸ピル型のソフトなスプライトを生成する（ボタンの光り用）。</summary>
    static Sprite MakeRoundedSprite(int w, int h, int radius, float feather)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 角丸矩形の符号付き距離（内側が負）
                float dx = Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - radius);
                float dy = Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - radius);
                float d = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f))
                          + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                float aa = Mathf.Clamp01(0.5f - d / feather);
                px[y * w + x] = new Color(1f, 1f, 1f, aa);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    /// <summary>ボタンの当たり判定はそのままに、枠に合わせた光りだけを子に載せる。</summary>
    Image AddGlowOverlay(Transform parent, Vector2 size, int radius)
    {
        GameObject go = new GameObject("Glow");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image im = go.AddComponent<Image>();
        im.sprite = MakeRoundedSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), radius, 2.5f);
        im.type = Image.Type.Simple;
        im.raycastTarget = false;
        im.color = new Color(0, 0, 0, 0);
        return im;
    }

    Button AddButton(string name, Vector2 pos, Vector2 size, Action onClick, out Image image)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y);
        rt.sizeDelta = size;

        image = go.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0);

        Button btn = go.AddComponent<Button>();
        // conformance: button-feedback（全buttonに約0.1秒・1.05倍→戻る反応）。
        // pivot が (0,1) のため拡大は左上基準で伸びる。anchoredPosition を同時に
        // 補正して見かけ上は中心から拡大させ、静止時の座標は一切変えない。
        btn.onClick.AddListener(() => { StartCoroutine(PressFeedback(rt)); onClick(); });
        return btn;
    }

    IEnumerator PressFeedback(RectTransform rt)
    {
        WarukyureSfx.PlayTap();
        const float DUR = 0.1f;
        const float PEAK = 1.05f;
        Vector2 basePos = rt.anchoredPosition;
        Vector2 size = rt.sizeDelta;
        float t = 0f;
        while (t < DUR)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / DUR);
            float k = 1f - Mathf.Abs(p * 2f - 1f); // 0→1→0
            float sc = Mathf.Lerp(1f, PEAK, k);
            rt.localScale = new Vector3(sc, sc, 1f);
            rt.anchoredPosition = basePos + new Vector2(-size.x * (sc - 1f) * 0.5f, size.y * (sc - 1f) * 0.5f);
            yield return null;
        }
        rt.localScale = Vector3.one;
        rt.anchoredPosition = basePos;
    }

    // ----------------- interaction -----------------
    void ToggleBet(int bet)
    {
        if (!sessionReady || isRunning) return;
        if (selectedBets.Contains(bet)) selectedBets.Remove(bet);
        else selectedBets.Add(bet);
        UpdateBetButtonState();
    }

    void UpdateBetButtonState()
    {
        for (int i = 0; i < 5; i++)
        {
            int bet = int.Parse(betLabels[i]);
            bool on = selectedBets.Contains(bet);
            betButtonImages[i].color = on ? new Color32(255, 205, 60, 190) : new Color(0, 0, 0, 0);
        }
    }

    void OnSpin()
    {
        if (!sessionReady)
        {
            ShowResultOverlay("読み込み中です。少し待ってからもう一度どうぞ。", 1.5f);
            return;
        }
        if (isRunning)
        {
            skipRequested = true;
            return;
        }
        DismissResultOverlay();
        if (selectedBets.Count == 0)
        {
            ShowResultOverlay("BETを1つ以上選んでください", 1.5f);
            return;
        }
        StartCoroutine(SpinRound());
    }

    void ToggleHelp()
    {
        if (isRunning) return;
        ShowResultOverlay("2/4/6/8/20 を選んで SPIN\n数字に止まれば number × 倍率 × 100 枚", -1f);
    }

    void UpdateHeader()
    {
        string netStr;
        if (lastNet > 0) netStr = $"+{lastNet}";
        else if (lastNet < 0) netStr = $"-{Mathf.Abs(lastNet)}";
        else netStr = "±0";
        walletText.text = $"純益 {netStr} / 残高 {wallet:N0}";
        UpdateCollectionPanel();
    }

    // ----------------- overlay -----------------
    void ShowResultOverlay(string text, float displayDuration, bool blocking = true)
    {
        if (resultPanel == null) return;
        resultPanelText.text = text;
        resultPanel.SetActive(true);
        resultPanelGroup.blocksRaycasts = blocking;
        if (overlayRoutine != null) StopCoroutine(overlayRoutine);
        // 非ブロッキング時は fade も詰め、表示開始〜消滅を displayDuration 内に収める。
        overlayRoutine = StartCoroutine(OverlayRoutine(displayDuration, blocking ? 0.3f : 0.1f));
    }

    void DismissResultOverlay()
    {
        if (resultPanel == null) return;
        if (overlayRoutine != null)
        {
            StopCoroutine(overlayRoutine);
            overlayRoutine = null;
        }
        resultPanelGroup.alpha = 0;
        resultPanelGroup.blocksRaycasts = false;
        resultPanel.SetActive(false);
    }

    IEnumerator OverlayRoutine(float displayDuration, float fade = 0.3f)
    {
        yield return FadeOverlay(1f, fade);
        if (displayDuration > 0)
        {
            yield return new WaitForSeconds(displayDuration);
            yield return FadeOverlay(0f, fade);
            resultPanelGroup.blocksRaycasts = false;
            resultPanel.SetActive(false);
        }
        overlayRoutine = null;
    }

    IEnumerator FadeOverlay(float target, float fade = 0.3f)
    {
        float start = resultPanelGroup.alpha;
        float t = 0f;
        float FADE = fade;
        while (t < FADE)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / FADE);
            p = Mathf.SmoothStep(0f, 1f, p);
            resultPanelGroup.alpha = Mathf.Lerp(start, target, p);
            yield return null;
        }
        resultPanelGroup.alpha = target;
    }

    // ----------------- API -----------------
    IEnumerator InitSession()
    {
        // poicasi-auth から戻ってきた直後なら、ゲスト token より認証を優先する。
        string paCode = TakePaCode();
        if (!string.IsNullOrEmpty(paCode))
        {
            string anonToken = PlayerPrefs.GetString(TOKEN_KEY, "");
            string authJson = "{\"action\":\"auth\",\"pa_code\":\"" + paCode + "\"";
            if (!string.IsNullOrEmpty(anonToken))
                authJson += ",\"anon_token\":\"" + anonToken + "\"";
            authJson += "}";
            string authBody = null;
            string authErr = null;
            yield return StartCoroutine(ApiPost(authJson, null, (b) => authBody = b, (e) => authErr = e));
            if (string.IsNullOrEmpty(authErr) && !string.IsNullOrEmpty(authBody))
            {
                var authRes = JsonUtility.FromJson<InitResponse>(authBody);
                if (authRes != null && !string.IsNullOrEmpty(authRes.token) && authRes.state != null)
                {
                    token = authRes.token;
                    PlayerPrefs.SetString(TOKEN_KEY, token);
                    wallet = authRes.state.wallet;
                    ballMask = authRes.state.ballMask;
                    lastNet = 0;
                    sessionReady = true;
                    UpdateHeader();
                    yield break;
                }
            }
            // 認証に失敗しても遊べなくならないよう、以降のゲスト経路へフォールバックする。
            Debug.LogWarning("poicasi-auth 認証に失敗したためゲストで継続: " + (authErr ?? "invalid response"));
        }

        token = PlayerPrefs.GetString(TOKEN_KEY, "");
        if (!string.IsNullOrEmpty(token))
        {
            string json = "{\"action\":\"state\",\"token\":\"" + token + "\"}";
            string error = null;
            string body = null;
            yield return StartCoroutine(ApiPost(json, null, (b) => body = b, (e) => error = e));
            if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(body))
            {
                var res = JsonUtility.FromJson<StateResponse>(body);
                if (res != null && res.state != null)
                {
                    wallet = res.state.wallet;
                    ballMask = res.state.ballMask;
                    lastNet = 0;
                    sessionReady = true;
                    UpdateHeader();
                    yield break;
                }
            }
        }

        string initJson = "{\"action\":\"init\"}";
        string initBody = null;
        string initErr = null;
        yield return StartCoroutine(ApiPost(initJson, null, (b) => initBody = b, (e) => initErr = e));
        if (!string.IsNullOrEmpty(initErr))
        {
            ShowResultOverlay("通信エラー: " + initErr, -1f);
            yield break;
        }
        var initRes = JsonUtility.FromJson<InitResponse>(initBody);
        // #16: token/state が揃っていない応答で先へ進むと NullReference か無効トークンのまま遊べてしまう。
        if (initRes == null || string.IsNullOrEmpty(initRes.token) || initRes.state == null)
        {
            ShowResultOverlay("初期化に失敗しました。\n通信環境を確認して再読み込みしてください。", -1f);
            yield break;
        }
        token = initRes.token;
        PlayerPrefs.SetString(TOKEN_KEY, token);
        wallet = initRes.state.wallet;
        ballMask = initRes.state.ballMask;
        lastNet = 0;
        sessionReady = true;
        UpdateHeader();
    }

    IEnumerator SpinRound()
    {
        isRunning = true;
        skipRequested = false;
        spinButtonText.text = "SKIP";
        DismissResultOverlay();
        UpdateBetButtonState();

        currentRunId = System.Guid.NewGuid().ToString();

        // prepare
        string demoPart = IsDemoMode() ? ",\"demo\":true" : "";
        string prepareJson = "{\"action\":\"prepare\",\"token\":\"" + token + "\",\"runId\":\"" + currentRunId + "\"" + demoPart + "}";
        string prepareBody = null;
        string prepareErr = null;
        yield return StartCoroutine(ApiPost(prepareJson, currentRunId, (b) => prepareBody = b, (e) => prepareErr = e));
        if (!string.IsNullOrEmpty(prepareErr))
        {
            // 409＝前回の中断ランが残留している。そのrunIdを引き継いでresolveし、復帰させる。
            // 放置すると以後prepareが永久に409になり遊べなくなる（2026-08-19 社長報告）。
            string stuckId = (lastErrorCode == 409) ? ExtractStuckRunId(lastErrorBody) : null;
            if (string.IsNullOrEmpty(stuckId))
            {
                EndRound(API_RETRY_MSG);
                yield break;
            }
            currentRunId = stuckId;
        }

        // resolve
        int[] bets = new int[selectedBets.Count];
        selectedBets.CopyTo(bets);
        Array.Sort(bets);
        string betStr = string.Join(",", bets);
        string resolveJson = "{\"action\":\"resolve\",\"token\":\"" + token + "\",\"runId\":\"" + currentRunId + "\",\"bets\":[" + betStr + "]}";
        string resolveBody = null;
        string resolveErr = null;
        yield return StartCoroutine(ApiPost(resolveJson, currentRunId, (b) => resolveBody = b, (e) => resolveErr = e));
        if (!string.IsNullOrEmpty(resolveErr))
        {
            EndRound(API_RETRY_MSG);
            yield break;
        }

        lastResult = JsonUtility.FromJson<ResolveResponse>(resolveBody);
        if (lastResult == null)
        {
            EndRound(API_RETRY_MSG);
            yield break;
        }

        // JsonUtility は未指定の bool を false にする。ok が省略されている旧契約では true と見なす。
        lastResult.ok = true;
        int okIdx = resolveBody.IndexOf("\"ok\"", StringComparison.Ordinal);
        if (okIdx >= 0)
        {
            int i = okIdx + 4;
            while (i < resolveBody.Length && char.IsWhiteSpace(resolveBody[i])) i++;
            if (i < resolveBody.Length && resolveBody[i] == ':')
            {
                i++;
                while (i < resolveBody.Length && char.IsWhiteSpace(resolveBody[i])) i++;
                if (i + 5 <= resolveBody.Length &&
                    resolveBody[i] == 'f' && resolveBody[i + 1] == 'a' && resolveBody[i + 2] == 'l' &&
                    resolveBody[i + 3] == 's' && resolveBody[i + 4] == 'e')
                    lastResult.ok = false;
            }
        }

        if (!ValidateResolveResponse(lastResult, currentRunId, bets))
        {
            EndRound(API_RETRY_MSG);
            yield break;
        }

        wallet = lastResult.state.wallet;
        ballMask = lastResult.state.ballMask;
        UpdateCollectionPanel();

        // lamp animation
        var path = BuildLampPath(lastResult.pathId, lastResult.stopId);
        if (path != null && path.Count > 0)
            lampRect.anchoredPosition = path[0];
        yield return StartCoroutine(RunLamp(path));

        if (!skipRequested)
        {
            float hold = 0f;
            while (hold < HOLD_DURATION)
            {
                hold += Time.deltaTime;
                if (skipRequested) break;
                yield return null;
            }
        }

        ShowResult(lastResult);
    }

    void EndRound(string error)
    {
        isRunning = false;
        skipRequested = false;
        spinButtonText.text = "SPIN";
        if (!string.IsNullOrEmpty(error)) ShowResultOverlay(error, -1f);

        // クロスプロモ: ラウンド終了（＝リザルト表示）時のみ発火。プレイ中には割り込まない。
        // 通信エラー時は出さない。同一セッション1回までの制御は PoiPlayTime 側が持つ。
        if (string.IsNullOrEmpty(error)) CrossPromoPopupUI.ShowIfEligible(canvas, Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium"));
    }

    // resolve 応答の必須項目を検証。1つでも満たさなければSPIN復帰＋エラー表示。
    bool ValidateResolveResponse(ResolveResponse r, string expectedRunId, int[] sentBets)
    {
        if (r == null) return false;
        if (!r.ok) return false;
        if (string.IsNullOrEmpty(r.runId) || r.runId != expectedRunId) return false;
        if (string.IsNullOrEmpty(r.stopId) || BoardData.GetIndex(r.stopId) < 0) return false;
        if (r.bets == null || r.bets.Length != sentBets.Length) return false;
        for (int i = 0; i < sentBets.Length; i++)
            if (r.bets[i] != sentBets[i]) return false;
        if (r.awardBreakdown == null || r.awardBreakdown.total < 0) return false;
        if (r.state == null) return false;
        return true;
    }

    // 409ボディ {"error":"...","run":{"runId":"xxxx",...}} から runId を取り出す
    string ExtractStuckRunId(string body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        const string key = "\"runId\":\"";
        int i = body.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        i += key.Length;
        int j = body.IndexOf('"', i);
        if (j <= i) return null;
        return body.Substring(i, j - i);
    }

    IEnumerator ApiPost(string json, string idemKey, Action<string> onOk, Action<string> onErr)
    {
        UnityWebRequest req = new UnityWebRequest(API_URL, "POST");
        byte[] body = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(idemKey))
            req.SetRequestHeader("Idempotency-Key", idemKey);
        req.timeout = 15;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success || req.responseCode != 200)
        {
            lastErrorCode = req.responseCode;
            lastErrorBody = req.downloadHandler != null ? req.downloadHandler.text : null;
            string msg = $"HTTP {req.responseCode}";
            if (!string.IsNullOrEmpty(req.error)) msg += " " + req.error;
            onErr(msg);
        }
        else
        {
            lastErrorCode = 0;
            lastErrorBody = null;
            onOk(req.downloadHandler.text);
        }
    }

    // ----------------- lamp path -----------------
    List<Vector2> BuildLampPath(string pathId, string stopId)
    {
        string home;
        string sourceCell;
        string sourceTrack;
        string targetCell = null;
        string targetTrack = null;
        string stopCell = stopId;

        if (pathId == "outer")
        {
            home = "o_01";
            sourceTrack = "outer";
            sourceCell = stopId;
        }
        else
        {
            var warp = BoardData.Warp[pathId];
            sourceCell = warp.source;
            targetCell = warp.target;
            targetTrack = warp.targetTrack;
            sourceTrack = BoardData.GetTrack(sourceCell);
            home = sourceTrack == "outer" ? "o_01" : "i_01";
        }

        int sourceIndex = BoardData.GetIndex(sourceCell);
        int homeIndex = BoardData.GetIndex(home);
        string[] srcArr = BoardData.GetTrackArray(sourceTrack);
        int srcL = srcArr.Length;

        string[] tgtArr = null;
        int tgtL = 0;

        if (pathId != "outer")
        {
            if (targetTrack != "castle")
            {
                tgtArr = BoardData.GetTrackArray(targetTrack);
                tgtL = tgtArr.Length;
            }
        }

        int srcFast = LAMP_LAPS_FAST * srcL;
        int srcMid  = Mathf.RoundToInt(LAMP_LAPS_MID * srcL);
        int srcLap  = srcFast + srcMid;
        int runIn = ((sourceIndex - homeIndex - srcLap) % srcL + srcL) % srcL;

        List<string> cells = new List<string>();
        List<float> cellSpeeds = new List<float>();
        cells.Add(home);        cellSpeeds.Add(0f);
        for (int i = 1; i <= srcLap + runIn; i++)
        {
            cells.Add(srcArr[(homeIndex + i) % srcL]);
            cellSpeeds.Add(i <= srcFast ? LAMP_SPEED_FAST : (i <= srcLap ? LAMP_SPEED_MID : LAMP_SPEED_SLOW));
        }

        // warp + target track
        if (pathId != "outer")
        {
            cells.Add(targetCell);  cellSpeeds.Add(LAMP_SPEED_SLOW);   // ワープの1手は低速のまま（移動先を認識させる）
            if (targetTrack != "castle")
            {
                int targetIndex = BoardData.GetIndex(targetCell);
                int stopIndex   = BoardData.GetIndex(stopId);
                int tFast = LAMP_LAPS_FAST * tgtL;
                int tMid  = Mathf.RoundToInt(LAMP_LAPS_MID * tgtL);
                int tLap  = tFast + tMid;
                int tRunIn = ((stopIndex - targetIndex - tLap) % tgtL + tgtL) % tgtL;
                for (int i = 1; i <= tLap + tRunIn; i++)
                {
                    cells.Add(tgtArr[(targetIndex + i) % tgtL]);
                    cellSpeeds.Add(i <= tFast ? LAMP_SPEED_FAST : (i <= tLap ? LAMP_SPEED_MID : LAMP_SPEED_SLOW));
                }
            }
        }

        List<Vector2> path = new List<Vector2>();
        lampSegSpeeds.Clear();
        lampSizes.Clear();
        lampTracks.Clear();
        lampCells.Clear();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2 c;
            if (BoardData.TryGetCenter(cells[i], out c))
            {
                path.Add(new Vector2(c.x, -c.y));
                lampSizes.Add(CellSizeForTrack(BoardData.GetTrack(cells[i])));
                lampTracks.Add(BoardData.GetTrack(cells[i]));
                lampCells.Add(cells[i]);
                if (path.Count > 1) lampSegSpeeds.Add(cellSpeeds[i]);
            }
        }
        return path;
    }

    float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    IEnumerator RunLamp(List<Vector2> path)
    {
        if (path == null || path.Count < 2)
        {
            if (path != null && path.Count > 0)
            {
                lampRect.anchoredPosition = path[path.Count - 1];
                ApplyLampCell(lampSizes.Count - 1);
                WarukyureSfx.PlayLampStop();
            }
            yield break;
        }

        int segments = path.Count - 1;

        float virt = 0f; // 進んだマス数（連続値）
        while (virt < segments)
        {
            if (skipRequested)
            {
                lampRect.anchoredPosition = path[path.Count - 1];
                ApplyLampCell(lampSizes.Count - 1);
                WarukyureSfx.PlayLampStop();
                yield break;
            }
            float speed = (lampSegSpeeds.Count == 0) ? LAMP_SPEED_SLOW : lampSegSpeeds[Mathf.Clamp(Mathf.FloorToInt(virt), 0, lampSegSpeeds.Count - 1)];
            virt = Mathf.Min(segments, virt + speed * Time.deltaTime);
            int idx = Mathf.FloorToInt(virt);
            if (idx > segments) idx = segments;
            lampRect.anchoredPosition = path[idx];
            ApplyLampCell(idx);
            yield return null;
        }
        lampRect.anchoredPosition = path[path.Count - 1];
        ApplyLampCell(lampSizes.Count - 1);
        WarukyureSfx.PlayLampStop();
    }

    // ----------------- result -----------------
    void ShowResult(ResolveResponse r)
    {
        lastNet = r.awardBreakdown.net;
        wallet = r.state.wallet;
        ballMask = r.state.ballMask;
        UpdateHeader();

        // ボールコンプリート → JACKPOT チャレンジ（5ランプ）
        if (r.bonusOutcome != null && r.awardBreakdown.jackpot > 0)
        {
            StartCoroutine(RunJackpotChallenge(r));
            return;
        }

        // 通常の BIG/MEGA 配当：fx があれば poifx、なければテキスト
        if (r.fx != null && !string.IsNullOrEmpty(r.fx.tier) && r.fx.amount > 0)
        {
            StartCoroutine(RunPoiFxThenResult(r));
            return;
        }

        ShowNormalResult(r);
        EndRound("");
    }

    void ShowNormalResult(ResolveResponse r)
    {
        StringBuilder sb = new StringBuilder();
        if (r.primaryType == "out")
        {
            sb.Append("はずれ");
        }
        else if (r.primaryType == "number")
        {
            sb.Append($"数字 {r.number} × {r.multiplier} = {r.awardBreakdown.number}枚");
        }
        else if (r.primaryType == "castle")
        {
            sb.Append($"城 90 = {r.awardBreakdown.castle}枚");
        }
        else if (r.primaryType == "ball")
        {
            string name = "???";
            if (r.collection != null && r.collection.ballType >= 0 && r.collection.ballType < 4)
                name = ballNames[r.collection.ballType];
            sb.Append($"BALL {name} ゲット");
        }

        // conformance: loss-fast（外れ→次入力可能まで0.4秒以内）。
        // 当たりは従来どおり 1.5 秒ブロック、外れだけ非ブロッキングの短表示にする。
        bool isLoss = r.primaryType == "out";
        ShowResultOverlay(sb.ToString(), isLoss ? 0.4f : 1.5f, !isLoss);
    }

    // ----------------- JACKPOT challenge flow -----------------
    float GetJackpotX(int index)
    {
        float startX = 85f;
        float spacing = 137.5f;
        return startX + index * spacing;
    }

    IEnumerator RunJackpotChallenge(ResolveResponse r)
    {
        // 通常UIを隠して Header A + Ad-Virtua を最前面に
        SetNormalUIForChallenge(false);
        walletText.rectTransform.SetAsLastSibling();
        adVirtuaRect.SetAsLastSibling();

        jackpotPanel.SetActive(true);
        jackpotPanelGroup.blocksRaycasts = true;

        // フェードイン
        yield return FadeJackpotPanel(1f);

        int stopIndex = r.bonusOutcome.stopIndex;

        // 既存の自前ランプ群は LampAnnouncer の背後に残るため非表示にする
        for (int i = 0; i < 5; i++)
        {
            if (jackpotLampRects[i] != null) jackpotLampRects[i].gameObject.SetActive(false);
        }

        // Babeltower8192 と同じ 5 ランプ演出。停止位置はサーバー指定値を再生する。
        string[] labels = { "3000", "1000", "JACKPOT", "1000", "5000" };
        Color[] labelColors =
        {
            new Color(1f, 0.94f, 0.80f),
            new Color(1f, 0.94f, 0.80f),
            new Color(1f, 0.85f, 0.40f),
            new Color(1f, 0.94f, 0.80f),
            new Color(1f, 0.94f, 0.80f)
        };

        yield return LampAnnouncer.Run(labels, stopIndex, labelColors, () => skipRequested);

        // 獲得枚数表示
        jackpotAwardText.text = $"{r.bonusOutcome.award}枚";

        // 0.3秒の表示溜め
        float hold = 0f;
        while (hold < 0.3f)
        {
            if (skipRequested) break;
            hold += Time.deltaTime;
            yield return null;
        }

        // poifx（サーバー fx があれば）
        if (r.fx != null && !string.IsNullOrEmpty(r.fx.tier) && r.fx.amount > 0)
        {
            yield return StartCoroutine(RunPoiFx(r.fx.tier, r.fx.amount));
        }

        // 通常画面へ復帰
        yield return FadeJackpotPanel(0f);
        jackpotPanel.SetActive(false);
        jackpotPanelGroup.blocksRaycasts = false;
        SetNormalUIForChallenge(true);
        ShowResultOverlay($"JACKPOT {r.bonusOutcome.award}枚", 1.2f);
        EndRound("");
    }

    IEnumerator FadeJackpotPanel(float target)
    {
        float start = jackpotPanelGroup.alpha;
        float t = 0f;
        const float FADE = 0.25f;
        while (t < FADE)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / FADE);
            p = EaseOutCubic(p);
            jackpotPanelGroup.alpha = Mathf.Lerp(start, target, p);
            yield return null;
        }
        jackpotPanelGroup.alpha = target;
    }

    void SetNormalUIForChallenge(bool visible)
    {
        GameObject[] gos = { lampRect.gameObject, spinButton.gameObject, resultPanel };
        foreach (var g in gos)
        {
            if (g != null) g.SetActive(visible);
        }
        for (int i = 0; i < betButtons.Length; i++)
        {
            if (betButtons[i] != null) betButtons[i].gameObject.SetActive(visible);
        }
    }

    IEnumerator RunPoiFxThenResult(ResolveResponse r)
    {
        yield return StartCoroutine(RunPoiFx(r.fx.tier, r.fx.amount));
        ShowNormalResult(r);
        EndRound("");
    }

    IEnumerator RunPoiFx(string tier, int amount)
    {
        poiFxPending = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        // 現在のフレームのレンダリング／rAF 完了後にブラウザ側演出を発火し、
        // ブラウザにスタイル更新の機会を与える。
        yield return new WaitForEndOfFrame();
        PoiFxJackpot(tier, amount, "枚", gameObject.name, "OnPoiFxDone");
#else
        Debug.Log($"[poifx] {tier} {amount}枚");
        OnPoiFxDone("");
#endif
        float timeout = 0f;
        while (poiFxPending && timeout < 5f)
        {
            if (skipRequested)
            {
                CallPoiFxSkip();
                poiFxPending = false;
            }
            timeout += Time.deltaTime;
            yield return null;
        }
        poiFxPending = false;
    }

    void CallPoiFxSkip()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PoiFxSkip();
#else
        Debug.Log("[poifx] skip");
#endif
    }

    // WebGL jslib からの onDone コールバック
    public void OnPoiFxDone(string _)
    {
        poiFxPending = false;
    }

    // ----------------- DEV-only forced effect debug -----------------
    bool IsDevUrl(string url)
    {
        return url.Contains("warukyure-dev") ||
               url.Contains("localhost") ||
               url.Contains("127.0.0.1");
    }

    string GetQueryParam(string url, string key)
    {
        int q = url.IndexOf('?');
        if (q < 0) return null;
        string query = url.Substring(q + 1);
        string[] pairs = query.Split('&');
        foreach (var p in pairs)
        {
            int eq = p.IndexOf('=');
            if (eq < 0) continue;
            string k = Uri.UnescapeDataString(p.Substring(0, eq));
            if (k == key) return Uri.UnescapeDataString(p.Substring(eq + 1));
        }
        return null;
    }

    // SIMULATOR-BADGE v1 と同一アルゴリズム: '?'以降を'&'分割し、最初の'demo'キーのraw value が'1'のみ。
    // URLデコードは行わない（client/index.html:1587-1594 踏襲）。
    string GetRawQueryParam(string url, string key)
    {
        if (string.IsNullOrEmpty(url)) return null;
        int hashIdx = url.IndexOf('#');
        if (hashIdx >= 0) url = url.Substring(0, hashIdx);
        int qIdx = url.IndexOf('?');
        if (qIdx < 0) return null;
        string query = url.Substring(qIdx + 1);
        string[] pairs = query.Split('&');
        foreach (var p in pairs)
        {
            int eq = p.IndexOf('=');
            if (eq < 0) continue;
            if (p.Substring(0, eq) == key) return p.Substring(eq + 1);
        }
        return null;
    }

    bool IsDemoMode()
    {
        return GetRawQueryParam(Application.absoluteURL, "demo") == "1";
    }

    IEnumerator TryDebugForceFx()
    {
        // WebGL では1フレーム遅らせないと Application.absoluteURL が未設定のままになることがある。
        yield return null;
        string url = Application.absoluteURL;
        if (!IsDevUrl(url)) yield break;

        string fx = GetQueryParam(url, "fx");
        if (string.IsNullOrEmpty(fx)) yield break;

        if (fx == "big" || fx == "mega")
        {
            int amount = fx == "mega" ? 10000 : 3200;
            // オーバーレイだけを発火。JsonUtility も同時に検証する。
            string testJson = "{\"fx\":{\"tier\":\"" + fx + "\",\"amount\":" + amount + "}}";
            var r = JsonUtility.FromJson<ResolveResponse>(testJson);
            Debug.Log("[fx-debug] parsed fx=" + (r?.fx?.tier ?? "null") + " amount=" + (r?.fx?.amount));
            if (r?.fx != null && !string.IsNullOrEmpty(r.fx.tier) && r.fx.amount > 0)
            {
                StartCoroutine(RunPoiFx(r.fx.tier, r.fx.amount));
            }
        }
        else if (fx == "jp")
        {
            // 演出だけ発火（抽選・残高更新なし）
            var fake = new ResolveResponse
            {
                bonusOutcome = new BonusOutcome { stopIndex = 0, award = 3000 },
                awardBreakdown = new AwardBreakdown { wager = 500, number = 0, castle = 0, jackpot = 3000, total = 3000, net = 2500 },
                fx = null
            };
            StartCoroutine(RunJackpotChallenge(fake));
        }
    }

    // ----------------- JSON data classes -----------------
    [Preserve]
    [Serializable]
    public class StateData
    {
        public int wallet;
        public int ballMask;
    }

    [Preserve]
    [Serializable]
    public class InitResponse
    {
        public string uid;
        public string token;
        public StateData state;
    }

    [Preserve]
    [Serializable]
    public class StateResponse
    {
        public StateData state;
    }

    [Preserve]
    [Serializable]
    public class AwardBreakdown
    {
        public int wager;
        public int number;
        public int castle;
        public int jackpot;
        public int total;
        public int net;
    }

    [Preserve]
    [Serializable]
    public class Collection
    {
        public int ballType;
        public int maskBefore;
        public int maskAfter;
        public bool isNew;
    }

    [Preserve]
    [Serializable]
    public class BonusOutcome
    {
        public int stopIndex;
        public int award;
    }

    [Preserve]
    [Serializable]
    public class FxData
    {
        public string tier;
        public int amount;
    }

    [Preserve]
    [Serializable]
    public class ResolveResponse
    {
        public bool ok;
        public string runId;
        public string stopId;
        public string pathId;
        public string primaryType;
        public int number;
        public int multiplier;
        public string track;
        public int[] bets;
        public AwardBreakdown awardBreakdown;
        public Collection collection;
        public BonusOutcome bonusOutcome;
        public int effectTier;
        public FxData fx;
        public StateData state;
    }
}
