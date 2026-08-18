using UnityEngine;
using UnityEngine.UI;

public class WarukyureBoard : MonoBehaviour
{
    private GameObject messagePanel;

    void Start()
    {
        SetupCanvas();
        CreateAdVirtuaPlaceholder();
        CreateBoardImage();
        CreateHelpButton();
        CreateBetButtons();
        CreateSpinButton();
    }

    void SetupCanvas()
    {
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // SAFE AREA = 720x819, Ad-Virtua = 405 => canvas reference is 720x1224.
        // This makes the board image and all UI parts map 1:1 to layout.json SAFE coordinates.
        scaler.referenceResolution = new Vector2(720, 1224);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject eventGO = new GameObject("EventSystem");
        eventGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    void CreateAdVirtuaPlaceholder()
    {
        GameObject go = new GameObject("AdVirtua");
        go.transform.SetParent(GameObject.Find("Canvas").transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(720, 405);

        Image img = go.AddComponent<Image>();
        img.color = new Color32(26, 29, 34, 255);
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
        go.transform.SetParent(GameObject.Find("Canvas").transform, false);
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

    void CreateHelpButton()
    {
        // layout.json: help x=660 y=709 w=32 h=32 (SAFE coordinates)
        AddButton("Help", new Vector2(660, 405 + 709), new Vector2(32, 32));
    }

    void CreateBetButtons()
    {
        // layout.json: bet_2..bet_20 y=755 w=95 h=52 (SAFE coordinates)
        string[] bets = new[] { "2", "4", "6", "8", "20" };
        float[] xs = new[] { 16f, 119f, 222f, 325f, 428f };
        for (int i = 0; i < bets.Length; i++)
        {
            AddButton("Bet" + bets[i], new Vector2(xs[i], 405 + 755), new Vector2(95, 52));
        }
    }

    void CreateSpinButton()
    {
        // layout.json: spin x=536 y=755 w=168 h=52 (SAFE coordinates)
        AddButton("Spin", new Vector2(536, 405 + 755), new Vector2(168, 52));
    }

    void AddButton(string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(GameObject.Find("Canvas").transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y);
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => ShowNotConnected());
    }

    void ShowNotConnected()
    {
        if (messagePanel != null)
        {
            Destroy(messagePanel);
            messagePanel = null;
            return;
        }

        Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();

        GameObject panel = new GameObject("MessagePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(360, 120);

        Image img = panel.AddComponent<Image>();
        img.color = new Color32(50, 50, 50, 230);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform, false);
        RectTransform trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = Vector2.zero;

        Text txt = textGO.AddComponent<Text>();
        txt.text = "未接続";
        txt.font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (txt.font == null)
        {
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        txt.fontSize = 28;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        messagePanel = panel;
    }
}
