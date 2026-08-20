using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 累積プレイ時間を計測し、達成 tier を判定する。
/// WebGL では localStorage、Editor/他プラットフォームでは PlayerPrefs へ永続化する。
/// 全ポイカジゲームが同一オリジン lp.poicasi.co.jp 配下なので GameId で保存キーを分ける。
/// </summary>
public static class PoiPlayTime
{
    public const int PromoFirstSec = 3600;
    public const int PromoRepeatSec = 36000;

    // このリポでは GameId = "warukyure"。他ゲームへコピーする際のみ書き換えること。
    public const string GameId = "warukyure";
    private const string PlayTimeKey = "poi_playtime_sec_" + GameId;
    private const string PromoTierKey = "poi_promo_tier_" + GameId;
    private const float SaveIntervalSec = 30f;

    private static float totalSeconds = -1f;
    private static float unsavedSeconds;
    private static int consumedTier;
    private static int pendingTier;
    private static bool wasPlaying;
    private static bool shownThisSession;

    public static float TotalSeconds
    {
        get
        {
            EnsureLoaded();
            return totalSeconds;
        }
    }

    // 状態 × イベント:
    // inactive + Tick(true)  -> active。遷移フレームは加算しない。
    // active   + Tick(true)  -> 時間を加算。30秒ごとに永続化。
    // active   + Tick(false) -> 永続化し inactive へ。
    // eligible + Consume     -> 到達 tier を消費。同一セッションでは1回まで表示。
    public static void Tick(bool isPlaying)
    {
        EnsureLoaded();
        bool shouldAccumulate = isPlaying && Application.isFocused;
        if (!shouldAccumulate)
        {
            if (wasPlaying) Save();
            wasPlaying = false;
            return;
        }

        if (!wasPlaying)
        {
            wasPlaying = true;
            return;
        }

        float delta = Mathf.Max(0f, Time.unscaledDeltaTime);
        totalSeconds += delta;
        unsavedSeconds += delta;
        int reachedTier = TierForSeconds(totalSeconds);
        if (reachedTier > consumedTier) pendingTier = reachedTier;
        if (unsavedSeconds >= SaveIntervalSec) Save();
    }

    public static bool ConsumeTierReached()
    {
        EnsureLoaded();
        if (shownThisSession) return false;

        if (pendingTier <= consumedTier) return false;

        consumedTier = pendingTier;
        SetInt(PromoTierKey, consumedTier);
        Flush();
        Save();
        pendingTier = 0;
        shownThisSession = true;
        return true;
    }

    private static int TierForSeconds(float seconds)
    {
        if (seconds < PromoFirstSec) return 0;
        return 1 + Mathf.FloorToInt((seconds - PromoFirstSec) / PromoRepeatSec);
    }

    private static void EnsureLoaded()
    {
        if (totalSeconds >= 0f) return;
        totalSeconds = Mathf.Max(0f, GetFloat(PlayTimeKey));
        consumedTier = Mathf.Max(0, GetInt(PromoTierKey));
        int reachedTier = TierForSeconds(totalSeconds);
        pendingTier = reachedTier > consumedTier ? reachedTier : 0;
    }

    private static void Save()
    {
        if (unsavedSeconds > 0f)
        {
            SetFloat(PlayTimeKey, totalSeconds);
            Flush();
            unsavedSeconds = 0f;
        }
    }

    private static void Flush()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // localStorage は jslib 側で即座に書き込む
#else
        PlayerPrefs.Save();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern float PoiPlayTimeGetFloat(string key);
    [DllImport("__Internal")]
    private static extern void PoiPlayTimeSetFloat(string key, float value);
    [DllImport("__Internal")]
    private static extern int PoiPlayTimeGetInt(string key);
    [DllImport("__Internal")]
    private static extern void PoiPlayTimeSetInt(string key, int value);
#endif

    private static float GetFloat(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PoiPlayTimeGetFloat(key);
#else
        return PlayerPrefs.GetFloat(key, 0f);
#endif
    }

    private static void SetFloat(string key, float value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PoiPlayTimeSetFloat(key, value);
#else
        PlayerPrefs.SetFloat(key, value);
#endif
    }

    private static int GetInt(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PoiPlayTimeGetInt(key);
#else
        return PlayerPrefs.GetInt(key, 0);
#endif
    }

    private static void SetInt(string key, int value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PoiPlayTimeSetInt(key, value);
#else
        PlayerPrefs.SetInt(key, value);
#endif
    }
}
