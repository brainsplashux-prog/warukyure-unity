using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Five-lamp announcement for the warukyure JACKPOT challenge.
/// The server supplies winningIndex; this class only presents that result.
/// </summary>
public static class LampAnnouncer
{
    private const float PanelWidth = 640f;
    private const float PanelHeight = 200f;
    private const float LampSpacing = PanelWidth * 2f / 10.4f;

    // Approved timing. Do not change these values independently of the game design.
    private const float FastStep = 0.1f;
    private const float MidStep = 0.2f;
    private const float SlowStep = 0.5f;
    private const int LampCount = 5;
    private const int FastLaps = 6;
    private const int MidLaps = 2;

    private const int LampDiameter = 80;
    private const int LampMaskDiameter = 76;
    private const float SourceLampHeight = 128f;
    private const float SourceLampCenterY = 64f;

    private static readonly float[] LabelLeft = { 55f, 178f, 279f, 424f, 547f };
    private static readonly float[] RingLeft = { 32f, 155f, 0f, 401f, 524f };

    private static Texture2D[] litFrames;
    private static Texture2D offFrame;
    private static Texture2D panelBackground;
    private static Texture2D centerBezelOff;
    private static Texture2D centerBezelOn;
    private static Texture2D sideRing;
    private static Texture2D jackpotPlate;
    private static Sprite maskSprite;
    private static Font labelFont;

    private sealed class GlowSample
    {
        public Text text;
        public Vector2 direction;
        public float baseRadius;
        public float blurRadiusScale;
        public float weight;
        public bool stroke;
    }

    private static void LoadFrames()
    {
        if (litFrames != null) return;
        litFrames = new Texture2D[LampCount];
        for (int i = 0; i < LampCount; i++)
            litFrames[i] = Resources.Load<Texture2D>("lamp/lit" + i);

        offFrame = Resources.Load<Texture2D>("lamp/off");
        panelBackground = Resources.Load<Texture2D>("jpvault/panel_bg");
        centerBezelOff = Resources.Load<Texture2D>("jpvault/bezel_center_off");
        centerBezelOn = Resources.Load<Texture2D>("jpvault/bezel_center_on");
        sideRing = Resources.Load<Texture2D>("jpvault/ring_side");
        jackpotPlate = Resources.Load<Texture2D>("jpvault/label_plate_jackpot");
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
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * size + x] = distance <= radius ? white : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        maskSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return maskSprite;
    }

    private static RectTransform SetTopLeftRect(GameObject go, float x, float y, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RawImage CreateTextureLayer(Transform parent, string name, Texture texture, float x, float y, float width, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        SetTopLeftRect(go, x, y, width, height);
        return image;
    }

    private static Font LabelFont()
    {
        if (labelFont != null) return labelFont;
        labelFont = Font.CreateDynamicFontFromOSFont("Arial Bold", 17);
        if (labelFont == null) labelFont = Font.CreateDynamicFontFromOSFont("Arial", 17);
        if (labelFont == null) labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (labelFont == null) labelFont = Resources.Load<Font>("Fonts/MPLUSRounded1c-Medium");
        return labelFont;
    }

    private static Text CreateLabel(Transform parent, string name, string value, Font font, float x, Color color, bool jackpot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontStyle = FontStyle.Bold;
        text.text = value ?? string.Empty;
        text.fontSize = 17;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignment = TextAnchor.UpperLeft;
        text.color = color;
        text.raycastTarget = false;
        SetTopLeftRect(go, x, 152f, jackpot ? 82f : 38f, 24f);
        return text;
    }

    private static void AddGlowRing(
        List<GlowSample> samples,
        Transform parent,
        string value,
        Font font,
        string name,
        int count,
        float baseRadius,
        float blurRadiusScale,
        float totalWeight,
        bool stroke)
    {
        float sampleWeight = totalWeight / count;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            Text text = CreateLabel(
                parent,
                name + i,
                value,
                font,
                LabelLeft[2],
                Color.clear,
                true);
            samples.Add(new GlowSample
            {
                text = text,
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                baseRadius = baseRadius,
                blurRadiusScale = blurRadiusScale,
                weight = sampleWeight,
                stroke = stroke
            });
        }
    }

    private static GlowSample[] CreateJackpotGlow(Transform parent, string value, Font font)
    {
        var containerGo = new GameObject("jackpotGlow", typeof(RectTransform));
        containerGo.transform.SetParent(parent, false);
        SetTopLeftRect(containerGo, 0f, 0f, PanelWidth, PanelHeight);

        var samples = new List<GlowSample>(61);

        // The stroke source is an outline made from two circular sample bands.
        // Its weights total 1, so UpdateJackpotGlow can normalize opacity exactly.
        AddGlowRing(samples, containerGo.transform, value, font, "strokeInner", 12, 1f, 0.35f, 0.65f, true);
        AddGlowRing(samples, containerGo.transform, value, font, "strokeOuter", 16, 1f, 0.85f, 0.35f, true);

        Text center = CreateLabel(
            containerGo.transform,
            "fillCenter",
            value,
            font,
            LabelLeft[2],
            Color.clear,
            true);
        samples.Add(new GlowSample
        {
            text = center,
            direction = Vector2.zero,
            baseRadius = 0f,
            blurRadiusScale = 0f,
            weight = 0.20f,
            stroke = false
        });
        AddGlowRing(samples, containerGo.transform, value, font, "fillInner", 12, 0f, 0.45f, 0.50f, false);
        AddGlowRing(samples, containerGo.transform, value, font, "fillOuter", 20, 0f, 1f, 0.30f, false);
        return samples.ToArray();
    }

    private static void UpdateJackpotGlow(
        GlowSample[] samples,
        float blurRadius,
        byte fillSourceAlpha,
        byte strokeSourceAlpha)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            GlowSample sample = samples[i];
            float sourceAlpha = (sample.stroke ? strokeSourceAlpha : fillSourceAlpha) / 255f;
            // Normalized alpha: overlapping every sample in a group recombines to
            // exactly the requested source alpha instead of growing with copy count.
            float alpha = 1f - Mathf.Pow(1f - sourceAlpha, sample.weight);
            float radius = sample.baseRadius + blurRadius * sample.blurRadiusScale;
            Vector2 offset = sample.direction * radius;
            SetTopLeftRect(
                sample.text.gameObject,
                LabelLeft[2] + offset.x,
                152f + offset.y,
                82f,
                24f);
            sample.text.color = sample.stroke
                ? new Color(1f, 124f / 255f, 20f / 255f, alpha)
                : new Color(1f, 190f / 255f, 43f / 255f, alpha);
        }
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

    public static IEnumerator Run(int[] amounts, int winningIndex, Func<bool> isSkip = null)
    {
        if (amounts == null || amounts.Length != LampCount) yield break;
        var labels = new string[LampCount];
        for (int i = 0; i < LampCount; i++) labels[i] = "×" + amounts[i].ToString("N0");
        yield return Run(labels, winningIndex, null, isSkip);
    }

    public static IEnumerator Run(string[] labels, int winningIndex, Color[] labelColors, Func<bool> isSkip = null)
    {
        if (labels == null || labels.Length != LampCount) yield break;
        if (winningIndex < 0 || winningIndex >= LampCount) yield break;

        LoadFrames();
        if (offFrame == null || panelBackground == null || centerBezelOff == null ||
            centerBezelOn == null || sideRing == null || jackpotPlate == null)
            yield break;

        var root = new GameObject("LampAnnouncer");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1224f);
        scaler.matchWidthOrHeight = 0.0f;
        root.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("panel");
        panelGo.transform.SetParent(root.transform, false);
        var panel = panelGo.AddComponent<RawImage>();
        panel.texture = panelBackground;
        panel.color = Color.white;
        panel.raycastTarget = false;
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.anchoredPosition = new Vector2(0f, -238.5f);

        var sideRings = new RawImage[LampCount];
        for (int i = 0; i < LampCount; i++)
        {
            if (i == 2) continue;
            sideRings[i] = CreateTextureLayer(panelGo.transform, "sideRing" + i, sideRing, RingLeft[i], 46f, 85f, 85f);
        }

        RawImage centerBezel = CreateTextureLayer(panelGo.transform, "centerBezel", centerBezelOff, 252f, 20f, 136f, 136f);

        Material activeRingMaterial = null;
        Shader uiShader = Shader.Find("UI/Default");
        if (uiShader != null)
        {
            activeRingMaterial = new Material(uiShader);
            activeRingMaterial.name = "LampAnnouncer Active Ring";
            activeRingMaterial.SetVector("_TextureSampleAdd", new Vector4(13f / 255f, 11f / 255f, 5f / 255f, 0f));
        }

        Sprite circleMask = MaskSprite();
        var lampImages = new RawImage[LampCount];
        for (int i = 0; i < LampCount; i++)
        {
            float centerX = PanelWidth / 2f + (i - 2) * LampSpacing;
            float lampLeft = Mathf.Round(centerX - LampDiameter / 2f);
            var maskGo = new GameObject("lampMask" + i);
            maskGo.transform.SetParent(panelGo.transform, false);
            var maskImage = maskGo.AddComponent<Image>();
            SetTopLeftRect(maskGo, lampLeft + 2f, 50f, LampMaskDiameter, LampMaskDiameter);
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
            float left = lampLeft / PanelWidth;
            float bottom = (SourceLampCenterY - LampDiameter / 2f) / SourceLampHeight;
            lamp.uvRect = new Rect(left, bottom, LampDiameter / PanelWidth, LampDiameter / SourceLampHeight);
            lampImages[i] = lamp;
        }

        CreateTextureLayer(panelGo.transform, "jackpotPlate", jackpotPlate, 269f, 149f, 104f, 32f);

        Font font = LabelFont();
        GlowSample[] jackpotGlow = CreateJackpotGlow(panelGo.transform, labels[2], font);
        UpdateJackpotGlow(jackpotGlow, 3f, 140, 80);

        for (int i = 0; i < LampCount; i++)
        {
            bool jackpot = i == 2;
            Color color = jackpot ? new Color32(255, 221, 92, 255) : new Color32(172, 176, 187, 255);
            Text label = CreateLabel(panelGo.transform, "label" + i, labels[i], font, LabelLeft[i], color, jackpot);
            if (jackpot)
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(95, 49, 7, 255);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        void SetLampActive(int index, bool active)
        {
            lampImages[index].texture = active && litFrames[index] != null ? litFrames[index] : offFrame;
            if (index != 2 && sideRings[index] != null)
                sideRings[index].material = active ? activeRingMaterial : null;
        }

        void SetJackpotMaximum()
        {
            centerBezel.texture = centerBezelOn;
            SetTopLeftRect(centerBezel.gameObject, 248f, 16f, 145f, 145f);
            UpdateJackpotGlow(jackpotGlow, 4f, 230, 160);
        }

        int position = 0;
        SetLampActive(position, true);
        bool IsSkip() => isSkip != null && isSkip();

        for (int step = 0; step < FastLaps * LampCount; step++)
        {
            yield return WaitOrSkip(FastStep, isSkip);
            if (IsSkip()) { position = winningIndex; break; }
            SetLampActive(position, false);
            position = (position + 1) % LampCount;
            SetLampActive(position, true);
        }

        if (!IsSkip())
        {
            for (int step = 0; step < MidLaps * LampCount; step++)
            {
                yield return WaitOrSkip(MidStep, isSkip);
                if (IsSkip()) { position = winningIndex; break; }
                SetLampActive(position, false);
                position = (position + 1) % LampCount;
                SetLampActive(position, true);
            }
        }

        if (!IsSkip())
        {
            int remaining = (winningIndex - position + LampCount) % LampCount;
            if (remaining == 0) remaining = LampCount;
            for (int step = 0; step < remaining; step++)
            {
                yield return WaitOrSkip(SlowStep, isSkip);
                if (IsSkip()) { position = winningIndex; break; }
                SetLampActive(position, false);
                position = (position + 1) % LampCount;
                SetLampActive(position, true);
            }
        }
        else
        {
            for (int i = 0; i < LampCount; i++) SetLampActive(i, false);
            position = winningIndex;
            SetLampActive(position, true);
        }

        if (position == 2) SetJackpotMaximum();

        yield return WaitOrSkip(0.45f, isSkip);
        if (!IsSkip())
        {
            for (int blink = 0; blink < 3; blink++)
            {
                if (IsSkip()) break;
                SetLampActive(position, false);
                yield return WaitOrSkip(0.09f, isSkip);
                if (IsSkip()) break;
                SetLampActive(position, true);
                yield return WaitOrSkip(0.14f, isSkip);
            }
        }
        yield return WaitOrSkip(0.5f, isSkip);

        if (activeRingMaterial != null) UnityEngine.Object.Destroy(activeRingMaterial);
        UnityEngine.Object.Destroy(root);
    }
}
