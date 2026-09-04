using UnityEngine;

public sealed class WarukyureSfx : MonoBehaviour
{
    private const float LampStepMinInterval = 0.02f;

    private static WarukyureSfx instance;

    private AudioSource audioSource;
    private AudioClip tapClip;
    private AudioClip lampStepClip;
    private AudioClip lampStopClip;
    private AudioClip fanfareClip;
    private float lastLampStepTime = float.NegativeInfinity;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        tapClip = Resources.Load<AudioClip>("sfx/se_tap");
        lampStepClip = Resources.Load<AudioClip>("sfx/se_lamp_step");
        lampStopClip = Resources.Load<AudioClip>("sfx/se_lamp_stop");
        // JACKPOT/城到達のファンファーレ。既存の確定音源(sfx/)には触らず se/ に新規追加。
        // 音源 = fan6_triumph（2026-09-05 社長選定「両方6で」）
        fanfareClip = Resources.Load<AudioClip>("se/se_fanfare");
    }

    private static WarukyureSfx EnsureInstance()
    {
        if (instance != null) return instance;

        GameObject go = new GameObject(nameof(WarukyureSfx));
        return go.AddComponent<WarukyureSfx>();
    }

    public static void PlayTap()
    {
        WarukyureSfx sfx = EnsureInstance();
        sfx.Play(sfx.tapClip);
    }

    public static void PlayLampStep()
    {
        WarukyureSfx sfx = EnsureInstance();
        float now = Time.unscaledTime;
        if (now - sfx.lastLampStepTime < LampStepMinInterval) return;

        sfx.lastLampStepTime = now;
        sfx.Play(sfx.lampStepClip);
    }

    public static void PlayLampStop()
    {
        WarukyureSfx sfx = EnsureInstance();
        sfx.Play(sfx.lampStopClip);
    }

    /// <summary>JACKPOT獲得時・城到達時のファンファーレ（約5秒）。</summary>
    public static void PlayFanfare()
    {
        WarukyureSfx sfx = EnsureInstance();
        sfx.Play(sfx.fanfareClip);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}
