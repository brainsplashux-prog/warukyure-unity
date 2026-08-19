using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Babeltower8192 LampAnnouncer の warukyure 向け移植。
///
/// 5 つのランプが左から右へ走り、サーバーですでに決まった winningIndex の
/// 位置で停止する。クライアントは抽選せず、あくまで演出を再生する。
///
/// 元実装からの変更点は warukyure 側の仕様・フォントに合わせたもののみ:
///   - TextMeshProUGUI → UnityEngine.UI.Text（プロジェクトに TMP フォントアセットが無いため）
///   - ラベル色・パネル縦位置を warukyure の JACKPOT チャレンジ配置に合わせる
///   - SKIP 対応のため shouldSkip コールバックを追加（元の減速カーブは維持）
///   - パネル背景を warukyure 盤面トンマナ（緑の芝＋青い水＋金の枠）に差し替え、
///     ランプ本体は Resources/lamp/lit* ・ lamp/off をそのまま使う
/// </summary>
public static class LampAnnouncer
{
    // The panel is rendered at 640x128 and the game lays out at 720 wide,
    // so it is shown one-to-one.  Lamp centres come from the Blender socket
    // positions (-4, -2, 0, 2, 4 on a 10.4-wide slab), which is a 2/10.4
    // fraction of the width between neighbours.
    private const float PanelWidth = 640f;
    private const float PanelHeight = 128f;
    private const float LampSpacing = PanelWidth * 2f / 10.4f;

    // Approved timing: 0.1s per lamp for six laps, 0.2s for two more, then
    // 0.5s per lamp until it lands.
    private const float FastStep = 0.1f;
    private const float MidStep = 0.2f;
    private const float SlowStep = 0.5f;
    private const int LampCount = 5;
    private const int FastLaps = 6;
    private const int MidLaps = 2;

    // ランプを切り抜くとき、マスクで丸くくり抜く範囲。
    // ベージュのリングを残しつつ、周りの石板は背景に置き換えるために
    // マスクはリングすぐ外側（半径 38）で切る。
    private const int LampDiameter = 80;
    private const int LampMaskDiameter = 76;

    private static Texture2D[] litFrames;
    private static Texture2D offFrame;
    private static Sprite maskSprite;

    // パネル背景色は art_final_v2.png から実際にサンプリングした値（推測無し）
    // grass: art_final_v2 上部の芝 (x 150-600, y 80-130) 平均
    // water: art_final_v2 右下の水 (x 500-570, y 520-560) 平均
    // gold: art_final_v2 盤面金枠 4 辺の平均
    private static readonly Color32 GrassColor = new Color32(115, 154, 82, 255);
    private static readonly Color32 WaterColor = new Color32(73, 118, 122, 255);
    private static readonly Color32 GoldColor = new Color32(236, 211, 168, 255);

    private static void LoadFrames()
    {
        if (litFrames != null) return;
        litFrames = new Texture2D[LampCount];
        for (int i = 0; i < LampCount; i++)
            litFrames[i] = Resources.Load<Texture2D>("lamp/lit" + i);
        offFrame = Resources.Load<Texture2D>("lamp/off");
    }

    private static Sprite MaskSprite()
    {
        if (maskSprite != null) return maskSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32 white = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] px = new Color32[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(center, new Vector2(x, y));
                px[y * size + x] = d <= r ? white : clear;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        maskSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return maskSprite;
    }

    private static Texture2D MakePanelTexture()
    {
        int w = (int)PanelWidth;
        int h = (int)PanelHeight;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[w * h];
        const int borderH = 8;
        const int borderW = 8;

        // 芝（上）と水（下）を緩やかな波状の境界で分ける
        for (int x = 0; x < w; x++)
        {
            int boundary = (int)(h * 0.5f
                + 8f * Mathf.Sin(x * 0.018f)
                + 4f * Mathf.Sin(x * 0.06f + 1f));

            for (int y = 0; y < h; y++)
            {
                int idx = y * w + x;
                bool border = y < borderH || y >= h - borderH || x < borderW || x >= w - borderW;
                pixels[idx] = border ? GoldColor : (y > boundary ? WaterColor : GrassColor);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Font JapaneseFont()
    {
        var font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    private static IEnumerator WaitOrSkip(float seconds, Func<bool> isSkip)
    {
        if (isSkip == null)
        {
            yield return new WaitForSeconds(seconds);
            yield break;
        }

        float t = 0f;
        while (t < seconds && !isSkip())
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// The amount board: five payouts, ascending, left to right.
    /// </summary>
    public static IEnumerator Run(int[] amounts, int winningIndex, Func<bool> isSkip = null)
    {
        if (amounts == null || amounts.Length != LampCount) yield break;
        var labels = new string[LampCount];
        for (int i = 0; i < LampCount; i++) labels[i] = "×" + amounts[i].ToString("N0");
        yield return Run(labels, winningIndex, null, isSkip);
    }

    /// <summary>
    /// Runs the announcement and leaves nothing behind.  Yields until the
    /// light has stopped and the winning lamp has been held long enough to
    /// read.
    ///
    /// The board takes plain labels rather than amounts because it is not
    /// only about money: the revival chance runs across the same five lamps
    /// with words on them.  Anything else that has to be announced as one
    /// result out of five belongs here too, not in its own effect.
    /// </summary>
    public static IEnumerator Run(string[] labels, int winningIndex, Color[] labelColors, Func<bool> isSkip = null)
    {
        if (labels == null || labels.Length != LampCount) yield break;
        if (winningIndex < 0 || winningIndex >= LampCount) yield break;

        LoadFrames();
        if (offFrame == null) yield break;

        var root = new GameObject("LampAnnouncer");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // above the play screen, below nothing
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // warukyure canvas reference is 720x1224, match width.
        scaler.referenceResolution = new Vector2(720f, 1224f);
        scaler.matchWidthOrHeight = 0.0f;
        root.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("panel");
        panelGo.transform.SetParent(root.transform, false);
        var panel = panelGo.AddComponent<RawImage>();
        panel.texture = MakePanelTexture();
        panel.raycastTarget = false;
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        // warukyure の JACKPOT ランプ中心と同じ高さ（canvas 中心から 202.5 下）
        panelRect.anchoredPosition = new Vector2(0f, -202.5f);

        Sprite circleMask = MaskSprite();
        var lampImages = new RawImage[LampCount];
        for (int i = 0; i < LampCount; i++)
        {
            float cx = PanelWidth / 2f + (i - 2) * LampSpacing;
            float cy = PanelHeight / 2f;

            // マスクは円形。子の RawImage は少し大きめに置き、
            // マスクでリング外の石板を切り落とす。
            var maskGo = new GameObject("lampMask" + i);
            maskGo.transform.SetParent(panelGo.transform, false);
            var maskRect = maskGo.AddComponent<RectTransform>();
            maskRect.anchoredPosition = new Vector2((i - 2) * LampSpacing, 0f);
            maskRect.sizeDelta = new Vector2(LampMaskDiameter, LampMaskDiameter);

            var maskImage = maskGo.AddComponent<Image>();
            maskImage.sprite = circleMask;
            maskImage.color = Color.white;
            maskImage.preserveAspect = false;

            var mask = maskGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var lampGo = new GameObject("lamp" + i);
            lampGo.transform.SetParent(maskGo.transform, false);
            var lampRect = lampGo.AddComponent<RectTransform>();
            lampRect.anchorMin = new Vector2(0.5f, 0.5f);
            lampRect.anchorMax = new Vector2(0.5f, 0.5f);
            lampRect.pivot = new Vector2(0.5f, 0.5f);
            lampRect.anchoredPosition = Vector2.zero;
            lampRect.sizeDelta = new Vector2(LampDiameter, LampDiameter);

            var lamp = lampGo.AddComponent<RawImage>();
            lamp.texture = offFrame;
            lamp.raycastTarget = false;

            // off/lit* 640x128 テクスチャから i 番目のランプだけを切り出す
            float left = (cx - LampDiameter / 2f) / PanelWidth;
            float bottom = (cy - LampDiameter / 2f) / PanelHeight;
            float uWidth = LampDiameter / PanelWidth;
            float uHeight = LampDiameter / PanelHeight;
            lamp.uvRect = new Rect(left, bottom, uWidth, uHeight);

            lampImages[i] = lamp;
        }

        var font = JapaneseFont();
        for (int i = 0; i < LampCount; i++)
        {
            var labelGo = new GameObject("label" + i);
            labelGo.transform.SetParent(panelGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            if (font != null) label.font = font;
            label.text = labels[i] ?? string.Empty;
            label.fontSize = 26;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 26;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = (labelColors != null && labelColors.Length == LampCount)
                ? labelColors[i]
                : new Color(1f, 0.94f, 0.80f);
            label.raycastTarget = false;

            // Babel の TMP outline に相当する弱い縁取り
            var outline = labelGo.AddComponent<Outline>();
            outline.effectColor = new Color32(30, 18, 8, 255);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(LampSpacing, 40f);
            labelRect.anchoredPosition = new Vector2((i - 2) * LampSpacing, -(PanelHeight / 2f) - 26f);
        }

        int position = 0;
        lampImages[position].texture = litFrames[position] != null ? litFrames[position] : offFrame;
        bool IsSkip() => isSkip != null && isSkip();

        for (int step = 0; step < FastLaps * LampCount; step++)
        {
            yield return WaitOrSkip(FastStep, isSkip);
            if (IsSkip()) { position = winningIndex; break; }
            lampImages[position].texture = offFrame;
            position = (position + 1) % LampCount;
            if (litFrames[position] != null) lampImages[position].texture = litFrames[position];
        }

        if (!IsSkip())
        {
            for (int step = 0; step < MidLaps * LampCount; step++)
            {
                yield return WaitOrSkip(MidStep, isSkip);
                if (IsSkip()) { position = winningIndex; break; }
                lampImages[position].texture = offFrame;
                position = (position + 1) % LampCount;
                if (litFrames[position] != null) lampImages[position].texture = litFrames[position];
            }
        }

        if (!IsSkip())
        {
            // A win on the leftmost lamp still gets a full slow lap: stopping
            // the moment the pace drops would read as the effect breaking.
            int remaining = (winningIndex - position + LampCount) % LampCount;
            if (remaining == 0) remaining = LampCount;
            for (int step = 0; step < remaining; step++)
            {
                yield return WaitOrSkip(SlowStep, isSkip);
                if (IsSkip()) { position = winningIndex; break; }
                lampImages[position].texture = offFrame;
                position = (position + 1) % LampCount;
                if (litFrames[position] != null) lampImages[position].texture = litFrames[position];
            }
        }
        else
        {
            for (int i = 0; i < LampCount; i++)
                lampImages[i].texture = offFrame;
            position = winningIndex;
            lampImages[position].texture = litFrames[position] ?? offFrame;
        }

        // Hold on the winner, then blink it so the stop reads as a decision
        // rather than as the animation simply running out.
        yield return WaitOrSkip(0.45f, isSkip);
        if (!IsSkip())
        {
            for (int blink = 0; blink < 3; blink++)
            {
                if (IsSkip()) break;
                lampImages[position].texture = offFrame;
                yield return WaitOrSkip(0.09f, isSkip);
                if (IsSkip()) break;
                lampImages[position].texture = litFrames[position] != null ? litFrames[position] : offFrame;
                yield return WaitOrSkip(0.14f, isSkip);
            }
        }
        yield return WaitOrSkip(0.5f, isSkip);

        UnityEngine.Object.Destroy(root);
    }
}
