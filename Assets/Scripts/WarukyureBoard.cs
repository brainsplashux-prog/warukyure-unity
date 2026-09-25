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
    // 環境切替: 1ソースで DEV/本番 の両方を配信する（2026-08-22 本番リリース）。
    //   配信先 URL に "warukyure-dev" を含む＝DEV、それ以外＝本番。
    //   Editor など absoluteURL が空の場合は DEV（本番へ誤射しない側に倒す）。
    const string API_URL_DEV  = "https://b5yl9sml5l.execute-api.ap-northeast-1.amazonaws.com/";
    const string API_URL_PROD = "https://f8fod9qgw3.execute-api.ap-northeast-1.amazonaws.com/";
    private static string apiUrlCache;
    static string API_URL
    {
        get
        {
            if (apiUrlCache != null) return apiUrlCache;
            string u = Application.absoluteURL;
            bool isProd = !string.IsNullOrEmpty(u) && u.IndexOf("warukyure-dev", StringComparison.Ordinal) < 0;
            apiUrlCache = isProd ? API_URL_PROD : API_URL_DEV;
            return apiUrlCache;
        }
    }
    const string TOKEN_KEY = "warukyure_token";
    // JACKPOT 演出の poifx v4 用ファンファーレ SE。
    // [社長確定 2026-09-06]「OKこれをJACKPOT獲得時の曲に変更してくれ（城獲得は変更なし）」
    // → fan9_levelup_rush（曲3秒＋余韻3秒／6.2秒）に差し替え。城到達側(se/se_fanfare=fan6_triumph)は不変。
    // 既存キーを上書きすると immutable キャッシュで旧版が永久に焼き付くため、新ファイル名にして参照を切り替える。
    const string JACKPOT_SE_URL = "https://lp.poicasi.co.jp/shared/poifx/v4/se/warukyure-jackpot-fan9.mp3";
    private int missionBet = 100;
    private int playMissionBet = 100; // そのプレイのprepare(またはPF bet)確定直後に固定するmissionBetのスナップショット。resolve結果の表示に使う（missionBetが以後更新されても値がぶれないように）
    const float RUN_DURATION = 2.0f;
    const float HOLD_DURATION = 0.5f;
    const int MIN_PATH_STEPS = 35;
    const string API_RETRY_MSG = "通信エラー。もう一度お試しください";

    // 社長指示(2026-08-19)の3段階ルーレット
    const float LAMP_SPEED_FAST = 20f;  // マス/秒。従来＝約40マスを2.0秒＝約20マス/秒 と等速
    const float LAMP_SPEED_MID  = 10f;  // 「今のスピードの半分」
    const float LAMP_SPEED_SLOW = 5f;   // 「1秒間に5マス」
    const int   LAMP_LAPS_FAST  = 1;    // 社長指示(2026-09-16): 停止までを短縮するため高速周回を2周→1周に削減
    // 社長指示(2026-09-16): 低速区間を10〜15マスのランダム値にして停止位置を読ませない（固定位置での賭け見切りを防ぐ）
    const int   LAMP_SLOW_MIN   = 10;
    const int   LAMP_SLOW_MAX   = 15;

    // ----------------- UI references -----------------
    private Canvas canvas;
    // 2026-09-15 社長指示（全幅化）: 盤面(y=405..1224相当)一式をキャンバス下端に
    // 常時吸着させるための下端アンカー基準点。SetupCanvas直後に生成し、
    // 盤面画像・セル消灯・ランプ・ヘルプ/BET/SPIN・結果パネル・JPパネルを
    // ここへ吸着させる（吸着すれば実測高さがどうであれ下端が一致し続ける）。
    // 詳細: harness/reports/20260915-warukyure-noreload-fullwidth.md
    private RectTransform boardRoot;
    // 2026-09-15 社長指示「ルーレット台を100px近く下げ中央配置。BET/SPINは盤面前面へ
    // 移設してボタン専用の下段バーを廃止する」対応。
    // 旧実装(BoardRoot高819=盤面819そのまま)では最深要素=BETピル下端(local801)から
    // BoardRoot下端(819)までの18pxしか可動域が無く、Ad-Virtuaゾーン(405designUnit固定)
    // との重なりを部分的にしか緩和できなかった（詳細根拠: harness/reports/
    // 20260915-warukyure-stage-top.md）。
    // 今回、下段のBET/SPIN帯(texture y=697..818, 実測確認済み=帯の背景色がy=697で
    // 1段階変化する)をRawImage.uvRectで非表示化し、BoardRoot自体の高さを697へ短縮。
    // その上で「Ad-Virtuaゾーン下端(405)～画面下端(H)」の可視領域内でBoardRootを
    // 上下中央配置する。BET/SPINボタンはBoardRootの子のまま新座標へ再配置するため、
    // BoardRootが動けば自動的に追従する。
    const float BoardRootHeightOld = 819f;      // 旧・盤面アート全体の高さ（クロップ前）
    const float BoardCropBandTopY = 697f;       // 帯の開始y（実測: ここでbg色が変化）
    const float BoardRootHeight = BoardCropBandTopY; // クロップ後のBoardRoot高さ = 697
    const float AdZoneHeight = 405f;            // AdVirtuaMonitorSetup.cs 側の固定値（読み取り専用参照。無変更）
    float boardLiftScreenW, boardLiftScreenH;

    // Ad-Virtuaゾーン下端(AdZoneHeight)〜画面下端(H)の可視領域内でBoardRoot(高さ697)を
    // 上下中央配置したときの、BoardRoot下端の canvas-Y（Unity座標系・下原点・上向き正）。
    // H = Screen.height * 720 / Screen.width（CanvasScaler: 幅基準固定・matchWidthOrHeight=0）。
    static float ComputeBoardBottomY()
    {
        if (Screen.width <= 0) return 0f;
        float visibleDesignH = Screen.height * 720f / Screen.width;
        float availableH = visibleDesignH - AdZoneHeight;
        float centeredBottomY = (availableH - BoardRootHeight) / 2f;
        return Mathf.Max(0f, centeredBottomY);
    }
    private RectTransform lampRect;
    private GameObject resultPanel;
    private RectTransform resultPanelRect;
    private CanvasGroup resultPanelGroup;
    private Text resultPanelText;
    private readonly Button[] betButtons = new Button[5];
    private readonly Image[] betButtonImages = new Image[5];
    private readonly Text[] betTexts = new Text[5]; // BETボタンの「BET n\nXXX枚」表示。missionBet確定/更新時に文言を再計算するため保持
    private Button spinButton;
    private Text spinButtonText;
    private Image spinButtonImage;
    private Image spinSkipPlate;   // SKIP時に板絵のSPINを覆うオレンジ板
    private GameObject betSheet;   // 2026-09-15 社長指示: BET5個を収める台（枠）。SPINで枠ごと非表示にする

    // ----------------- state -----------------
    private string token;
    private int wallet;
    private int lastNet;
    private int ballMask;
    private string currentRunId;
    private bool isRunning;
    // #16: init(token/state) が確定するまで BET/SPIN を受け付けない。
    private bool sessionReady;
    // TitleScreen が START タップを受け付けてよいかの参照用（sessionReady の読み取り公開）
    public bool IsSessionReady => sessionReady;
    // 2026-09-25 是正: 初期化に失敗した時のコード（null＝失敗していない）。
    // 以前は失敗後も sessionReady=false のまま放置され、SPIN/START が
    // 「読み込み中」表示や無反応のまま永久に止まっていた。
    private string sessionFailCode;
    public bool IsSessionFailed => sessionFailCode != null;
    private bool platformPrepareFailed;
    private bool spinPreChecking; // SPIN事前判定の通信中（二重押下で2本走らせない）
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

    // TitleScreen 参照（リザルトクローズ後の再表示用）
    private TitleScreen titleScreen;
    // セッション確立の再試行管理
    private IEnumerator initSessionEnum;
    private Coroutine initSessionRoutine;

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

    // ----------------- poicasi platform bridge -----------------
    private PlatformApiClient platformClient;
    private PlatformRun platformRun;
    private bool platformEnabled;
    // キャンペーンready: PF resolveでSETTLED・run一致を確認したrunと、通知済みrun。
    private string platformSettledRunId;
    private string lastCampaignReadyRunId;
    // 2026-09-13 是正(第1回codex指摘P1): platformRun/platformEnabledはSpinRound内の
    // 409復旧分岐(既存run復帰)でもクリアされる可変フィールドのため、reload可否の判定に
    // 使うと「取得済みのplatform run(A)が未精算のまま、currentRunIdが別run(B)に
    // 差し替わった」ケースを「そもそもplatform runではなかった」と誤判定してしまう。
    // 取得に成功したが精算(SettlePlatformRunでのSETTLED確定、またはTryAbortAndShowPopup
    // 経由の返金/精算確定)がまだ済んでいないplatform runIdを、409復旧等の
    // currentRunId差し替えとは独立して保持する不変集合。空＝未精算runなし。
    // 2026-09-13 是正(第2回codex指摘P1): 単一のstring変数だと、Aが未精算のまま次の
    // prepareでplatform run Cを取得した時点で pendingUnsettledPlatformRunId が
    // Cのrunidに上書きされ、Aの未精算が追跡できなくなる(Cが精算確定した瞬間、Aが
    // 未精算のままreload許可されてしまう)。複数の未精算runを同時に保持できる
    // HashSet<string> に変更し、「1件でも未精算が残っていればreload不可」にする。
    private readonly HashSet<string> pendingUnsettledPlatformRunIds = new HashSet<string>();
    // 2026-09-13 是正(第4回codex指摘P1 その2): TryPreparePlatform()はPrepare()の通信が
    // 完了して初めてpendingUnsettledPlatformRunIdsへAddする。そのため「通信中(Add前)の
    // 進行中prepare」はこの集合のCount==0判定に反映されず、他のrunのreload実行箇所からは
    // 「未精算runなし」に見えてしまう。もしこの間にreloadが実行されると、通信完了後に
    // 取得されるはずのrun(例: A中断からの復旧待機中に開始したC)のrunId/playTokenが
    // 判明する前にページが失われ、精算/返金の手がかりを失う。Prepare()呼び出し開始から
    // 完了(成功でAddした後・失敗でbreakする前のいずれか)までを「未解決」として数える
    // カウンタ。0でない間はreload不可。
    private int platformPrepareInFlight;
    // 2026-09-13 是正(第5回codex指摘P1): Prepare()はLaunch→Token→Plays(plays)の
    // 3段階からなり、サーバー側のplays実装(/Users/suzukimasahiro/Desktop/poicasi-platform/
    // src/games/play.mjs)はrunの作成・メダル予約(ledger line)を確定させた後にHTTP応答を
    // 返す。そのため、この応答が通信断・タイムアウト等でクライアントに届かず
    // task.IsFaultedになっても、サーバー側では既にrunが成立している(=未精算のまま)
    // 可能性を否定できない。かつクライアントはrunidを送っていない(サーバー側が生成する)
    // ため、このrunを個別に追跡してpendingUnsettledPlatformRunIdsへ追加することも
    // できない。「Task失敗はrun未成立の証拠にならない」ため、一度でもこの状態が
    // 起きたら、run idが特定できないまま永続的に未解決とみなし、以後の全reload経路を
    // 禁止する(セッション中に自己解消する手段が無いため、安全側でfalse固定にする。
    // 2026-09-13 是正(第6回codex指摘): warukyureのタイトル画面(TitleScreen)は
    // Yabuzame/PoiStartButtonのようなFindObjectOfType依存の状態機械ではなく、
    // OnPoiErrBackCore/RunPoiResultがtitleScreenの参照を直接保持してReopen()を
    // 呼ぶ設計であるため、そもそもTask A(状態機械固着対策)の対象外であり、
    // OnEnable自己修復も実装していない。reloadを諦めても、Yabuzameで発生した
    // 「非アクティブオブジェクトの検索失敗によるReset()不発」型の固着はwarukyureの
    // 構造上発生しないため、操作不能にはならない、という判断根拠)。
    private bool platformPrepareOutcomeUnknown;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PoiFxJackpot(string tier, int amount, string unit, string gameObjectName, string onDoneMethod, string se);

    [DllImport("__Internal")]
    private static extern void PoiFxSkip();

    // ----------------- poiresult bridge（共通リザルト画面 v2） -----------------
    // 正本: ~/.claude/manuals/poiresult-standard.md
    // payout と reward から分岐（>=1000 大当たり / >0 あたり / 0でも報酬あり=あたり / それ以外 はずれ）が
    // キット側で決まる。ゲーム側は当落を判定しない＝サーバが返した事実だけを渡す。
    [DllImport("__Internal")]
    private static extern void PoiResultShow(int payout, string detailHtml, string reward, string gameObjectName, string onDoneMethod);

    [DllImport("__Internal")]
    private static extern void PoiResultClose();

    [DllImport("__Internal")]
    private static extern void WarukyureCampaignResultReady(string runId);

    // 共通ヘッダーAの残高再取得（結果演出の完了後にのみ呼ぶ＝先出し防止）
    [DllImport("__Internal")]
    private static extern void PoiRefreshHeaderBalance();

    // 2026-09-13 社長指示: リザルト/エラーからタイトルへ戻る時に一度リロードする
    // (PoiStartButton等の状態機械固着対策)。Yabuzame の PoiPlatformBridge.jslib から逐語移植。
    [DllImport("__Internal")]
    private static extern void PoiReloadPage();

    // 2026-09-14 社長指摘「遷移先をゲーム一覧＝ポータルにしてくれ」への対応。
    // poierr v3の「戻る」でタイトルへ戻すと再エラーのループになるため、ポータルへ遷移する。
    // 参照実装: ~/Unity/Kurohige の KurohigePlatform.jslib PoiErrBackToPortal / KurohigeGameController.cs BackToPortal()。
    [DllImport("__Internal")]
    private static extern void PoiErrBackToPortal();

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

        // 2026-09-15: リサイズ/回転/iOSツールバー表示切替でHが変わってもBoardRoot(上下中央配置)
        // がズレたままにならないよう追従させる。BET/SPINボタンはBoardRootの子のため自動追従。
        if (boardRoot != null &&
            (!Mathf.Approximately(Screen.width, boardLiftScreenW) || !Mathf.Approximately(Screen.height, boardLiftScreenH)))
        {
            boardLiftScreenW = Screen.width;
            boardLiftScreenH = Screen.height;
            boardRoot.anchoredPosition = new Vector2(0, ComputeBoardBottomY());
        }
    }

    void Start()
    {
        gameObject.name = "WarukyureBoard";
        SetupCanvas();
        CreateBoardRoot();
        CreateBoardImage();
        CreateCellDimmers();
        CreateLamp();
        CreateAdVirtuaPlaceholder();
        AdVirtuaMonitorSetup.Setup();
        gameObject.AddComponent<AdVirtuaResizeWatcher>();
        CreateHelpButton();
        CreateBetButtons();
        CreateSpinButton();
        CreateResultOverlay();
        CreateJackpotChallengeUI();
        gameObject.AddComponent<SoundMuteButton>(); // game-layout-standard.md §2b 共通サウンドミュートボタン
        new GameObject("WarukyureBgm").AddComponent<WarukyureBgm>(); // BGMループ(ミュートはAudioListener一括)

        platformClient = new PlatformApiClient(API_URL.TrimEnd('/'));

        initSessionEnum = InitSession();
        initSessionRoutine = StartCoroutine(initSessionEnum);
        StartCoroutine(TryDebugForceFx());
        // ADVIRTUA の表示はタイトル画面を閉じた時に TitleScreen 側で行う
        // （game-layout-standard.md: ADVIRTUA を出せるのはゲーム画面のみ）。
        titleScreen = new GameObject("TitleScreen").AddComponent<TitleScreen>();
        titleScreen.Init(canvas, this);
    }

    // ----------------- setup -----------------
    void SetupCanvas()
    {
        // 2026-09-15 社長スクショ指摘対応: 同日の台センタリング化(ComputeBoardBottomY、上記
        // CreateBoardRoot参照)で、Ad-Virtuaゾーン下端〜盤面上端／盤面下端〜画面下端の隙間が
        // 機種によっては最大約230design単位まで空くようになった（旧実装は下端吸着＋最大18pxの
        // 部分緩和のみでこの隙間はほぼ皆無だった＝本件は426c252由来）。この隙間にはカメラの
        // 素のクリア色(scene既定値 rgb≈49,77,121の青灰、alpha=0)がバンド状に露出し、
        // 社長スクショで「広告枠下の青い帯」「盤面下の灰色の帯」として見えていたと判定。
        // 座標・レイアウトは一切変えず、露出時の色だけをページ背景(index.html body #F3E1C9,
        // 実測: /Users/suzukimasahiro/Desktop/warukyure/client/index.html 1495行)に合わせ、
        // 隙間が背景と地続きに見えるようにする。
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color32(0xF3, 0xE1, 0xC9, 0xFF);
        }

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

    // 2026-09-15 社長指示（全幅化・ボタン100%可視、同日追って盤面センタリングへ更新）:
    // 盤面一式を anchorMin/Max/pivot=(0,0) の箱(BoardRoot)に入れ、anchoredPosition.y で
    // キャンバス下端からのオフセットを動的制御する。現在はAd-Virtuaゾーン下端〜画面下端の
    // 可視領域内でBoardRootを上下中央配置する方式（上記ComputeBoardBottomY参照）。
    // 中身は従来どおり y=405 を原点とする相対配置のまま（"405 +"のオフセットをこの箱の
    // anchorMin/Max/pivot=(0,0)自体に肩代わりさせるだけで、板絵・当たり判定の相対位置は無変更）。
    void CreateBoardRoot()
    {
        GameObject go = new GameObject("BoardRoot");
        go.transform.SetParent(canvas.transform, false);
        boardRoot = go.AddComponent<RectTransform>();
        boardRoot.anchorMin = new Vector2(0, 0);
        boardRoot.anchorMax = new Vector2(0, 0);
        boardRoot.pivot = new Vector2(0, 0);
        // 2026-09-15 社長指示: Ad-Virtuaゾーン下端〜画面下端の可視領域内でBoardRoot
        // (クロップ後高さ697)を上下中央配置する。ボタン(HELP/BET/SPIN)は常にBoardRootと
        // 一体で動くため、可視範囲外に出ることは無い。
        boardRoot.anchoredPosition = new Vector2(0, ComputeBoardBottomY());
        boardRoot.sizeDelta = new Vector2(720, BoardRootHeight);
        boardLiftScreenW = Screen.width;
        boardLiftScreenH = Screen.height;
    }

    void CreateBoardImage()
    {
        // 2026-09-15 社長指摘(スクショ): 枠外の余白が肌色/薄黄/白で混在して見える件の是正。
        // art_final_v4.png 自体は無変更(削除禁止)のまま、枠外の余白だけを#F3E1C9へ
        // 単色化したコピー art_final_v4_flatbg.png をロード対象に切替える(見た目以外は無変更)。
        Texture2D tex = Resources.Load<Texture2D>("art_final_v4_flatbg");
        if (tex == null)
        {
            Debug.LogError("[Warukyure] art_final_v4_flatbg texture not found.");
            return;
        }

        GameObject go = new GameObject("Board");
        go.transform.SetParent(boardRoot, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(720, BoardRootHeight);

        go.AddComponent<CanvasRenderer>();
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        // 2026-09-15 社長指示: 下段BET/SPIN帯(texture y=697..818)を非表示化。
        // art_final_v4.png 自体は無変更（削除禁止）、uvRectで上側697/819だけを表示する。
        // v=0が下端・v=1が上端（テクスチャ座標系）。保持する上側697pxはv=[(819-697)/819, 1]。
        float keepBottomV = (BoardRootHeightOld - BoardRootHeight) / BoardRootHeightOld; // (819-697)/819
        img.uvRect = new Rect(0f, keepBottomV, 1f, 1f - keepBottomV);

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
            go.transform.SetParent(boardRoot, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // BoardData.CellCenters は「canvas絶対y(=405+盤面内y)」で格納されているため、
            // BoardRoot(=盤面基準の箱)へ移した分だけ405を差し戻す。
            rt.anchoredPosition = new Vector2(kv.Value.x, -(kv.Value.y - 405f));
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
        go.transform.SetParent(boardRoot, false);
        lampRect = go.AddComponent<RectTransform>();
        lampRect.anchorMin = new Vector2(0, 1);
        lampRect.anchorMax = new Vector2(0, 1);
        lampRect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 start;
        BoardData.TryGetCenter("o_01", out start);
        // BoardData値はcanvas絶対y。BoardRoot基準に合わせて405を差し戻す（dim_*と同じ理由）。
        lampRect.anchoredPosition = new Vector2(start.x, -(start.y - 405f));
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
        // 外周／内円でBGMを切り替える（ring4=IN1・loop2=IN2）
        WarukyureBgm.SetTrack(lampTracks[idx]);
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

    void CreateResultOverlay()
    {
        GameObject go = new GameObject("ResultPanel");
        go.transform.SetParent(boardRoot, false);
        resultPanel = go;

        resultPanelRect = go.AddComponent<RectTransform>();
        resultPanelRect.anchorMin = new Vector2(0, 1);
        resultPanelRect.anchorMax = new Vector2(0, 1);
        resultPanelRect.pivot = new Vector2(0.5f, 0.5f);
        // 旧: canvas絶対y=755.5 → BoardRoot基準(405差し戻し)で350.5
        resultPanelRect.anchoredPosition = new Vector2(360, -350.5f);
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
        go.transform.SetParent(boardRoot, false);
        jackpotPanel = go;

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // 旧: canvas絶対y=814.5 → BoardRoot基準(405差し戻し)で409.5（盤面と同じ720x819を覆う）
        rt.anchoredPosition = new Vector2(360, -409.5f);
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
        AddButton("Help", new Vector2(660, 655), new Vector2(32, 32), () => ToggleHelp(), out img);
    }

    // 2026-09-15 社長指示: SPINは「画面中央かつ×4 RING(Ring4Track)と×2 ISLAND(Loop2Track)の
    // 間、池と池の間」に配置。BoardData.CellCenters(canvas絶対y)から両トラックの外形を算出:
    //   Ring4Track y範囲: i_01(524)~ball_i1(684) → BoardRoot内local = 119~279（セル半径18を
    //     加味した外形下端 = 279+18 = 297）
    //   Loop2Track y範囲: m_00(806)~m_16/m_12(958) → local = 401~553（セル半径14.5を加味した
    //     外形上端 = 401-14.5 = 386.5）
    //   池と池の間の中点 = (297 + 386.5) / 2 = 341.75 ← SPINの中心y（BoardRoot内local）
    // 画面中央 = 盤面幅720の中央 = x=360（BoardRootはcanvas幅いっぱいの全幅のため盤面中央=画面中央）
    // サイズは社長指示により不変（170x68）。
    const float SpinCenterX = 360f;
    const float SpinCenterY = 341.75f;
    const float SpinW = 170f;
    const float SpinH = 68f;
    const float SpinTopY = SpinCenterY - SpinH / 2f;  // 307.75
    const float SpinLeftX = SpinCenterX - SpinW / 2f; // 275

    // 2026-09-15 社長指示「ベットボタンが浮いて見えるので、最初にあったみたいに台/シート/枠に
    // 5つのボタンを入れて欲しい。ルーレット始まったら枠ごと非表示」対応。
    // 台の色は旧・下段帯(art_final_v4.png, 削除禁止・非表示化のみ)を実測して近似:
    //   縁(ボーダー)=(200,140,45)…既存pillBorderと同値（帯の縁も同じ金色系だった実測）
    //   地(フィル)  =(245,218,169)…帯のうち焼き込みピルが無い素の帯地を複数箇所サンプルした平均
    //     （旧x=12,y=730付近の実測(236,171,76)～旧x=650,y=705付近(245,215,167)の中間帯色。
    //     ピル/SPIN自体の焼き込みは一切参照しない＝写り込み無し）
    // 台の幅: 画面(design幅は常に720固定=CanvasScalerが幅基準のため機種非依存)から左右
    //   等マージン24pxを残した672。台の内側にさらに左右16px・上下6pxの余白を取り、
    //   その内側にボタン5個(サイズ不変110%)を均等ギャップで並べる（詰めるのはギャップのみ）。
    const float BetSheetMarginX = 24f;  // 台の左右マージン（画面design幅720から等距離）
    const float BetSheetPadX = 16f;     // 台の内枠～ボタン端の余白
    const float BetSheetPadY = 6f;      // 台の内枠～ボタン上下端の余白
    const float BetSheetRadius = 24f;
    const float BetSheetBorderW = 4f;

    void CreateBetButtons()
    {
        // 2026-09-15 社長指示: 下段バー廃止に伴いBETボタンを盤面前面(SPIN直上)へ移設。
        // サイズ=現行の110%(95x96→104.5x105.6)で不変。台の内側に収まるようギャップのみ調整する。
        const float betW = 95f * 1.1f;  // 104.5
        const float betH = 96f * 1.1f;  // 105.6
        const float betSpinGap = 12f;   // SPINとの間隔
        const float betTopY = SpinTopY - betSpinGap - betH; // 307.75-12-105.6 = 190.15

        float sheetX = BetSheetMarginX;                       // 24
        float sheetW = 720f - 2f * BetSheetMarginX;           // 672
        float sheetTopY = betTopY - BetSheetPadY;             // 184.15
        float sheetH = betH + 2f * BetSheetPadY;              // 117.6
        // 台の下端(sheetTopY+sheetH=301.75) < SPIN上端(SpinTopY=307.75) を常に満たす
        // （betSpinGap=12 > BetSheetPadY*2=12 は等号だが実際は 6+6=12 でちょうど境界。
        //   下記Assertで機種非依存に検証する。座標は全てdesign単位＝機種非依存）
        Debug.Assert(sheetTopY + sheetH <= SpinTopY,
            "[Warukyure] BetSheet overlaps Spin button");

        float innerW = sheetW - 2f * BetSheetPadX;            // 640
        const float betCount = 5f;
        float gap = (innerW - betCount * betW) / (betCount - 1f); // (640-522.5)/4 = 29.375
        float betStartX = sheetX + BetSheetPadX;              // 24+16=40

        Color pillBorder = new Color32(200, 140, 45, 255); // art_final_v4実測近似(縁の金色)
        Color pillFill = new Color32(250, 229, 186, 255);  // art_final_v4実測近似(ピル地色)
        Color textColor = new Color32(90, 55, 20, 255);    // 焼き込み文字の濃茶に近似
        Color sheetFill = new Color32(245, 218, 169, 255); // 旧帯地の実測近似（ピル/SPIN焼き込み非参照）

        // 台（枠）を先に生成してボタンより背面に置く（sibling順で先=背面）。
        GameObject sheetGO = new GameObject("BetSheet");
        sheetGO.transform.SetParent(boardRoot, false);
        RectTransform srt = sheetGO.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1);
        srt.anchorMax = new Vector2(0, 1);
        srt.pivot = new Vector2(0, 1);
        srt.anchoredPosition = new Vector2(sheetX, -sheetTopY);
        srt.sizeDelta = new Vector2(sheetW, sheetH);
        AddPillBackground(sheetGO.transform, new Vector2(sheetW, sheetH), BetSheetRadius, pillBorder, sheetFill, BetSheetBorderW);
        betSheet = sheetGO;

        for (int i = 0; i < 5; i++)
        {
            float x = betStartX + i * (betW + gap);
            int bet = int.Parse(betLabels[i]);
            Image img;
            Button btn = AddButton("Bet" + betLabels[i], new Vector2(x, betTopY), new Vector2(betW, betH), () => ToggleBet(bet), out img);
            betButtons[i] = btn;

            // 2026-09-15: 旧位置の焼き込みピル絵(帯ごと非表示化)の代わりに、新位置で
            // ボタンとして視認できるよう手続き的にピル背景+文字を生成する（移設に伴う必須対応）。
            AddPillBackground(btn.transform, new Vector2(betW, betH), 14f, pillBorder, pillFill, 4f);

            GameObject txtGO = new GameObject("BetText");
            txtGO.transform.SetParent(btn.transform, false);
            RectTransform trt = txtGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = Vector2.zero;
            Text betText = txtGO.AddComponent<Text>();
            betText.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
            if (betText.font == null) betText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            betText.fontSize = 20;
            betText.alignment = TextAnchor.MiddleCenter;
            betText.color = textColor;
            betTexts[i] = betText;
            betText.text = "BET " + betLabels[i] + "\n" + missionBet.ToString("N0") + "枚"; // 1選択あたりの消費枚数=missionBet（cost=selectedBets.Count*missionBetと一致、口数ラベル自体には掛けない）。missionBet確定/変更時はUpdateBetButtonTexts()で再計算

            // 光りは110%スケールのピル枠に合わせた角丸で出す（矩形ベタ塗りだと枠からはみ出て見える）
            betButtonImages[i] = AddGlowOverlay(btn.transform, new Vector2(76f * 1.1f, 78f * 1.1f), Mathf.RoundToInt(12f * 1.1f));
        }
    }

    void CreateSpinButton()
    {
        Image img;
        spinButton = AddButton("Spin", new Vector2(SpinLeftX, SpinTopY), new Vector2(SpinW, SpinH), () => OnSpin(), out img);
        spinButtonImage = img;

        // 2026-09-15: 旧位置の焼き込みSPIN絵(帯ごと非表示化)の代わりに、新位置で
        // ボタンとして視認できるよう手続き的に赤ピル背景を生成する（移設に伴う必須対応）。
        AddPillBackground(spinButton.transform, new Vector2(SpinW, SpinH), 18f,
            new Color32(200, 140, 45, 255),  // 縁: 金色（BET同様の近似色で統一）
            new Color32(170, 20, 15, 255),   // 塗り: 赤（art_final_v4実測近似）
            4f);

        // SKIP 中は新背景の「SPIN」と文字が重なって読めないため、ボタンごと差し替える。
        // 背景を完全に覆うオレンジの角丸板を（背景の上・文字の下に）敷き、その上に SKIP を出す。
        GameObject plateGO = new GameObject("SkipPlate");
        plateGO.transform.SetParent(spinButton.transform, false);
        RectTransform prt = plateGO.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(6f, 6f);   // 背景の赤がフチから覗かないよう少し大きく
        spinSkipPlate = plateGO.AddComponent<Image>();
        spinSkipPlate.sprite = MakeRoundedSprite(170, 68, 18, 2f);
        spinSkipPlate.type = Image.Type.Simple;
        spinSkipPlate.color = new Color(1f, 0.58f, 0.10f, 1f);   // オレンジ
        spinSkipPlate.raycastTarget = false;
        plateGO.SetActive(false);

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

    /// <summary>SPIN ボタンを SPIN 表示（赤ピル背景のまま）と SKIP 表示（オレンジ板）で切り替える。</summary>
    void SetSpinButtonSkipMode(bool skip)
    {
        if (spinSkipPlate != null) spinSkipPlate.gameObject.SetActive(skip);
        spinButtonText.text = skip ? "SKIP" : "SPIN";
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

    // 2026-09-15 社長指示対応: BET/SPINを盤面前面の新座標へ移設すると、元の焼き込み絵
    // (ピル形の背景+縁取り)は旧位置(削除済み帯)に取り残されるため、視認できるボタンとして
    // 機能させるにはピル背景の再現が必須（見た目改善ではなく、移設に伴う不可避対応）。
    // 色は art_final_v4.png の該当ピル/SPIN部分をPillow実測して近似。
    // 縁(枠)＋塗り の2枚重ねで簡易的なピル型ボタン背景を作る。戻り値＝塗り側Image(色変更用)。
    Image AddPillBackground(Transform parent, Vector2 size, float radius, Color borderColor, Color fillColor, float borderWidth)
    {
        GameObject bgo = new GameObject("PillBorder");
        bgo.transform.SetParent(parent, false);
        RectTransform brt = bgo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = size;
        Image border = bgo.AddComponent<Image>();
        border.sprite = MakeRoundedSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), Mathf.RoundToInt(radius), 2f);
        border.type = Image.Type.Simple;
        border.color = borderColor;
        border.raycastTarget = false;

        GameObject fgo = new GameObject("PillFill");
        fgo.transform.SetParent(parent, false);
        RectTransform frt = fgo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.5f, 0.5f);
        frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.pivot = new Vector2(0.5f, 0.5f);
        frt.anchoredPosition = Vector2.zero;
        Vector2 fillSize = new Vector2(size.x - borderWidth * 2f, size.y - borderWidth * 2f);
        frt.sizeDelta = fillSize;
        Image fill = fgo.AddComponent<Image>();
        fill.sprite = MakeRoundedSprite(Mathf.RoundToInt(fillSize.x), Mathf.RoundToInt(fillSize.y), Mathf.RoundToInt(Mathf.Max(1f, radius - borderWidth)), 2f);
        fill.type = Image.Type.Simple;
        fill.color = fillColor;
        fill.raycastTarget = false;
        return fill;
    }

    Button AddButton(string name, Vector2 pos, Vector2 size, Action onClick, out Image image)
    {
        GameObject go = new GameObject(name);
        // 2026-09-15 社長指示（全幅化）: 盤面のボタン(Help/BET/SPIN)はBoardRoot(下端吸着)の
        // 子として配置する。posはBoardRoot内の相対y(=旧"405+"を除いた値)を渡すこと。
        go.transform.SetParent(boardRoot, false);
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
        if (TitleScreen.IsShowing) return;
        if (!sessionReady || isRunning) return;
        if (selectedBets.Contains(bet)) selectedBets.Remove(bet);
        else selectedBets.Add(bet);
        UpdateBetButtonState();
        UpdateHeader();
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
        if (TitleScreen.IsShowing) return;
        if (!sessionReady)
        {
            if (IsSessionFailed) ShowSessionError();
            else ShowResultOverlay("通信中…", 1.5f);
            return;
        }
        if (isRunning)
        {
            skipRequested = true;
            return;
        }
        if (spinPreChecking) return; // 「通信中…」表示中
        DismissResultOverlay();
        if (selectedBets.Count == 0)
        {
            ShowResultOverlay("BETを1つ以上選んでください", 1.5f);
            return;
        }
        if (!IsDemoMode())
        {
            ShowResultOverlay("通信中…", -1f, false);
            spinPreChecking = true;
            StartCoroutine(OnSpinPreCheck());
            return;
        }
        StartCoroutine(SpinRound());
    }

    // 2026-09-18 是正: SPIN事前判定の直前にプラットフォーム残高を取り直してから判定する。
    // 起因＝init/state時点のwalletが自ゲームAPI内の値のままで、プラットフォームの
    // 実残高（ヘッダー表示と同じ値）と食い違うと「ヘッダーは十分なのにSPIN事前判定だけ
    // 残高不足」になる事故（社長報告2026-09-18）。取得に失敗した場合は判定をスキップして
    // サーバー側のprepareに任せ、誤って弾かない。
    IEnumerator OnSpinPreCheck()
    {
        if (platformClient == null)
            platformClient = new PlatformApiClient(API_URL.TrimEnd('/'));

        var walletTask = platformClient.GetWalletBalance();
        yield return new WaitUntil(() => walletTask.IsCompleted);
        spinPreChecking = false;

        if (!walletTask.IsFaulted && !walletTask.IsCanceled)
        {
            wallet = walletTask.Result;
        }
        else
        {
            Debug.LogWarning("[PLATFORM] wallet/balance re-fetch failed at spin pre-check; skipping pre-check (server prepare will judge): " + walletTask.Exception?.Message);
            StartCoroutine(SpinRound());
            yield break;
        }

        int cost = selectedBets.Count * missionBet;
        if (wallet < cost)
        {
            // 2026-09-25 是正: 残高不足も既存 poierr で理由付きで出す（共通ヘッダー辞書で「メダルが足りません」）。
            ShowPoiError("INSUFFICIENT_BALANCE", null, false, OnPoiErrRetry, OnPoiErrBack);
            yield break;
        }
        StartCoroutine(SpinRound());
    }

    void ToggleHelp()
    {
        if (isRunning) return;
        ShowResultOverlay($"2/4/6/8/20 を選んで SPIN\n1口{missionBet}枚 / 数字に止まれば number × 倍率 × {missionBet} 枚", -1f);
    }

    void SetMissionBet(int value)
    {
        missionBet = (value > 0) ? value : 100;
        int[] baseJp = { 3000, 1000, 30000, 1000, 5000 };
        for (int i = 0; i < baseJp.Length; i++)
        {
            jpAwardLabels[i] = (baseJp[i] * missionBet / 100).ToString();
            if (jackpotLampTexts[i] != null)
                jackpotLampTexts[i].text = (i == 2) ? "JACKPOT" : jpAwardLabels[i];
        }
        UpdateBetButtonTexts();
    }

    // BETボタンの「BET n\nXXX枚」表示をmissionBetの最新値で再計算する。
    // CreateBetButtons()より先にSetMissionBet()が呼ばれる経路がある場合はbetTexts[i]がまだnullなのでスキップ
    // （CreateBetButtons自身が生成直後にmissionBetの現在値で初期表示するため取りこぼしはない）。
    void UpdateBetButtonTexts()
    {
        for (int i = 0; i < betTexts.Length; i++)
        {
            if (betTexts[i] != null)
                betTexts[i].text = "BET " + betLabels[i] + "\n" + missionBet.ToString("N0") + "枚";
        }
    }

    // 残高・コスト・純益の表示は共通ヘッダー（HTML側）が持つため、
    // ゲーム内の黒帯表示は撤去済み（2026-09-05 社長指示）。
    void UpdateHeader()
    {
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
                    SetMissionBet(authRes.missionBet);
                    yield return StartCoroutine(TryApplyPlatformMissionBet());
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
                    SetMissionBet(res.missionBet);
                    yield return StartCoroutine(TryApplyPlatformMissionBet());
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
            FailSession(ExtractServerErrorCode(lastErrorBody) ?? "E-INIT");
            yield break;
        }
        var initRes = JsonUtility.FromJson<InitResponse>(initBody);
        // #16: token/state が揃っていない応答で先へ進むと NullReference か無効トークンのまま遊べてしまう。
        if (initRes == null || string.IsNullOrEmpty(initRes.token) || initRes.state == null)
        {
            FailSession("E-INIT");
            yield break;
        }
        token = initRes.token;
        PlayerPrefs.SetString(TOKEN_KEY, token);
        wallet = initRes.state.wallet;
        ballMask = initRes.state.ballMask;
        SetMissionBet(initRes.missionBet);
        yield return StartCoroutine(TryApplyPlatformMissionBet());
        lastNet = 0;
        sessionReady = true;
        UpdateHeader();
    }

    // 2026-09-15 是正: warukyure-api(InitSession応答)のmissionBetは既定100固定
    // (本番Lambdaの環境変数MISSION_BET未設定)で、実際の1口単価はSPIN時のPF
    // (ポイカジ・プラットフォーム)prepareで初めて届く。そのためSPIN前のBETボタンが
    // 常に100表示になっていた(社長実機2026-09-15報告)。ここでPF /missions/current
    // (run生成・メダル予約なしの読み取り専用GET)を叩き、取得できればSPIN前から
    // 正しい単価を反映する。取得できない(standalone/demo・未ログイン・通信エラー等)
    // 場合はwarukyure-apiから受け取った値のまま維持し、100へ戻さない。
    IEnumerator TryApplyPlatformMissionBet()
    {
        if (platformClient == null)
            platformClient = new PlatformApiClient(API_URL.TrimEnd('/'));

        var task = platformClient.GetCurrentMissionCostMedal();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogWarning("[PLATFORM] missions/current failed; keeping warukyure-api missionBet: " + task.Exception?.Message);
        }
        else
        {
            int? costMedal = task.Result;
            if (costMedal.HasValue && costMedal.Value > 0)
                SetMissionBet(costMedal.Value);
        }

        // 2026-09-18 是正: wallet も missionBet と同じ場所・同じ作りで先取り同期する。
        // init/auth/stateが返すwalletは自ゲームAPI内の値で、プラットフォームの実残高
        // （ヘッダー表示と同じ値）になるのは、これまでは初回SPINがresolveした後だけ
        // だった。そのため初回SPIN前は事前判定がヘッダーと食い違うwalletで弾くことが
        // あった（社長報告2026-09-18）。取得に失敗したら既存のwalletのまま維持し、
        // 例外で止めない。
        var walletTask = platformClient.GetWalletBalance();
        yield return new WaitUntil(() => walletTask.IsCompleted);

        if (!walletTask.IsFaulted && !walletTask.IsCanceled)
        {
            wallet = walletTask.Result;
        }
        else
        {
            Debug.LogWarning("[PLATFORM] wallet/balance failed; keeping existing wallet: " + walletTask.Exception?.Message);
        }
    }

    IEnumerator SpinRound()
    {
        isRunning = true;
        skipRequested = false;
        SetSpinButtonSkipMode(true);
        // 2026-09-15 社長指示: SPINタップでBETボタンを非表示にし、回転中はSTOP以外を隠す。
        SetBetButtonsVisible(false);
        DismissResultOverlay();
        UpdateBetButtonState();

        platformEnabled = false;
        platformRun = null;
        ShowResultOverlay("通信中…", -1f, false);

        // launch → token → plays(prepare)。
        platformPrepareFailed = false;
        yield return StartCoroutine(TryPreparePlatform());
        // 2026-09-25 是正: 準備失敗時は TryPreparePlatform 内で poierr を出しているのに、
        // ここで止めずに後続の prepare/resolve・ランプ演出まで続行していた（失敗を無視して続行）。
        // demo=1（検証用URL）の既存フォールバックだけは残す。
        if (platformPrepareFailed && !IsDemoMode()) yield break;

        // prepare
        string demoPart = IsDemoMode() ? ",\"demo\":true" : "";
        // 2026-09-15 サーバー担当セッション依頼(社長指示「クロードで本番化して」):
        // prepareにplay_tokenを付ける。現行の本番サーバーは必須にしていないため無害
        // (付けた版が本番に出た後にサーバー側が賭け額比例を出し直す)。TryPreparePlatform()
        // がplatform連携を有効化した場合のみplatformRun.PlayTokenを保持しているのでその時だけ付与。
        string playTokenPart = (platformEnabled && platformRun != null && !string.IsNullOrEmpty(platformRun.PlayToken))
            ? ",\"play_token\":\"" + platformRun.PlayToken + "\""
            : "";
        string prepareJson = "{\"action\":\"prepare\",\"token\":\"" + token + "\",\"runId\":\"" + currentRunId + "\"" + demoPart + playTokenPart + "}";
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
                // 2026-09-20: サーバーが実コード({"code":"..."}等)を返していればそれを渡し、
                // 無ければ従来どおり固定タグへフォールバック。
                ShowPoiError(ExtractServerErrorCode(lastErrorBody) ?? "E-RETRY", currentRunId, true, OnPoiErrRetry, OnPoiErrBack);
                yield break;
            }
            currentRunId = stuckId;
            // 中断ランは PF と紐付いていない可能性があるため、既存のゲームフローに戻す。
            platformEnabled = false;
            platformRun = null;
        }

        // prepare 応答から最新 missionBet を受信（サーバー正本）。PF 有効時は plays() で受け取った bet を優先。
        if (!platformEnabled && !string.IsNullOrEmpty(prepareBody))
        {
            var prepareRes = JsonUtility.FromJson<PrepareResponse>(prepareBody);
            if (prepareRes != null) SetMissionBet(prepareRes.missionBet);
        }
        // このプレイの単価をここで確定・保持する（PF有効時はTryPreparePlatform内のSetMissionBet(platformRun.Bet)で
        // 既に反映済み）。以後missionBetが更新されてもこのプレイのリザルト表示はこの値のまま。
        playMissionBet = missionBet;

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
            string runId = platformRun != null ? platformRun.RunId : currentRunId;
            string playToken = platformRun != null ? platformRun.PlayToken : null;
            // 2026-09-20: 実コードがあれば渡す。無ければ従来の固定タグへフォールバック。
            yield return StartCoroutine(TryAbortAndShowPopup(ExtractServerErrorCode(lastErrorBody) ?? "E-RESOLVE", runId, playToken));
            yield break;
        }

        lastResult = JsonUtility.FromJson<ResolveResponse>(resolveBody);
        if (lastResult == null)
        {
            EndRound(API_RETRY_MSG);
            string runId = platformRun != null ? platformRun.RunId : currentRunId;
            string playToken = platformRun != null ? platformRun.PlayToken : null;
            yield return StartCoroutine(TryAbortAndShowPopup("E-PARSE", runId, playToken));
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
            string runId = platformRun != null ? platformRun.RunId : currentRunId;
            string playToken = platformRun != null ? platformRun.PlayToken : null;
            yield return StartCoroutine(TryAbortAndShowPopup("E-VALIDATE", runId, playToken));
            yield break;
        }

        wallet = lastResult.state.wallet;
        // [社長確定] 2026-09-06「結果の前にボールが増えちゃってる、ちゃんと結果が出てから反映してくれ」
        // ここでコレクションを反映するとランプ停止前に光る＝結果の先バレ。
        // 反映は共通リザルト画面を閉じた後（RunPoiResult 末尾 ApplyPendingBallMask）。

        // lamp animation
        DismissResultOverlay();
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

        // [プレイ] → s2s_commit → PF resolve → wallet/balance
        if (platformEnabled && platformRun != null)
        {
            yield return StartCoroutine(SettlePlatformRun());
        }

        // 2026-09-13 メイン指摘是正: SettlePlatformRun()がS2sCommit/Resolve失敗で
        // TryAbortAndShowPopup経由の復旧表示に分岐していても、このShowResult呼び出し自体は
        // (既存の制御フロー変更はスコープ外のため)そのまま実行される。そのためreload可否は
        // 「そもそもplatform runでなかった(!platformEnabled)」か「SettlePlatformRun()が成功し
        // Resolveがstate=="SETTLED"でこのrunに確定した(platformSettledRunId==currentRunId)」
        // 場合だけ true にする。
        // 2026-09-13 是正(第1回codex指摘P1): 上記の!platformEnabledだけでは、409復旧分岐
        // (TryPreparePlatformでplatform run Aを取得済み→game側prepareが409→既存run Bへ
        // 復帰する際にplatformEnabled=falseへ戻す)で「Aが未精算のまま残っている」ケースを
        // 「そもそもplatform runではなかった」と誤判定してしまう。
        // 2026-09-13 是正(第2回codex指摘P1): さらに、単一のstring変数で保持すると
        // 「Aが未精算のまま、タイトルからの次のSPINやエラー再試行でplatform run Cを
        // 新規取得」した場合に、Cのrunidで上書きされAの未精算が追跡できなくなり、
        // Cがreload対象時にはAの未精算を見落とす。pendingUnsettledPlatformRunIds
        // (409復旧等のcurrentRunId差し替えでは中身が書き換わらない不変集合)に
        // 1件でも未精算runが残っている間はreloadを禁止する(空である＝Count==0の
        // ことも必須条件に加える)。
        // 2026-09-13 是正(第4回codex指摘P1 その2): 進行中(Add前)のprepare通信が
        // あればそれも未解決runとして扱い、reloadを禁止する。
        // 2026-09-13 是正(第5回codex指摘P1): Prepare()応答喪失で成立有無が不明な
        // runがあれば、以後永続的にreloadを禁止する。
        bool resultAllowReload = pendingUnsettledPlatformRunIds.Count == 0 && platformPrepareInFlight == 0 &&
            !platformPrepareOutcomeUnknown &&
            (!platformEnabled || (platformSettledRunId != null && platformSettledRunId == currentRunId));
        ShowResult(lastResult, resultAllowReload);
    }

    IEnumerator TryPreparePlatform()
    {
        if (platformClient == null)
            platformClient = new PlatformApiClient(API_URL.TrimEnd('/'));

        // 2026-09-13 是正(第4回codex指摘P1 その2): 通信中(Add前)もreload不可として
        // 数えるため、開始時点でインクリメントする。全ての出口(失敗/成功)で必ず
        // デクリメントする。
        platformPrepareInFlight++;
        var task = platformClient.Prepare();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            Debug.LogWarning("[PLATFORM] Prepare failed, falling back to standalone game flow: " + task.Exception?.Message);
            platformRun = null;
            platformEnabled = false;
            platformPrepareInFlight--;
            // 2026-09-13 是正(第5回codex指摘P1): Launch/Token/Playsのいずれの段階の
            // 失敗でも、サーバー側でplaysのrun作成・メダル予約が既に確定している
            // 可能性を否定できない(runidはクライアントに届かず追跡不能)。以後の
            // reloadを永続的に禁止する。
            platformPrepareOutcomeUnknown = true;
            platformPrepareFailed = true;
            // 2026-09-20: PF /plays (Prepare) がdaily_play_limit_reached等の429を返した場合、
            // HttpStatusException.Body に載ったサーバーの実コードを渡す。無ければ従来のE-PREPARE。
            ShowPoiError(ExtractServerErrorCode(task.Exception) ?? "E-PREPARE", currentRunId, true, OnPoiErrRetry, OnPoiErrBack);
            currentRunId = System.Guid.NewGuid().ToString();
            yield break;
        }

        platformRun = task.Result;
        platformEnabled = true;
        currentRunId = platformRun.RunId;
        // 2026-09-13 是正(第1回codex指摘P1)→第2回codex指摘P1でHashSetへ変更:
        // この時点でplatform runを取得したが、精算/返金が確定するまでは
        // 「未精算のplatform runがある」ことを不変集合に追加して記録する。
        // 以前に取得済みで未精算のrun(409復旧で切り離されたAなど)が集合内に
        // 残っていても、今回のCをAdd(上書きでなく追加)するだけなので、
        // Aの未精算記録は失われない。
        pendingUnsettledPlatformRunIds.Add(platformRun.RunId);
        // Addが完了した(=以後はpendingUnsettledPlatformRunIdsの集合判定で追跡可能に
        // なった)ので、進行中カウンタから外す。
        platformPrepareInFlight--;
        SetMissionBet(platformRun.Bet);
        Debug.Log($"[PLATFORM] Prepared runId={platformRun.RunId} bet={platformRun.Bet}");
    }

    IEnumerator SettlePlatformRun()
    {
        platformSettledRunId = null;
        var s2sTask = platformClient.S2sCommit(token, currentRunId, platformRun.PlayToken);
        yield return new WaitUntil(() => s2sTask.IsCompleted);

        if (s2sTask.IsFaulted)
        {
            Debug.LogWarning("[PLATFORM] s2s_commit failed: " + s2sTask.Exception?.Message);
            EndRound(API_RETRY_MSG);
            // 2026-09-20: 実コードがあれば渡す。無ければ従来のE-COMMIT。
            yield return StartCoroutine(TryAbortAndShowPopup(ExtractServerErrorCode(s2sTask.Exception) ?? "E-COMMIT", platformRun.RunId, platformRun.PlayToken, lastResult));
            yield break;
        }

        var resolveTask = platformClient.Resolve(currentRunId, platformRun.PlayToken);
        yield return new WaitUntil(() => resolveTask.IsCompleted);

        if (resolveTask.IsFaulted)
        {
            Debug.LogWarning("[PLATFORM] resolve failed: " + resolveTask.Exception?.Message);
            EndRound(API_RETRY_MSG);
            // 2026-09-20: 実コードがあれば渡す。無ければ従来のE-SETTLE。
            yield return StartCoroutine(TryAbortAndShowPopup(ExtractServerErrorCode(resolveTask.Exception) ?? "E-SETTLE", platformRun.RunId, platformRun.PlayToken, lastResult));
            yield break;
        }

        var resolvedRun = resolveTask.Result;
        // キャンペーンready対象は、同一runがSETTLEDで確定した時だけ。
        if (resolvedRun != null && resolvedRun.ok && resolvedRun.state == "SETTLED" && resolvedRun.run_id == currentRunId)
        {
            platformSettledRunId = currentRunId;
            // 2026-09-13 是正(第1回codex指摘P1、第2回codex指摘P1でHashSet化):
            // このrunの精算が確定したので、未精算集合から取り除く。他のrun(409復旧で
            // 切り離されたAなど)がまだ集合内に残っていればそれはそのまま保持される。
            pendingUnsettledPlatformRunIds.Remove(currentRunId);
        }

        var walletTask = platformClient.GetWalletBalance();
        yield return new WaitUntil(() => walletTask.IsCompleted);

        if (!walletTask.IsFaulted)
        {
            wallet = walletTask.Result;
        }
    }

    void ShowPoiError(string code, string runId, bool refunded, System.Action onRetry, System.Action onBack)
    {
        ResetSpinState();
        DismissResultOverlay();
        PoiErr.Show(code, runId, refunded, onRetry, onBack);
    }

    void ResetSpinState()
    {
        isRunning = false;
        skipRequested = false;
        SetSpinButtonSkipMode(false);
        // 2026-09-15 社長指示: 次回スピン可能な状態に戻ったらBETボタンを再表示。
        SetBetButtonsVisible(true);
    }

    /// <summary>2026-09-15 社長指示: スピン中はBETボタン5個(を収めた台ごと)非表示にし、STOP以外が
    /// 画面の邪魔にならないようにする。次回スピン可能になったら台ごと再表示する。</summary>
    void SetBetButtonsVisible(bool visible)
    {
        for (int i = 0; i < betButtons.Length; i++)
        {
            if (betButtons[i] != null) betButtons[i].gameObject.SetActive(visible);
        }
        if (betSheet != null) betSheet.SetActive(visible);
    }

    void OnPoiErrRetry()
    {
        PoiErr.Hide();
        DismissResultOverlay();
        if (sessionReady) OnSpin();
        else RetrySession();
    }

    // 2026-09-13 メイン指摘是正: 旧コメント「ここに来る時点でabort/resolve等の通信は
    // TryAbortAndShowPopup内で完了済み」は誤りだった。OnPoiErrBackはShowPoiErrorのback
    // コールバックとして複数の異なる文脈(abort失敗・resolve失敗・runId/playToken無し・
    // 通常のabort成功)から共有されており、通信が失敗/未完了のまま到達する経路がある。
    // そのため無条件reloadは廃止し、呼び出し元が「返金/精算が確定した」と確認できた時だけ
    // OnPoiErrBackAllowReload を明示的に渡す。デフォルト(OnPoiErrBack)はreloadしない。
    void OnPoiErrBack() => OnPoiErrBackCore(false);

    void OnPoiErrBackAllowReload() => OnPoiErrBackCore(true);

    void OnPoiErrBackCore(bool allowReload)
    {
        PoiErr.Hide();
        DismissResultOverlay();
        // 2026-09-14 社長指摘「遷移先をゲーム一覧＝ポータルにしてくれ」への対応。
        // 以前はtitleScreen.Reopen()でタイトルへ戻していたため再エラーのループになっていた。
        // 全エラーコード共通でポータルへ遷移する(タイトルへは戻さない)。allowReloadは
        // 「ShowPoiError呼び出し時点で精算/返金が確定済みか」のスナップショットに過ぎない
        // (旧reload許可判定の名残)。未確定(allowReload==false)かつ現在runが未精算のまま
        // 残っているなら、遷移で追跡不能になる前にbest-effortでabortを試みる。
        StartCoroutine(BackToPortal(allowReload));
    }

    IEnumerator BackToPortal(bool allowReload)
    {
        if (!allowReload && platformRun != null && !string.IsNullOrEmpty(platformRun.PlayToken) &&
            pendingUnsettledPlatformRunIds.Contains(platformRun.RunId))
        {
            float deadline = Time.time + 3f;
            var abortTask = platformClient.Abort(platformRun.RunId, platformRun.PlayToken);
            while (!abortTask.IsCompleted && Time.time < deadline) yield return null;
            // 結果(成功/失敗/未完了)は問わない。ここで詰まるとエラーループが直らないため、
            // 遷移は必ず行う(ベストエフォート)。
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        PoiErrBackToPortal();
#else
        Debug.Log("[PoiErr] back to portal");
#endif
    }

    static bool IsCommittedState(string state)
    {
        return state == "RESULT_COMMITTED" || state == "SETTLED";
    }

    IEnumerator TryAbortAndShowPopup(string code, string runId, string playToken, ResolveResponse committedFallback = null)
    {
        if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(playToken))
        {
            ShowPoiError(code, runId, true, OnPoiErrRetry, OnPoiErrBack);
            yield break;
        }

        float deadline = Time.time + 7f;
        var task = platformClient.Abort(runId, playToken);
        while (!task.IsCompleted && Time.time < deadline) yield return null;

        if (task.IsFaulted && Time.time < deadline)
        {
            task = platformClient.Abort(runId, playToken);
            while (!task.IsCompleted && Time.time < deadline) yield return null;
        }

        if (!task.IsCompleted || task.IsFaulted)
        {
            ShowPoiError(code, runId, false, OnPoiErrRetry, OnPoiErrBack);
            yield break;
        }

        var abortRes = task.Result;
        if (IsCommittedState(abortRes.state))
        {
            PoiErr.Hide();
            DismissResultOverlay();
            if (committedFallback != null && committedFallback.ok && committedFallback.state != null)
            {
                ResetSpinState();
                // committedFallback は abort より前に取得済みの結果(=このTryAbortAndShowPopup
                // 自体がS2sCommit/Resolve失敗で呼ばれた経路)であり、精算が確定したとは言えない。
                // reload不可(false)。
                ShowResult(committedFallback, false);
            }
            else
            {
                var resolveTask = platformClient.Resolve(runId, playToken);
                yield return new WaitUntil(() => resolveTask.IsCompleted);
                if (!resolveTask.IsFaulted)
                {
                    var resolved = resolveTask.Result;
                    if (resolved != null && resolved.ok && !string.IsNullOrEmpty(resolved.run_id))
                    {
                        ResetSpinState();
                        string detail = resolved.payout > 0
                            ? BuildScoreDetail("結果", $"{resolved.payout:N0}枚獲得")
                            : BuildLoseDetail();
                        bool settled = resolved.state == "SETTLED" && resolved.run_id == runId;
                        platformSettledRunId = settled ? runId : null;
                        // ここのresolveはabort直後にリカバリとして呼んだもので、state=="SETTLED"
                        // かつrun一致まで確認できた時だけ精算確定とみなしreload可。
                        // 2026-09-13 是正(第1回codex指摘P1、第2回codex指摘P1でHashSet化):
                        // このrunの精算が確定したので、未精算集合から取り除く。
                        if (settled) pendingUnsettledPlatformRunIds.Remove(runId);
                        // 2026-09-13 是正(第3回codex指摘P1): settledはこのrun単体の確定を
                        // 意味するだけで、他のrun(409復旧で切り離されたA等)が未精算のまま
                        // 集合に残っていないことは保証しない。「1件でも未精算runが残れば
                        // reload不可」の条件をここでも満たすため、Remove後の
                        // pendingUnsettledPlatformRunIds.Count == 0 をANDで必須とする。
                        // 2026-09-13 是正(第4回codex指摘P1 その2): 進行中(Add前)のprepare
                        // 通信も未解決run扱いにする。
                        bool recoverAllowReload = settled && pendingUnsettledPlatformRunIds.Count == 0 &&
                            platformPrepareInFlight == 0 && !platformPrepareOutcomeUnknown;
                        StartCoroutine(RunPoiResult(resolved.payout, detail, "", recoverAllowReload));
                        EndRound("");
                        yield break;
                    }
                }
                ShowPoiError(code, runId, false, OnPoiErrRetry, OnPoiErrBack);
            }
            yield break;
        }

        // abortRes.refunded==true はサーバーが返金の実施を保証する確定情報。
        // このrunはもう成立しないため、戻る操作でのreloadを許可してよい。
        // 2026-09-13 是正(第1回codex指摘P1、第2回codex指摘P1でHashSet化): このrunの
        // 返金が確定したので、未精算集合から取り除く。
        if (abortRes.refunded) pendingUnsettledPlatformRunIds.Remove(runId);
        // 2026-09-13 是正(第3回codex指摘P1): abortRes.refundedはこのrun単体の返金確定を
        // 意味するだけで、他のrun(409復旧で切り離されたA等)が未精算のまま集合に残って
        // いないことは保証しない。「1件でも未精算runが残ればreload不可」の条件をここでも
        // 満たすため、Remove後のpendingUnsettledPlatformRunIds.Count == 0をANDで必須とする。
        // 2026-09-13 是正(第4回codex指摘P1 その2): 進行中(Add前)のprepare通信も
        // 未解決run扱いにする。
        bool backAllowReload = abortRes.refunded && pendingUnsettledPlatformRunIds.Count == 0 &&
            platformPrepareInFlight == 0 && !platformPrepareOutcomeUnknown;
        ShowPoiError(code, runId, abortRes.refunded, OnPoiErrRetry,
            backAllowReload ? (System.Action)OnPoiErrBackAllowReload : OnPoiErrBack);
    }

    void EndRound(string error)
    {
        isRunning = false;
        skipRequested = false;
        SetSpinButtonSkipMode(false);
        // 2026-09-15 社長指示: ラウンド終了・次回スピン可能になったらBETボタンを再表示。
        SetBetButtonsVisible(true);
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

    // 2026-09-20 追加(社長指示: 日次プレイ上限で正確な文言を出す)。
    // サーバーが返すエラー応答 {"code":"daily_play_limit_reached"} (poicasi-platform
    // src/handler.mjs buildGameErrorResponse) や {"error":"..."} (play-cap.mjs等) から
    // 実コードを取り出す。見つからなければnullを返し、呼び出し側は従来の固定フェーズ
    // タグ(E-PREPARE等)へフォールバックする(=新しいエラー処理機構は増やさず、
    // 既存の失敗パスに実コードを流し込む1本だけを通す)。
    static string ExtractJsonStringField(string body, string key)
    {
        if (string.IsNullOrEmpty(body)) return null;
        string needle = "\"" + key + "\":\"";
        int i = body.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0) return null;
        i += needle.Length;
        int j = body.IndexOf('"', i);
        if (j <= i) return null;
        return body.Substring(i, j - i);
    }

    static string ExtractServerErrorCode(string body)
    {
        string code = ExtractJsonStringField(body, "code");
        if (string.IsNullOrEmpty(code)) code = ExtractJsonStringField(body, "error");
        return string.IsNullOrEmpty(code) ? null : code;
    }

    // task.Exception(AggregateException)の中からHttpStatusExceptionを探し、そのBodyから
    // 実コードを取り出す。無ければnull。
    static string ExtractServerErrorCode(AggregateException ex)
    {
        if (ex == null) return null;
        HttpStatusException http = ex.InnerException as HttpStatusException;
        if (http == null)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                if (inner is HttpStatusException h) { http = h; break; }
            }
        }
        return http == null ? null : ExtractServerErrorCode(http.Body);
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

    // 社長指示(2026-09-16): 高速1周→中速→低速k(10〜15マス、ランダム)で停止する経路の内訳を計算する。
    // ソース区間・ターゲット区間の両方から共通で呼ぶ。
    void ComputeLampPlan(int startIndex, int endIndex, int trackLen, out int fastSteps, out int midSteps, out int slowSteps, out int totalSteps)
    {
        int d = ((endIndex - startIndex) % trackLen + trackLen) % trackLen;
        fastSteps = LAMP_LAPS_FAST * trackLen; // 1周
        int k = UnityEngine.Random.Range(LAMP_SLOW_MIN, LAMP_SLOW_MAX + 1); // 両端含む10〜15
        k = Mathf.Min(k, Mathf.Max(trackLen - 1, 0)); // 短いトラック(例: Ring4=8マス)で破綻しないよう上限をL-1に丸める
        int rem = d;
        if (rem < k) rem += trackLen; // 中速区間が負にならないよう1周分繰り上げる
        midSteps = rem - k;
        slowSteps = k;
        totalSteps = fastSteps + rem;
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

        // 社長指示(2026-09-16): 高速1周→中速→停止k(10〜15)マス手前から低速、で経路を組む。
        // kはトラックごとに毎回ランダムに引き直す（固定位置での賭け見切りを防ぐ）。
        // L=トラック長、startIdx=起点index、endIdx=終点(停止)index として、
        // d=起点から終点までの周回距離(0..L-1)を求め、1周(fast)進んだ後の残り距離remから
        // 低速kマスを差し引いた分を中速(mid)に割り当てる。最終マスは常にendIdxに一致する。
        int fastSteps, midSteps, slowSteps, totalSteps;
        ComputeLampPlan(homeIndex, sourceIndex, srcL, out fastSteps, out midSteps, out slowSteps, out totalSteps);

        List<string> cells = new List<string>();
        List<float> cellSpeeds = new List<float>();
        cells.Add(home);        cellSpeeds.Add(0f);
        for (int i = 1; i <= totalSteps; i++)
        {
            cells.Add(srcArr[(homeIndex + i) % srcL]);
            cellSpeeds.Add(i <= fastSteps ? LAMP_SPEED_FAST : (i <= fastSteps + midSteps ? LAMP_SPEED_MID : LAMP_SPEED_SLOW));
        }

        // warp + target track
        if (pathId != "outer")
        {
            cells.Add(targetCell);  cellSpeeds.Add(LAMP_SPEED_SLOW);   // ワープの1手は低速のまま（移動先を認識させる）
            if (targetTrack != "castle")
            {
                int targetIndex = BoardData.GetIndex(targetCell);
                int stopIndex   = BoardData.GetIndex(stopId);
                int tFastSteps, tMidSteps, tSlowSteps, tTotalSteps;
                ComputeLampPlan(targetIndex, stopIndex, tgtL, out tFastSteps, out tMidSteps, out tSlowSteps, out tTotalSteps);
                for (int i = 1; i <= tTotalSteps; i++)
                {
                    cells.Add(tgtArr[(targetIndex + i) % tgtL]);
                    cellSpeeds.Add(i <= tFastSteps ? LAMP_SPEED_FAST : (i <= tFastSteps + tMidSteps ? LAMP_SPEED_MID : LAMP_SPEED_SLOW));
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
                // BoardData値はcanvas絶対y。lampRectはBoardRoot基準になったため405を差し戻す。
                path.Add(new Vector2(c.x, -(c.y - 405f)));
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
    // 2026-09-13 メイン指摘是正(Poinoshinで発覚した「共通遷移関数への無条件reload禁止」の
    // 教訓を横展開): allowReload は「このrunの精算(S2sCommit/Resolve)が確定した、または
    // そもそもplatform runでなかった」ことを呼び出し元が確認済みの場合だけ true にする。
    // 精算失敗でTryAbortAndShowPopup経由の復旧表示(ShowResult(committedFallback,...))
    // では常に false。
    void ShowResult(ResolveResponse r, bool allowReload)
    {
        lastNet = r.awardBreakdown.net;
        // PF 有効時は SettlePlatformRun() で取得した PF 残高を優先。
        if (!platformEnabled) wallet = r.state.wallet;
        // ballMask はリザルト画面を閉じてから適用する（先バレ防止・上のコメント参照）。
        pendingBallMask = r.state.ballMask;
        hasPendingBallMask = true;

        // ボールコンプリート → JACKPOT チャレンジ（5ランプ）
        if (r.bonusOutcome != null && r.awardBreakdown.jackpot > 0)
        {
            StartCoroutine(RunJackpotChallenge(r, allowReload));
            return;
        }

        // 通常の BIG/MEGA 配当：fx があれば poifx、なければテキスト
        if (r.fx != null && !string.IsNullOrEmpty(r.fx.tier) && r.fx.amount > 0)
        {
            StartCoroutine(RunPoiFxThenResult(r, allowReload));
            return;
        }

        ShowNormalResult(r, allowReload);
        EndRound("");
    }

    // 2026-09-17 poiresult v6（案A）是正: 中央板(detailHtml)をキット標準の型
    // （pr-a-formula＝式型 / pr-a-block＝スコア型 / pr-a-lose2＝はずれ型）に合わせる。
    // 正本: ~/.claude/manuals/poiresult-standard.md §9-4。
    // primaryType が想定外の値（サーバー未知/null/空）で来た場合にsbが空のまま
    // Z3が白紙になっていた問題（fc1245e是正の後継）を、else分岐でpayoutベースの
    // 代替表示に置き換えて塞ぐ。
    static string EscapeHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    // 式型（pr-a-formula）: 材料行(最大4)＋結果。
    static string BuildFormulaDetail(string[] materialLines, int resultValue, string unit = "枚")
    {
        StringBuilder h = new StringBuilder();
        h.Append("<div class=\"pr-a-formula\">");
        foreach (var line in materialLines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            h.Append("<div class=\"pr-a-mat-line pr-fit\">").Append(EscapeHtml(line)).Append("</div>");
        }
        h.Append("<div class=\"pr-a-eqline pr-fit\">＝</div>");
        h.Append("<div class=\"pr-a-result pr-fit\">").Append(resultValue.ToString("N0"))
         .Append("<span class=\"pr-u\">").Append(EscapeHtml(unit)).Append("</span></div>");
        h.Append("</div>");
        return h.ToString();
    }

    // はずれ型（pr-a-lose2）。補足ブロックは0〜1個。
    static string BuildLoseDetail(string subLabel = null, string subText = null)
    {
        StringBuilder h = new StringBuilder();
        h.Append("<div class=\"pr-a-block\"><div class=\"pr-a-label2\">結果</div>")
         .Append("<div class=\"pr-a-lose2 pr-fit\">はずれ</div></div>");
        if (!string.IsNullOrEmpty(subLabel) && !string.IsNullOrEmpty(subText))
        {
            h.Append("<div class=\"pr-a-block\"><div class=\"pr-a-label2\">").Append(EscapeHtml(subLabel)).Append("</div>")
             .Append("<div class=\"pr-a-subnum pr-fit\">").Append(EscapeHtml(subText)).Append("</div></div>");
        }
        return h.ToString();
    }

    // スコア型（pr-a-block）1ブロック。
    static string BuildScoreDetail(string label, string valueText)
    {
        StringBuilder h = new StringBuilder();
        h.Append("<div class=\"pr-a-block\"><div class=\"pr-a-label2\">").Append(EscapeHtml(label)).Append("</div>")
         .Append("<div class=\"pr-a-num2 pr-fit\">").Append(EscapeHtml(valueText)).Append("</div></div>");
        return h.ToString();
    }

    void ShowNormalResult(ResolveResponse r, bool allowReload)
    {
        string detail;
        string reward = "";   // メダル以外の報酬名（無ければ空）
        if (r.primaryType == "out")
        {
            detail = BuildLoseDetail();
        }
        else if (r.primaryType == "number")
        {
            detail = BuildFormulaDetail(new[]
            {
                $"数字 {r.number}",
                $"× 倍率 {r.multiplier}",
                $"× {playMissionBet:N0}枚",
            }, r.awardBreakdown.number);
        }
        else if (r.primaryType == "castle")
        {
            WarukyureSfx.PlayFanfare();   // 城到達のファンファーレ
            detail = BuildFormulaDetail(new[] { "城 90" }, r.awardBreakdown.castle);
        }
        else if (r.primaryType == "ball")
        {
            string name = "???";
            if (r.collection != null && r.collection.ballType >= 0 && r.collection.ballType < 4)
                name = ballNames[r.collection.ballType];
            // [社長確定] 2026-09-06「ボールは残念じゃなくてやったねなのでフラグを直して」
            // メダルは0枚だがボールという報酬を得ている回。報酬名だけ渡し、当落はキットが決める。
            reward = $"{name}ボール";
            detail = BuildScoreDetail("結果", $"{name}ボール獲得");
        }
        else
        {
            // 2026-09-17是正: primaryTypeがサーバーから未知/null/空の値で来た場合の
            // 代替表示。payout(r.awardBreakdown.total)だけを使い、白紙を防ぐ。
            detail = r.awardBreakdown.total > 0
                ? BuildScoreDetail("結果", $"{r.awardBreakdown.total:N0}枚獲得")
                : BuildLoseDetail();
        }
        else
        {
            // 2026-09-17是正: primaryTypeがサーバーから未知/null/空の値で来た場合、
            // 従来はどの分岐にも該当せずsbが空のままZ3(中央板)が白紙になっていた。
            // payout(r.awardBreakdown.total)だけを使った最低限のフォールバックを出す。
            if (r.awardBreakdown.total > 0)
                sb.Append($"{r.awardBreakdown.total:N0}枚 獲得");
            else
                sb.Append("はずれ");
        }

        // 精算表示は共通リザルト画面（poiresult v2）に一本化する。
        // 滞在5秒→自動クローズ→SPIN待機（＝本ゲームのタイトル相当）へ戻る。自動再開はしない。
        // = §5-2b [社長確定] 2026-09-06「全てをタイトル画面に戻せば共通化できるからそういう設計にする」。
        StartCoroutine(RunPoiResult(r.awardBreakdown.total, detail, reward, allowReload));
    }

    // ----------------- 共通リザルト画面 -----------------
    bool poiResultPending;

    // リザルト画面を閉じた後に反映するコレクション状態（先バレ防止）
    int pendingBallMask;
    bool hasPendingBallMask;

    void ApplyPendingBallMask()
    {
        if (!hasPendingBallMask) return;
        hasPendingBallMask = false;
        ballMask = pendingBallMask;
        UpdateCollectionPanel();
    }

    // 共通リザルト表示後に、PF resolveでSETTLED・run一致を確認済みのrunだけ1回ready通知する。
    void NotifyCampaignResultReadyOnce()
    {
        string runId = platformSettledRunId;
        if (string.IsNullOrEmpty(runId) || runId == lastCampaignReadyRunId) return;
        lastCampaignReadyRunId = runId;
#if UNITY_WEBGL && !UNITY_EDITOR
        WarukyureCampaignResultReady(runId);
#endif
    }

    IEnumerator RunPoiResult(int payout, string detail, string reward = "", bool allowReload = false)
    {
        poiResultPending = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        yield return new WaitForEndOfFrame();
        PoiResultShow(payout, detail, reward ?? "", gameObject.name, "OnPoiResultDone");
#else
        Debug.Log($"[poiresult] payout={payout} reward={reward} detail={detail}");
        OnPoiResultDone("");
#endif
        // キット側の滞在時間は5秒。取りこぼし対策に少し余裕を持たせた保険タイムアウト。
        float t = 0f;
        bool campaignReadyChecked = false;
        while (poiResultPending && t < 7f)
        {
            t += Time.deltaTime;
            yield return null;
            // 共通リザルト表示中（表示後1フレーム）に同一runだけready通知する。
            if (!campaignReadyChecked && poiResultPending)
            {
                campaignReadyChecked = true;
                NotifyCampaignResultReadyOnce();
            }
        }
        poiResultPending = false;
        // ヘッダー残高の更新は結果演出の完了後に行う(先出し防止)。
#if UNITY_WEBGL && !UNITY_EDITOR
        PoiRefreshHeaderBalance();
#endif
        // 結果が出きってからコレクションへ反映する。
        ApplyPendingBallMask();
        // 2026-09-15 社長指摘是正「毎回リロードされるのがストレス」: 通常/JACKPOT/FX/
        // 中断リカバリの結果表示後は、もうリロードしない(旧: 2026-09-13追加の
        // allowReload成立時にPoiReloadPage()を1回呼ぶ実装を撤去。allowReload自体の
        // 計算(呼び出し元のrun確定判定)は他のcodex是正箇所に影響するため残置しているが、
        // ここでのreload呼び出しは削除した)。
        // 2026-09-13導入コミット(0f96564)のメッセージ自身が「WarukyureBoardはisRunning
        // フラグがOnSpin/ResetSpinState/EndRound/ShowPoiErrorの全経路で確実にクリアされる
        // plain Buttonベースの作りのため、恒久固着の経路は無い(A: 変更なし・確認のみ)」と
        // 結論している。つまりreloadはYabuzame側の別バグ対策を逐語移植した予防措置に
        // 過ぎず、WarukyureBoard固有の固着が実証されたことは無い。
        // 唯一実在するリスクは、共通リザルトDOM(poiresult/v2、外部kit)側のonCloseが
        // 何らかの理由で発火せず、下のOnPoiResultDoneが呼ばれないまま上のwatchdog(7秒)に
        // 達するケース: この場合 titleScreen.Reopen() が一度も呼ばれず、タイトルが
        // 再表示されないまま操作不能に見える(reloadが隠していた固着の実体はここ)。
        // PoiResultClose()はpoiresult.jslib:23で定義済みだったが、これまでどこからも
        // 呼ばれておらず未使用のセーフティ手段だった。ここで初めて安全網として使い、
        // DOMを強制的に閉じたうえでOnPoiResultDoneと同じ復帰処理を直接実行することで、
        // リロード無しでも必ずタイトルへ戻す(既にコールバックが正常に来ていた場合は
        // IsShowingが既にtrueなのでこのブロックは実行されない=二重呼び出しなし)。
        if (!TitleScreen.IsShowing)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PoiResultClose();
#endif
            OnPoiResultDone("");
        }
    }

    // 2026-09-25 是正: 初期化失敗・接続待ちタイムアウトを既存 poierr へつなぐ。
    // 再試行は OnPoiErrRetry → RetrySession（sessionReady=false のため）。
    public void FailSession(string code)
    {
        if (sessionReady) return;
        if (initSessionEnum != null) StopCoroutine(initSessionEnum);
        sessionFailCode = string.IsNullOrEmpty(code) ? "E-INIT" : code;
        ShowSessionError();
    }

    public void ShowSessionError()
    {
        if (sessionFailCode == null) return;
        ShowPoiError(sessionFailCode, null, false, OnPoiErrRetry, OnPoiErrBack);
    }

    // セッション未確立時のタイトル画面から呼ばれる再接続。
    public void RetrySession()
    {
        if (initSessionEnum != null) StopCoroutine(initSessionEnum);
        sessionReady = false;
        sessionFailCode = null;
        initSessionEnum = InitSession();
        initSessionRoutine = StartCoroutine(initSessionEnum);
    }

    // WebGL jslib からの onClose コールバック
    public void OnPoiResultDone(string _)
    {
        poiResultPending = false;
        if (titleScreen != null) titleScreen.Reopen();
    }

    // ----------------- JACKPOT challenge flow -----------------
    float GetJackpotX(int index)
    {
        float startX = 85f;
        float spacing = 137.5f;
        return startX + index * spacing;
    }

    IEnumerator RunJackpotChallenge(ResolveResponse r, bool allowReload)
    {
        // 通常UIを隠して Header A + Ad-Virtua を最前面に
        SetNormalUIForChallenge(false);
        adVirtuaRect.SetAsLastSibling();

        jackpotAwardText.text = "";
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
        // missionBet により配当がスケールするため、ランプ表示も同率スケール（トップは JACKPOT のまま）。
        string[] labels = {
            (3000 * missionBet / 100).ToString(),
            (1000 * missionBet / 100).ToString(),
            "JACKPOT",
            (1000 * missionBet / 100).ToString(),
            (5000 * missionBet / 100).ToString()
        };
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

        // poifx（サーバー fx があれば）。JACKPOT 専用 SE を指定。
        if (r.fx != null && !string.IsNullOrEmpty(r.fx.tier) && r.fx.amount > 0)
        {
            yield return StartCoroutine(RunPoiFx(r.fx.tier, r.fx.amount, JACKPOT_SE_URL));
        }

        // 通常画面へ復帰
        yield return FadeJackpotPanel(0f);
        jackpotPanel.SetActive(false);
        jackpotPanelGroup.blocksRaycasts = false;
        SetNormalUIForChallenge(true);
        yield return StartCoroutine(RunPoiResult(r.awardBreakdown.total, BuildScoreDetail("JACKPOT", $"{r.bonusOutcome.award:N0}枚"), "", allowReload));
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

    IEnumerator RunPoiFxThenResult(ResolveResponse r, bool allowReload)
    {
        yield return StartCoroutine(RunPoiFx(r.fx.tier, r.fx.amount, null));
        ShowNormalResult(r, allowReload);
        EndRound("");
    }

    IEnumerator RunPoiFx(string tier, int amount, string se = null)
    {
        poiFxPending = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        // 現在のフレームのレンダリング／rAF 完了後にブラウザ側演出を発火し、
        // ブラウザにスタイル更新の機会を与える。
        yield return new WaitForEndOfFrame();
        PoiFxJackpot(tier, amount, "枚", gameObject.name, "OnPoiFxDone", se);
#else
        Debug.Log($"[poifx] {tier} {amount}枚 se={se ?? "(default)"}");
        OnPoiFxDone("");
#endif
        float timeout = 0f;
        while (poiFxPending && timeout < 5.5f)
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
                fx = new FxData { tier = "mega", amount = 3000 }
            };
            // devデバッグ用の演出だけ発火(抽選・精算なし)なのでreloadは許可しない。
            StartCoroutine(RunJackpotChallenge(fake, false));
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
        public int missionBet;
    }

    [Preserve]
    [Serializable]
    public class StateResponse
    {
        public StateData state;
        public int missionBet;
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

    [Preserve]
    [Serializable]
    public class PrepareResponse
    {
        public string runId;
        public int masterVersion;
        public int configVersion;
        public int[] betOptions;
        public int wagerPerBet;
        public int missionBet;
    }
}
