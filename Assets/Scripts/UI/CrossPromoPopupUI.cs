using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 累積プレイ時間に応じて他ゲーム選択画面へ誘導するクロスプロモポップアップ。
/// 素材不要・ランタイム生成。PoiPlayTime と組み合わせて、通常スピン終了時に1度だけ表示する。
/// </summary>
public static class CrossPromoPopupUI
{
    private const string GamesUrl = "https://lp.poicasi.co.jp/games/?from=warukyure&utm_source=warukyure&utm_medium=crosspromo&utm_campaign=2026-08-crosspromo&placement=promo_popup";

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
    private static readonly Color CardColor = new Color(0.97f, 0.96f, 0.94f, 1f); // PostGameCTA ModalCard cream
    private static readonly Color GoldColor = new Color(0.784f, 0.588f, 0.243f, 1f); // POICASI brand gold
    private static readonly Color DarkText = new Color(0.10f, 0.07f, 0.04f, 1f);
    private static readonly Color LightText = new Color(0.97f, 0.96f, 0.94f, 1f);
    private static readonly Color LaterColor = new Color(0.45f, 0.42f, 0.38f, 1f);

    private static GameObject root;
    private static RectTransform gamesBtnRT;
    private static RectTransform laterBtnRT;

    public static bool IsOpen { get; private set; }

    /// <summary>
    /// 到達 tier を消費し、条件を満たせばポップアップを表示する。
    /// 通常スピン終了時に1回だけ呼ぶこと。表示中は SlotGame の入力を全ブロックする。
    /// </summary>
    public static void ShowIfEligible(Canvas canvas, Font referenceFont)
    {
        if (canvas == null || !PoiPlayTime.ConsumeTierReached()) return;
        Build(canvas, ResolveFont(referenceFont));
    }

    private static Font ResolveFont(Font referenceFont)
    {
        if (referenceFont != null) return referenceFont;
        // Unity 6000 以降、組込みレガシーフォント名は LegacyRuntime.ttf でないと解決しない。
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void Build(Canvas canvas, Font font)
    {
        SendGa4Event("promo_popup_shown");

        root = new GameObject("CrossPromoPopup", typeof(RectTransform));
        var rootRt = (RectTransform)root.transform;
        rootRt.SetParent(canvas.transform, false);
        rootRt.localScale = Vector3.one;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        rootRt.SetAsLastSibling(); // リザルト/演出の前面

        NewFullscreenImage(rootRt, "Dim", DimColor);

        float cardWidth = Mathf.Min(600f, rootRt.rect.width * 0.86f);
        var card = NewCenteredImage(rootRt, "ModalCard", CardColor, new Vector2(cardWidth, 600f), new Vector2(0f, -200f));
        var cardRt = card.rectTransform;

        NewCenteredText(cardRt, "Title", font, "他のゲームもあるよ！", 44, DarkText, FontStyle.Bold,
            new Vector2(560f, 100f), new Vector2(0f, 210f));

        NewCenteredText(cardRt, "Subtitle", font, "ちょっと気分を変えて、\nほかのゲームも遊んでみる？", 30, DarkText, FontStyle.Normal,
            new Vector2(520f, 180f), new Vector2(0f, 65f));

        gamesBtnRT = BuildGoldButton(cardRt, font, "GamesButton", "ゲームをえらぶ", new Vector2(0f, -100f));

        laterBtnRT = BuildLaterButton(cardRt, font, new Vector2(0f, -230f));

        var input = root.AddComponent<CrossPromoPopupInput>();
        input.Initialize(gamesBtnRT, laterBtnRT);

        IsOpen = true;
    }

    private static void Close()
    {
        IsOpen = false;
        if (root != null) Object.Destroy(root);
    }

    private static void OpenGamesAndClose()
    {
        SendGa4Event("promo_popup_games_click");
        Application.OpenURL(GamesUrl);
        Close();
    }

    private static Image NewFullscreenImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private static Image NewCenteredImage(RectTransform parent, string name, Color color, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private static Text NewCenteredText(RectTransform parent, string name, Font font, string content, int fontSize,
        Color color, FontStyle style, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        var txt = go.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = content;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    private static RectTransform BuildGoldButton(RectTransform parent, Font font, string name, string label, Vector2 anchoredPos)
    {
        var bg = NewCenteredImage(parent, name, GoldColor, new Vector2(460f, 96f), anchoredPos);
        var t = NewCenteredText(bg.rectTransform, "Label", font, label, 30, LightText, FontStyle.Bold,
            new Vector2(440f, 80f), Vector2.zero);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return bg.rectTransform;
    }

    private static RectTransform BuildLaterButton(RectTransform parent, Font font, Vector2 anchoredPos)
    {
        var go = new GameObject("LaterButton", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 96f);
        rt.anchoredPosition = anchoredPos;
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // 不可視ヒット領域。ラベルが見た目を担う。
        var t = NewCenteredText(rt, "Label", font, "あとで", 28, LaterColor, FontStyle.Normal,
            new Vector2(220f, 60f), Vector2.zero);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return rt;
    }

    /// <summary>
    /// PostGameCTAController.SendGa4Event と同じ dataLayer.push パターン。
    /// WebGL のみ、エラーは無視してユーザーフローを壊さない。
    /// </summary>
    private static void SendGa4Event(string eventName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            Application.ExternalEval(
                $"if(window.dataLayer){{window.dataLayer.push({{event:'{eventName}'}});}}");
        }
        catch (System.Exception) { /* swallow: telemetry must not break the user-facing flow */ }
#endif
    }

    // Input System の生ポインタ + RectTransformUtility で自前ヒット判定。
    // 本ゲームには EventSystem / GraphicRaycaster がないため Button.onClick は発火しない。
    private class CrossPromoPopupInput : MonoBehaviour
    {
        RectTransform gamesBtnRT;
        RectTransform laterBtnRT;
        Canvas parentCanvas;
        Camera hitCamera;
        Vector2 ptrPos;
        bool ptrDown;

        public void Initialize(RectTransform games, RectTransform later)
        {
            gamesBtnRT = games;
            laterBtnRT = later;
            ResolveCamera();
        }

        // 盤面 Canvas は ScreenSpaceCamera なので、当たり判定カメラを null 固定にすると
        // RectangleContainsScreenPoint が永久に false になる（SoundMuteButton と同じ方式で解決する）。
        void ResolveCamera()
        {
            if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                hitCamera = null;
                return;
            }
            hitCamera = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main;
        }

        void Update()
        {
            ReadPointer();
            if (!ptrDown) return;

            if (InRect(gamesBtnRT)) OpenGamesAndClose();
            else if (InRect(laterBtnRT))
            {
                SendGa4Event("promo_popup_later_click");
                Close();
            }
        }

        void ReadPointer()
        {
            ptrDown = false;

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                ptrDown = true;
                ptrPos = Input.GetTouch(0).position;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                ptrDown = true;
                ptrPos = Input.mousePosition;
            }
        }

        bool InRect(RectTransform rt)
            => RectTransformUtility.RectangleContainsScreenPoint(rt, ptrPos, hitCamera);
    }
}
