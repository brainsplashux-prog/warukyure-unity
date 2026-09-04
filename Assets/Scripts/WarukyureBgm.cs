using System.Collections;
using UnityEngine;

/// <summary>
/// BGM ループ再生＋盤面トラックに応じた曲の切り替え。
/// ・外周 = Resources/bgm/bgm_main（雄大な旅立ちの曲）
/// ・ring4（上の小さい内円）= bgm_in1（IN1・BPM180 追跡）
/// ・loop2（下の大きめの内円）= bgm_in2（IN2・BPM180 遭遇）
///   ※ 2026-09-05 社長指示「下の大きめの内円に入った時をIN2 / 上の小さい内円に入った時をIN1」
/// ・ミュートは SoundMuteButton の AudioListener.volume で一括制御されるので、
///   このクラスはミュート状態を一切見ない（SoundMuteButton.cs は変更禁止）。
/// ・iOS/Safari は AudioContext がユーザー操作まで停止しているため、Start() の
///   Play() が実際には鳴らないことがある。初回タップ時に再確認して鳴らし直す。
/// </summary>
public sealed class WarukyureBgm : MonoBehaviour
{
    private const float Volume = 0.55f;   // SE(1.0)に対する伴奏レベル
    private const float FadeSec = 0.12f;  // 切り替え時の短いフェード（ブツ切り防止）

    private static WarukyureBgm instance;
    private AudioSource source;
    private bool unlocked;
    private string currentKey;
    private Coroutine switching;

    /// <summary>盤面トラックID → Resources 内のクリップパス。該当なしは切り替えない。</summary>
    private static string ClipPathFor(string boardTrack)
    {
        switch (boardTrack)
        {
            case "outer": return "bgm/bgm_main";
            case "ring4": return "bgm/bgm_in1";
            case "loop2": return "bgm/bgm_in2";
            default:      return null;   // castle 等は直前の曲を鳴らし続ける
        }
    }

    /// <summary>ランプが乗っているマスのトラックを渡すと、必要な時だけ曲を差し替える。</summary>
    public static void SetTrack(string boardTrack)
    {
        if (instance == null) return;
        string path = ClipPathFor(boardTrack);
        if (path == null || path == instance.currentKey) return;
        instance.SwitchTo(path);
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = Volume;
        currentKey = "bgm/bgm_main";
        source.clip = Resources.Load<AudioClip>(currentKey);
    }

    void Start()
    {
        if (source != null && source.clip != null) source.Play();
    }

    void Update()
    {
        // 初回のユーザー操作でブラウザの音声出力が解禁される。そこで一度だけ鳴らし直す。
        if (unlocked || source == null || source.clip == null) return;
        bool tapped = Input.GetMouseButtonDown(0) || Input.touchCount > 0;
        if (!tapped) return;
        unlocked = true;
        if (!source.isPlaying) source.Play();
    }

    void SwitchTo(string path)
    {
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null) return;
        currentKey = path;
        if (switching != null) StopCoroutine(switching);
        switching = StartCoroutine(FadeSwap(clip));
    }

    IEnumerator FadeSwap(AudioClip next)
    {
        for (float t = 0f; t < FadeSec; t += Time.unscaledDeltaTime)
        {
            source.volume = Volume * (1f - t / FadeSec);
            yield return null;
        }
        source.volume = 0f;
        source.clip = next;
        source.Play();
        for (float t = 0f; t < FadeSec; t += Time.unscaledDeltaTime)
        {
            source.volume = Volume * (t / FadeSec);
            yield return null;
        }
        source.volume = Volume;
        switching = null;
    }
}
