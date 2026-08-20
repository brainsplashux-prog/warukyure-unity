using UnityEngine;
using UnityEngine.UI;

// game-layout-standard.md §2b 共通サウンドミュートボタン。
// アイコン3種はクレーンゲーム版 SoundMuteButton.cs の手続き生成 Sprite を無改変で移植したもの
// （外部PNGは存在せず、このコード自体が正典アセット）。ゲーム別の差し替え・文字ラベルは禁止。
// 押下判定は D-14裁定（2026-08-17）に従い、EventSystem の onClick に依存せず
// 旧Input System の生ポインタ座標＋RectTransformUtility.RectangleContainsScreenPoint で行う。
[DefaultExecutionOrder(-100)]
public class SoundMuteButton : MonoBehaviour
{
    const string PK_MUTE = "wk_mute"; // 1=ミュート / 0(未設定)=音オン
    const float Margin = 18f;         // 右端・Ad-Virtua帯下端からの間隔
    const float ButtonSize = 100f;    // ボタンサイズ（sizeDelta）
    const float AdBandBottom = 405f;  // WarukyureBoard.CreateAdVirtuaPlaceholder の帯下端（720x1224参照系で固定）

    bool soundMuted;
    Image soundSlashIcon;
    RectTransform _root;

    Canvas _parentCanvas;
    Camera _hitCamera;

    void Awake()
    {
        // BGM/SE再生開始より先に実行し、保存されたミュート状態を即反映
        ApplyMute(PlayerPrefs.GetInt(PK_MUTE, 0) == 1, false);
        Build();
    }

    void Start()
    {
        ResolveCamera();
    }

    void Update()
    {
        if (_root == null) return;
        if (_hitCamera == null) ResolveCamera();
        ReadTap();
    }

    void Build()
    {
        Canvas canvas = _parentCanvas;
        if (canvas == null)
        {
            GameObject found = GameObject.Find("Canvas");
            if (found != null) canvas = found.GetComponent<Canvas>();
        }
        if (canvas == null)
        {
            Debug.LogWarning("[Warukyure][SOUND_MUTE] Canvas not found");
            return;
        }
        _parentCanvas = canvas;

        // Button は付けない（D-14裁定: onClick 依存禁止）。
        GameObject root = new GameObject("SoundMuteButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        _root = root.GetComponent<RectTransform>();
        _root.anchorMin = new Vector2(1f, 1f);
        _root.anchorMax = new Vector2(1f, 1f);
        _root.pivot = new Vector2(1f, 1f);
        _root.anchoredPosition = new Vector2(-Margin, -(AdBandBottom + Margin));
        _root.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        Image bg = root.GetComponent<Image>();
        bg.sprite = CircleSprite();
        bg.color = new Color(0.05f, 0.06f, 0.06f, 0.66f);
        bg.raycastTarget = true;

        CreateIcon(root.transform, "SpeakerIcon", SpeakerSprite(), 69f, out _);
        CreateIcon(root.transform, "SpeakerSlash", SlashSprite(), 76f, out soundSlashIcon);
        soundSlashIcon.enabled = soundMuted;
    }

    static void CreateIcon(Transform parent, string name, Sprite sprite, float size, out Image image)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, 0.96f);
        image.preserveAspect = true;
        image.raycastTarget = false;
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

        if (down && RectTransformUtility.RectangleContainsScreenPoint(_root, pos, _hitCamera))
            ToggleSound();
    }

    // 親 Canvas の renderMode に応じて当たり判定用カメラを決定。
    // ScreenSpaceOverlay では null、ScreenSpaceCamera/WorldSpace では
    // canvas.worldCamera → Camera.main の順でフォールバック。
    void ResolveCamera()
    {
        if (_root == null) return;

        if (_parentCanvas == null)
            _parentCanvas = _root.GetComponentInParent<Canvas>();

        if (_parentCanvas == null || _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            _hitCamera = null;
            return;
        }

        _hitCamera = _parentCanvas.worldCamera;
        if (_hitCamera == null) _hitCamera = Camera.main;
    }

    void ToggleSound()
    {
        ApplyMute(!soundMuted, true);
    }

    void ApplyMute(bool mute, bool persist)
    {
        soundMuted = mute;
        AudioListener.volume = mute ? 0f : 1f; // BGM/SE 一括制御
        if (soundSlashIcon != null) soundSlashIcon.enabled = mute;
        if (persist)
        {
            PlayerPrefs.SetInt(PK_MUTE, mute ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // ---- 以下3つの Sprite はクレーンゲーム版からの無改変移植（正典アセット） ----

    static Sprite _circleSprite;
    static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var c = new Color32[N * N];
        float r = N * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = (x + 0.5f) - r, dy = (y + 0.5f) - r;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
            float a = Mathf.Clamp01((1f - d) * 14f); // 内側はベタ、縁だけAA
            c[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(c); tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return _circleSprite;
    }

    static Sprite _speakerSprite;
    static Sprite SpeakerSprite()
    {
        if (_speakerSprite != null) return _speakerSprite;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var c = new Color32[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float fx = x + 0.5f, fy = y + 0.5f; float a = 0f;
            // 本体: 左の小四角(コーン後部)
            if (fx >= 14f && fx <= 24f && fy >= 26f && fy <= 38f) a = 1f;
            // 三角部(ラッパ)
            if (fx >= 24f && fx <= 34f)
            {
                float half = (fx - 24f) * 1.1f + 6f;
                if (Mathf.Abs(fy - 32f) <= half) a = 1f;
            }
            // 音波弧(2本)
            float dx = fx - 34f, dy = fy - 32f; float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (fx >= 36f && dx > Mathf.Abs(dy) * 0.55f)
            {
                if (Mathf.Abs(dist - 12f) <= 1.6f) a = Mathf.Max(a, 1f);
                if (Mathf.Abs(dist - 19f) <= 1.6f) a = Mathf.Max(a, 1f);
            }
            c[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
        }
        tex.SetPixels32(c); tex.Apply();
        _speakerSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return _speakerSprite;
    }

    static Sprite _slashSprite;
    static Sprite SlashSprite()
    {
        if (_slashSprite != null) return _slashSprite;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var c = new Color32[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float d = Mathf.Abs(fy - fx) / 1.41421f;
            float a = (fx >= 10f && fx <= 54f && d <= 3.5f) ? 1f : 0f;
            c[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
        }
        tex.SetPixels32(c); tex.Apply();
        _slashSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return _slashSprite;
    }
}
