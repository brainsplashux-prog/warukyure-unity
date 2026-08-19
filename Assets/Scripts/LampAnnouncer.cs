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

    private static Texture2D[] litFrames;
    private static Texture2D offFrame;

    private static void LoadFrames()
    {
        if (litFrames != null) return;
        litFrames = new Texture2D[LampCount];
        for (int i = 0; i < LampCount; i++)
            litFrames[i] = Resources.Load<Texture2D>("lamp/lit" + i);
        offFrame = Resources.Load<Texture2D>("lamp/off");
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
        panel.texture = offFrame;
        panel.raycastTarget = false;
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        // warukyure の JACKPOT ランプ中心と同じ高さ（canvas 中心から 202.5 下）
        panelRect.anchoredPosition = new Vector2(0f, -202.5f);

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
        panel.texture = litFrames[position] != null ? litFrames[position] : offFrame;
        bool IsSkip() => isSkip != null && isSkip();

        for (int step = 0; step < FastLaps * LampCount; step++)
        {
            yield return WaitOrSkip(FastStep, isSkip);
            if (IsSkip()) { position = winningIndex; break; }
            position = (position + 1) % LampCount;
            if (litFrames[position] != null) panel.texture = litFrames[position];
        }

        if (!IsSkip())
        {
            for (int step = 0; step < MidLaps * LampCount; step++)
            {
                yield return WaitOrSkip(MidStep, isSkip);
                if (IsSkip()) { position = winningIndex; break; }
                position = (position + 1) % LampCount;
                if (litFrames[position] != null) panel.texture = litFrames[position];
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
                position = (position + 1) % LampCount;
                if (litFrames[position] != null) panel.texture = litFrames[position];
            }
        }
        else
        {
            position = winningIndex;
            panel.texture = litFrames[winningIndex] ?? offFrame;
        }

        // Hold on the winner, then blink it so the stop reads as a decision
        // rather than as the animation simply running out.
        yield return WaitOrSkip(0.45f, isSkip);
        if (!IsSkip())
        {
            for (int blink = 0; blink < 3; blink++)
            {
                if (IsSkip()) break;
                panel.texture = offFrame;
                yield return WaitOrSkip(0.09f, isSkip);
                if (IsSkip()) break;
                panel.texture = litFrames[position] != null ? litFrames[position] : offFrame;
                yield return WaitOrSkip(0.14f, isSkip);
            }
        }
        yield return WaitOrSkip(0.5f, isSkip);

        UnityEngine.Object.Destroy(root);
    }
}
