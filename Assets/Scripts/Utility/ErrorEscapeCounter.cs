using System;

/// <summary>
/// エラー連続時の脱出判定（dec-20260925-072 / dec-20260926-018）。
/// 「エラー表示 → もう一度 → 10秒以内に再びエラー」が3回続いたら脱出（TOPへ戻す）と判定する。
/// 10秒は「もう一度」を押してから次のエラーが出るまでで測る（通信待ちで表示間隔が延びても取りこぼさない）。
/// 成功（試合開始など）で連鎖は切れる。日次上限・残高不足は「詰み」ではないので数えない。
/// UnityEngine に依存しない純ロジック（時刻は呼び出し側から秒で渡す）。
/// </summary>
public sealed class ErrorEscapeCounter
{
    public const double WindowSec = 10.0;
    public const int Threshold = 3;

    private int chain;
    private double retryTappedAt = -1.0;

    public int Chain => chain;

    /// <summary>エラーを表示する直前に呼ぶ。true なら表示せず脱出する。</summary>
    public bool OnError(string code, double nowSec)
    {
        if (IsIgnored(code))
        {
            retryTappedAt = -1.0;
            return false;
        }
        bool continued = retryTappedAt >= 0.0 && nowSec - retryTappedAt <= WindowSec && nowSec >= retryTappedAt;
        chain = continued ? chain + 1 : 1;
        retryTappedAt = -1.0;
        if (chain >= Threshold)
        {
            chain = 0;
            return true;
        }
        return false;
    }

    /// <summary>エラーポップアップの「もう一度」が押された時に呼ぶ。</summary>
    public void OnRetryTapped(double nowSec)
    {
        retryTappedAt = nowSec;
    }

    /// <summary>処理が成功した時に呼ぶ（連鎖を切る）。</summary>
    public void OnSuccess()
    {
        chain = 0;
        retryTappedAt = -1.0;
    }

    public static bool IsIgnored(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        string c = code.Trim().ToUpperInvariant().Replace('-', '_');
        return c == "DAILY_PRIZE_LIMIT" || c == "DAILY_RUN_LIMIT" || c == "DAILY_PLAY_LIMIT_REACHED" || c == "INSUFFICIENT_BALANCE";
    }
}
