using UnityEngine;

/// <summary>
/// BGM ループ再生。Resources/bgm/bgm_main を loop=true で流し続けるだけの薄い層。
/// ・ミュートは SoundMuteButton の AudioListener.volume で一括制御されるので、
///   このクラスはミュート状態を一切見ない（SoundMuteButton.cs は変更禁止）。
/// ・iOS/Safari は AudioContext がユーザー操作まで停止しているため、Start() の
///   Play() が実際には鳴らないことがある。初回タップ時に再確認して鳴らし直す。
/// </summary>
public sealed class WarukyureBgm : MonoBehaviour
{
    private const string ClipPath = "bgm/bgm_main";
    private const float Volume = 0.55f;   // SE(1.0)に対する伴奏レベル

    private static WarukyureBgm instance;
    private AudioSource source;
    private bool unlocked;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.clip = Resources.Load<AudioClip>(ClipPath);
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = Volume;
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
}
