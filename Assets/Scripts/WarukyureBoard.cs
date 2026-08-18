using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
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
    private int lastTotal;
    private int ballMask;
    private string currentRunId;
    private bool isRunning;
    private bool skipRequested;
    private readonly HashSet<int> selectedBets = new HashSet<int>();
    private ResolveResponse lastResult;
    private readonly string[] betLabels = { "2", "4", "6", "8", "20" };
    private readonly string[] ballNames = { "うさぎ", "ねこ", "くま", "ことり" };
    private Coroutine overlayRoutine;

    void Start()
    {
        SetupCanvas();
        CreateBoardImage();
        CreateLamp();
        CreateAdVirtuaPlaceholder();
        CreateHeaderText();
        CreateHelpButton();
        CreateBetButtons();
        CreateSpinButton();
        CreateResultOverlay();

        StartCoroutine(InitSession());
    }

    // ----------------- setup -----------------
    void SetupCanvas()
    {
        GameObject canvasGO = new GameObject("Canvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
        Texture2D tex = Resources.Load<Texture2D>("art_final");
        if (tex == null)
        {
            Debug.LogError("[Warukyure] art_final texture not found.");
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
        img.color = Color.white;
        img.raycastTarget = false;
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
        img.texture = CreateCircleTexture(64, new Color32(255, 220, 80, 230));
        img.raycastTarget = false;
        img.color = Color.white;
    }

    void CreateAdVirtuaPlaceholder()
    {
        GameObject go = new GameObject("AdVirtua");
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(720, 405);

        Image img = go.AddComponent<Image>();
        img.color = new Color32(26, 29, 34, 255);
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
        walletText = CreateText("WalletText", new Vector2(450, 1130), new Vector2(400, 32), TextAnchor.MiddleRight, 18);
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

    void CreateHelpButton()
    {
        Image img;
        AddButton("Help", new Vector2(660, 405 + 709), new Vector2(32, 32), () => ToggleHelp(), out img);
    }

    void CreateBetButtons()
    {
        float[] xs = new[] { 16f, 119f, 222f, 325f, 428f };
        for (int i = 0; i < 5; i++)
        {
            int bet = int.Parse(betLabels[i]);
            Image img;
            Button btn = AddButton("Bet" + betLabels[i], new Vector2(xs[i], 405 + 755), new Vector2(95, 52), () => ToggleBet(bet), out img);
            betButtons[i] = btn;
            betButtonImages[i] = img;
        }
    }

    void CreateSpinButton()
    {
        Image img;
        spinButton = AddButton("Spin", new Vector2(536, 405 + 755), new Vector2(168, 52), () => OnSpin(), out img);
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
        spinButtonText.fontSize = 26;
        spinButtonText.alignment = TextAnchor.MiddleCenter;
        spinButtonText.color = Color.white;
        spinButtonText.text = "SPIN";
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
        btn.onClick.AddListener(() => onClick());
        return btn;
    }

    // ----------------- interaction -----------------
    void ToggleBet(int bet)
    {
        if (isRunning) return;
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
            betButtonImages[i].color = on ? new Color32(255, 215, 0, 120) : new Color(0, 0, 0, 0);
        }
    }

    void OnSpin()
    {
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
        walletText.text = $"合計 +{lastTotal} / 残高 {wallet:N0}";
    }

    // ----------------- overlay -----------------
    void ShowResultOverlay(string text, float displayDuration)
    {
        if (resultPanel == null) return;
        resultPanelText.text = text;
        resultPanel.SetActive(true);
        resultPanelGroup.blocksRaycasts = true;
        if (overlayRoutine != null) StopCoroutine(overlayRoutine);
        overlayRoutine = StartCoroutine(OverlayRoutine(displayDuration));
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

    IEnumerator OverlayRoutine(float displayDuration)
    {
        yield return FadeOverlay(1f);
        if (displayDuration > 0)
        {
            yield return new WaitForSeconds(displayDuration);
            yield return FadeOverlay(0f);
            resultPanelGroup.blocksRaycasts = false;
            resultPanel.SetActive(false);
        }
        overlayRoutine = null;
    }

    IEnumerator FadeOverlay(float target)
    {
        float start = resultPanelGroup.alpha;
        float t = 0f;
        const float FADE = 0.3f;
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
                    lastTotal = 0;
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
        if (initRes != null)
        {
            token = initRes.token;
            PlayerPrefs.SetString(TOKEN_KEY, token);
            wallet = initRes.state.wallet;
            ballMask = initRes.state.ballMask;
            lastTotal = 0;
            UpdateHeader();
        }
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
        string prepareJson = "{\"action\":\"prepare\",\"token\":\"" + token + "\",\"runId\":\"" + currentRunId + "\"}";
        string prepareBody = null;
        string prepareErr = null;
        yield return StartCoroutine(ApiPost(prepareJson, currentRunId, (b) => prepareBody = b, (e) => prepareErr = e));
        if (!string.IsNullOrEmpty(prepareErr))
        {
            EndRound("通信エラー: " + prepareErr);
            yield break;
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
            EndRound("通信エラー: " + resolveErr);
            yield break;
        }

        lastResult = JsonUtility.FromJson<ResolveResponse>(resolveBody);
        if (lastResult == null)
        {
            EndRound("レスポンス解析エラー");
            yield break;
        }

        wallet = lastResult.state.wallet;
        ballMask = lastResult.state.ballMask;

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
        EndRound("");
    }

    void EndRound(string error)
    {
        isRunning = false;
        skipRequested = false;
        spinButtonText.text = "SPIN";
        if (!string.IsNullOrEmpty(error)) ShowResultOverlay(error, -1f);
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

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success || req.responseCode != 200)
        {
            string msg = $"HTTP {req.responseCode}";
            if (!string.IsNullOrEmpty(req.error)) msg += " " + req.error;
            onErr(msg);
        }
        else
        {
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
        int sourceDist = (sourceIndex - homeIndex + srcL) % srcL;

        int naturalSteps = sourceDist;
        int targetDist = 0;
        string[] tgtArr = null;
        int tgtL = 0;

        if (pathId != "outer")
        {
            naturalSteps += 1; // warp step
            if (targetTrack != "castle")
            {
                tgtArr = BoardData.GetTrackArray(targetTrack);
                tgtL = tgtArr.Length;
                int targetIndex = BoardData.GetIndex(targetCell);
                int stopIndex = BoardData.GetIndex(stopId);
                targetDist = (stopIndex - targetIndex + tgtL) % tgtL;
                naturalSteps += targetDist;
            }
        }

        int lapCount = 0;
        if (naturalSteps < MIN_PATH_STEPS)
        {
            int need = MIN_PATH_STEPS - naturalSteps;
            lapCount = (need + srcL - 1) / srcL;
        }

        List<string> cells = new List<string>();
        // source track: home + laps + to source
        cells.Add(home);
        for (int lap = 0; lap < lapCount; lap++)
        {
            for (int i = 1; i <= srcL; i++)
                cells.Add(srcArr[(homeIndex + i) % srcL]);
        }
        for (int i = 1; i <= sourceDist; i++)
            cells.Add(srcArr[(homeIndex + i) % srcL]);

        // warp + target track
        if (pathId != "outer")
        {
            cells.Add(targetCell);
            if (targetTrack != "castle")
            {
                int targetIndex = BoardData.GetIndex(targetCell);
                for (int i = 1; i <= targetDist; i++)
                    cells.Add(tgtArr[(targetIndex + i) % tgtL]);
            }
        }

        List<Vector2> path = new List<Vector2>();
        foreach (var cid in cells)
        {
            Vector2 c;
            if (BoardData.TryGetCenter(cid, out c))
                path.Add(new Vector2(c.x, -c.y));
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
                lampRect.anchoredPosition = path[path.Count - 1];
            yield break;
        }

        int segments = path.Count - 1;
        float elapsed = 0f;
        while (elapsed < RUN_DURATION)
        {
            if (skipRequested)
            {
                lampRect.anchoredPosition = path[path.Count - 1];
                yield break;
            }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / RUN_DURATION);
            float p = EaseOutCubic(t);
            float f = p * segments;
            int idx = Mathf.FloorToInt(f);
            float frac = f - idx;
            if (idx >= segments)
            {
                lampRect.anchoredPosition = path[path.Count - 1];
            }
            else
            {
                lampRect.anchoredPosition = Vector2.Lerp(path[idx], path[idx + 1], frac);
            }
            yield return null;
        }
        lampRect.anchoredPosition = path[path.Count - 1];
    }

    // ----------------- result -----------------
    void ShowResult(ResolveResponse r)
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

        if (r.bonusOutcome != null && r.awardBreakdown.jackpot > 0)
        {
            sb.Append($"\nJACKPOT {r.awardBreakdown.jackpot}枚");
        }

        lastTotal = r.awardBreakdown.total;
        wallet = r.state.wallet;
        ballMask = r.state.ballMask;
        UpdateHeader();

        ShowResultOverlay(sb.ToString(), 1.5f);
    }

    // ----------------- JSON data classes -----------------
    [Serializable]
    public class StateData
    {
        public int wallet;
        public int ballMask;
    }

    [Serializable]
    public class InitResponse
    {
        public string uid;
        public string token;
        public StateData state;
    }

    [Serializable]
    public class StateResponse
    {
        public StateData state;
    }

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

    [Serializable]
    public class Collection
    {
        public int ballType;
        public int maskBefore;
        public int maskAfter;
        public bool isNew;
    }

    [Serializable]
    public class BonusOutcome
    {
        public int stopIndex;
        public int award;
    }

    [Serializable]
    public class ResolveResponse
    {
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
        public StateData state;
    }
}
