using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>HTTP status code 付き例外。</summary>
public sealed class HttpStatusException : Exception
{
    public long StatusCode { get; }
    public HttpStatusException(long statusCode, string message) : base(message) { StatusCode = statusCode; }
}

/// <summary>プラットフォーム session cookie 橋渡し。</summary>
public static class PlatformSession
{
    /// <summary>
    /// 生の Cookie ヘッダー値。エディタ/スタンドアローンでは外部から注入する。
    /// WebGL では jslib パッチが credentials: 'include' で送るため、C# からは読まない。
    /// </summary>
    public static string Cookie { get; set; }
}

[Serializable]
public class PlatformLaunchResponse
{
    public bool ok;
    public string launch_code;
}

[Serializable]
public class PlatformTokenResponse
{
    public bool ok;
    public string play_token;
}

[Serializable]
public class PlatformPlaysResponse
{
    public bool ok;
    public string run_id;
    public string state;
    public string asset_code;
    public int bet;
    public long expires_at;
}

[Serializable]
public class PlatformResolveResponse
{
    public bool ok;
    public string run_id;
    public string state;
    public int payout;
    public long wallet_version;
}

[Serializable]
public class PlatformAbortResponse
{
    public bool ok;
    public string run_id;
    public bool refunded;
    public int refund_amount;
    public string state;
    public bool idempotent;
    public string error;
}

[Serializable]
public class PlatformAssetBalance
{
    public string asset_code;
    public int available_units;
}

[Serializable]
public class PlatformWalletBalanceResponse
{
    public bool ok;
    public int wallet_version;
    public PlatformAssetBalance[] assets;
}

[Serializable]
public class S2sCommitResponse
{
    public bool ok;
    public bool idempotent;
}

/// <summary>GET /api/v1/missions/current の mission 部分。cost_medal が1口の消費メダル数の正本。</summary>
[Serializable]
public class PlatformMissionInfo
{
    public int no;
    public string metric;
    public int threshold;
    public int cost_medal;
    public string reward_rank;
}

[Serializable]
public class PlatformMissionCurrentResponse
{
    public bool ok;
    public PlatformMissionInfo mission;
    public int progress;
    public string state;
}

/// <summary>PF plays で発行された 1 プレイの情報。</summary>
public sealed class PlatformRun
{
    public string RunId { get; }
    public string PlayToken { get; }
    public int Bet { get; }
    public long ExpiresAt { get; }

    public PlatformRun(string runId, string playToken, int bet, long expiresAt)
    {
        RunId = runId;
        PlayToken = playToken;
        Bet = bet;
        ExpiresAt = expiresAt;
    }
}

/// <summary>
/// ポイカジ・プラットフォームとの結線クライアント。
/// launch → token → plays(prepare) → [ゲームプレイ] → s2s_commit → resolve の流れを提供する。
/// </summary>
public sealed class PlatformApiClient
{
#if JEM_BUILD
    // JEM分離(jem-games.md §2): 別URL・別platform games.jsonエントリ(jem-warukyure)を叩く。
    // 元warukyureのgame_idは1文字も変えない。
    public const string GameId = "jem-warukyure";
    // JEM専用ゲームLambda(jem-warukyure-api)の既定エンドポイント。
    // DEVの実測URLは呼び出し側(WarukyureBoard.API_URL_DEV)が渡す。ここは空のままにし、
    // 本番(API_URL_PROD空)からの空endpointコンストラクタfallbackでDEVへ向く経路を残さない。
    // 空endpointに対してはSendRawが例外を投げ、InitSessionが拒否する(fail-closed維持)。
    // 元warukyure-apiのURLをここへfallbackとして書いてはいけない(誤配信防止)。
    public const string DefaultEndpoint = "";
#else
    public const string GameId = "warukyure";
    // ゲーム Lambda 既定エンドポイント（DEV）。本番は呼び出し側から API_URL を渡す。
    public const string DefaultEndpoint = "https://b5yl9sml5l.execute-api.ap-northeast-1.amazonaws.com";
#endif
    // PF 既定エンドポイント（dev stage）。WebGL では jslib から同一生成元 URL を取得する。
    private const string FallbackPlatformBaseUrl = "https://c9nrwvslv8.execute-api.ap-northeast-1.amazonaws.com/dev";
    private const int TimeoutSeconds = 10;

    private static string cachedPlatformBaseUrl;

    private readonly string endpoint;

    public PlatformApiClient(string endpoint = null)
    {
        this.endpoint = string.IsNullOrEmpty(endpoint) ? DefaultEndpoint : endpoint;
#if UNITY_WEBGL && !UNITY_EDITOR
        PoiPlatformCredentialsNoop();
#endif
    }

    private string PlatformBaseUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(cachedPlatformBaseUrl)) return cachedPlatformBaseUrl;
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string s = PoiGetPlatformBaseUrl();
                if (!string.IsNullOrEmpty(s))
                {
                    cachedPlatformBaseUrl = s;
                    return s;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlatformApiClient] PoiGetPlatformBaseUrl failed: " + ex.Message);
            }
#endif
            return FallbackPlatformBaseUrl;
        }
    }

    /// <summary>
    /// launch → token → plays で 1 プレイを prepare する。
    /// クライアントは units を送らず、消費数は PF 側が決定する。
    /// assetCode は賭け対象の資産コード(通常ビルドはMEDAL固定、JEM_BUILDは選択した宝石)。
    /// rate は倍率(1/2/5/10/20/50/100のいずれか。通常ビルドは常に既定の1)。
    /// PF契約: 省略=1、許可値外は400、消費数=units×rate。1でも明示送信して問題ない契約のため
    /// 分岐は設けず常に送る(jem-poinoshin PlatformApiClient.cs と同じ型)。
    /// </summary>
    public async Task<PlatformRun> Prepare(string assetCode = "MEDAL", int rate = 1)
    {
        if (string.IsNullOrEmpty(PlatformSession.Cookie))
            Debug.LogWarning("[PlatformApiClient] PlatformSession.Cookie is empty; launch may fail without credentials");

        string launchCode = await Launch();
        string playToken = await Token(launchCode);
        var prepared = await Plays(playToken, assetCode, rate);

        return new PlatformRun(prepared.run_id, playToken, prepared.bet, prepared.expires_at);
    }

    /// <summary>ゲーム Lambda の s2s_commit を呼び、PF /commit への署名をサーバーに依頼する。</summary>
    public async Task<S2sCommitResponse> S2sCommit(string token, string runId, string playToken)
    {
        if (string.IsNullOrEmpty(token)) throw new ArgumentException("token is required", nameof(token));
        if (string.IsNullOrEmpty(runId)) throw new ArgumentException("runId is required", nameof(runId));
        if (string.IsNullOrEmpty(playToken)) throw new ArgumentException("playToken is required", nameof(playToken));

        string body = "{\"action\":\"s2s_commit\",\"token\":\"" + EscapeJsonString(token)
            + "\",\"run_id\":\"" + EscapeJsonString(runId)
            + "\",\"play_token\":\"" + EscapeJsonString(playToken) + "\"}";

        var (statusCode, text) = await SendRaw(body);
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"s2s_commit failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<S2sCommitResponse>(text);
        if (parsed == null || !parsed.ok)
            throw new Exception("s2s_commit response malformed");
        return parsed;
    }

    /// <summary>PF /resolve を呼び、1 プレイを精算する。</summary>
    public async Task<PlatformResolveResponse> Resolve(string runId, string playToken)
    {
        var (statusCode, text) = await PlatformPost($"/api/v1/games/{GameId}/plays/{runId}/resolve", "{}", playToken);
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"resolve failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<PlatformResolveResponse>(text);
        if (parsed == null || !parsed.ok || string.IsNullOrEmpty(parsed.run_id))
            throw new Exception("resolve response malformed");
        return parsed;
    }

    public async Task<PlatformAbortResponse> Abort(string runId, string playToken)
    {
        if (string.IsNullOrEmpty(runId)) throw new ArgumentException("runId is required", nameof(runId));
        if (string.IsNullOrEmpty(playToken)) throw new ArgumentException("playToken is required", nameof(playToken));

        var (statusCode, text) = await PlatformPost($"/api/v1/games/{GameId}/plays/{runId}/abort", "{}", playToken);
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"abort failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<PlatformAbortResponse>(text);
        if (parsed == null)
            throw new Exception("abort response malformed");
        return parsed;
    }

    /// <summary>
    /// PF /missions/current から現在の1口消費メダル数(cost_medal)を取得する。
    /// 2026-09-15 是正: SPIN前のBETボタン表示が常定100に固定されるバグへの対応。
    /// run生成・メダル予約・トークン消費のいずれも発生しない読み取り専用GET
    /// （ポータルのセッションcookieだけで完結し、Launch/Token/Playsの完了は不要）。
    /// 取得できない場合(未ログイン・全ミッション踏破後でmission==null・通信エラー等)は
    /// null を返す。呼び出し側は既存のmissionBetを維持し、100へ戻さないこと。
    /// </summary>
    public async Task<int?> GetCurrentMissionCostMedal()
    {
        var (statusCode, text) = await PlatformGet("/api/v1/missions/current");
        if (statusCode < 200 || statusCode >= 300) return null;

        var parsed = JsonUtility.FromJson<PlatformMissionCurrentResponse>(text);
        if (parsed == null || !parsed.ok || parsed.mission == null) return null;
        if (parsed.mission.cost_medal <= 0) return null;
        return parsed.mission.cost_medal;
    }

    /// <summary>PF /wallet/balance から MEDAL 残高を取得する。</summary>
    public async Task<int> GetWalletBalance()
    {
        var (statusCode, text) = await PlatformGet("/api/v1/wallet/balance");
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"wallet/balance failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<PlatformWalletBalanceResponse>(text);
        if (parsed == null || parsed.assets == null) return 0;

        foreach (var asset in parsed.assets)
        {
            if (asset != null && asset.asset_code == "MEDAL")
                return asset.available_units;
        }
        return 0;
    }

    // ---------- 低レベル PF 呼び出し ----------

    private async Task<string> Launch()
    {
        var (statusCode, text) = await PlatformPost($"/api/v1/games/{GameId}/launch", "{}");
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"launch failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<PlatformLaunchResponse>(text);
        if (parsed == null || !parsed.ok || string.IsNullOrEmpty(parsed.launch_code))
            throw new Exception("launch response malformed");
        return parsed.launch_code;
    }

    private async Task<string> Token(string launchCode)
    {
        string body = "{\"launch_code\":\"" + EscapeJsonString(launchCode) + "\"}";
        var (statusCode, text) = await PlatformPost($"/api/v1/games/{GameId}/token", body);
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"token failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<PlatformTokenResponse>(text);
        if (parsed == null || !parsed.ok || string.IsNullOrEmpty(parsed.play_token))
            throw new Exception("token response malformed");
        return parsed.play_token;
    }

    private async Task<PlatformPlaysResponse> Plays(string playToken, string assetCode, int rate)
    {
        string body = "{\"asset_code\":\"" + EscapeJsonString(assetCode) + "\",\"rate\":" + rate + "}";
        var (statusCode, text) = await PlatformPost($"/api/v1/games/{GameId}/plays", body, playToken);
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"plays failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<PlatformPlaysResponse>(text);
        if (parsed == null || !parsed.ok || string.IsNullOrEmpty(parsed.run_id))
            throw new Exception("plays response malformed");
        return parsed;
    }

    private Task<(long statusCode, string text)> PlatformGet(string path)
    {
        return SendToPlatform(path, null, null);
    }

    private Task<(long statusCode, string text)> PlatformPost(string path, string jsonBody, string playToken = null)
    {
        return SendToPlatform(path, jsonBody, playToken);
    }

    private Task<(long statusCode, string text)> SendToPlatform(string path, string jsonBody, string playToken)
    {
        var tcs = new TaskCompletionSource<(long, string)>();
        string url = PlatformBaseUrl + path;

        UnityWebRequest req;
        if (string.IsNullOrEmpty(jsonBody))
        {
            req = UnityWebRequest.Get(url);
        }
        else
        {
            req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            byte[] body = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
        }

        if (!string.IsNullOrEmpty(PlatformSession.Cookie))
            req.SetRequestHeader("Cookie", PlatformSession.Cookie);
        if (!string.IsNullOrEmpty(playToken))
            req.SetRequestHeader("Authorization", "Bearer " + playToken);

        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = TimeoutSeconds;

        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
#if UNITY_2020_2_OR_NEWER
                bool networkOrProtocolError = req.result == UnityWebRequest.Result.ConnectionError
                                            || req.result == UnityWebRequest.Result.DataProcessingError;
#else
                bool networkOrProtocolError = req.isNetworkError;
#endif
                if (networkOrProtocolError)
                {
                    tcs.SetException(new Exception($"Network error: {req.error}"));
                    return;
                }
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                tcs.SetResult((req.responseCode, text));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                req.Dispose();
            }
        };
        return tcs.Task;
    }

#if JEM_BUILD
    // JEM_BUILD: 宝石選択UI向けに GEM_ 系資産の残高を asset_code キーの辞書で返す。
    // 表示順・種類・アイコンは共通catalog(shared/jem-selector)が正本のため、
    // ゲーム側は固定配列を持たない（2026-09-21 社長指示・共通化）。
    public async Task<Dictionary<string, int>> GetGemBalances()
    {
        var (statusCode, text) = await PlatformGet("/api/v1/wallet/balance");
        if (statusCode < 200 || statusCode >= 300)
            throw new HttpStatusException(statusCode, $"wallet/balance failed: HTTP {statusCode}");

        var parsed = JsonUtility.FromJson<PlatformWalletBalanceResponse>(text);
        if (parsed == null || parsed.assets == null) throw new Exception("wallet/balance response malformed");

        var result = new Dictionary<string, int>();
        foreach (var asset in parsed.assets)
        {
            if (asset == null || string.IsNullOrEmpty(asset.asset_code)) continue;
            if (!asset.asset_code.StartsWith("GEM_")) continue;
            result[asset.asset_code] = asset.available_units;
        }
        return result;
    }
#endif

    /// <summary>既存 ゲーム Lambda への生 POST。</summary>
    private async Task<(long statusCode, string text)> SendRaw(string jsonBody)
    {
#if JEM_BUILD
        // fail-closed: JEM専用Lambdaが未作成(=endpoint空)の間は通信しない。
        // 元warukyure-apiへ誤って流すとplay_token検証401→abort返還や誤課金に繋がるため。
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("JEM_BUILD: jem-warukyure-api endpoint is not configured");
#endif
        var tcs = new TaskCompletionSource<(long, string)>();
        UnityWebRequest req = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = TimeoutSeconds;

        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
#if UNITY_2020_2_OR_NEWER
                bool networkOrProtocolError = req.result == UnityWebRequest.Result.ConnectionError
                                            || req.result == UnityWebRequest.Result.DataProcessingError;
#else
                bool networkOrProtocolError = req.isNetworkError;
#endif
                if (networkOrProtocolError)
                {
                    tcs.SetException(new Exception($"Network error: {req.error}"));
                    return;
                }
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                tcs.SetResult((req.responseCode, text));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                req.Dispose();
            }
        };
        return await tcs.Task;
    }

    private static string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PoiPlatformCredentialsNoop();

    [DllImport("__Internal")]
    private static extern string PoiGetPlatformBaseUrl();
#else
    private static void PoiPlatformCredentialsNoop() { }
    private static string PoiGetPlatformBaseUrl() => null;
#endif
}
