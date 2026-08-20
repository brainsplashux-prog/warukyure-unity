using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Ad_Virtua.Runtime
{
    /// <summary>
    /// Ad-Virtua デバイス情報管理を担当するヘルパークラス
    /// ADID/IDFA取得、UUID生成、セッションID管理を統合
    /// </summary>
    public static class DeviceInfoHelper
    {
        #region キャッシュ管理

        // ADID/IDFA情報のキャッシュ
        private static string _cachedAdId = null;
        private static string _cachedIsAdLimited = null;
        private static bool _isFetching = false; // リクエスト中かを示すフラグ
        private static bool _hasCompleted = false; // すでに取得済みか

        // セッションID（UUID）のキャッシュ
        private static string _uniqueId = null;

        #endregion

        #region 初期化

        /// <summary>
        /// 静的コンストラクタ - セッションIDを生成
        /// </summary>
        static DeviceInfoHelper()
        {
            GenerateNewSessionId();
        }

        #endregion

        #region ADID/IDFA統合取得

        /// <summary>
        /// キャッシュされたADID/IDFAを取得する
        /// プラットフォームに応じてAndroid ADIDまたはiOS IDFAを取得
        /// </summary>
        /// <param name="callback">取得完了時のコールバック（adId, isAdLimited）</param>
        /// <returns>コルーチン</returns>
        public static IEnumerator GetCachedIFA(Action<string, string> callback)
        {
            // すでに取得済みならキャッシュを返す
            if (_hasCompleted)
            {
                callback(_cachedAdId, _cachedIsAdLimited);
                yield break;
            }

            // 他のリクエストが実行中の場合は待機
            while (_isFetching)
            {
                yield return null;
            }

            // 初めての取得の場合
            if (!_hasCompleted)
            {
                _isFetching = true;

                // プラットフォーム別のADID/IDFA取得
                switch (Application.platform)
                {
                    case RuntimePlatform.Android:
                        yield return GetAdIdFromAndroid();
                        break;
                    case RuntimePlatform.IPhonePlayer:
                        yield return GetIdfaFromIOS();
                        break;
                    default:
                        Debug.LogWarning($"[Ad-Virtua] ADID/IDFA is not supported on {Application.platform}.");
                        _cachedAdId = "";
                        _cachedIsAdLimited = "";
                        break;
                }

                _hasCompleted = true;
                _isFetching = false;
            }

            callback(_cachedAdId, _cachedIsAdLimited);
        }

        /// <summary>
        /// タイムアウト付きでADID/IDFAを取得する
        /// </summary>
        /// <param name="timeout">タイムアウト時間（秒）</param>
        /// <param name="callback">取得完了時のコールバック</param>
        /// <returns>コルーチン</returns>
        public static IEnumerator GetDeviceInfoWithTimeout(MonoBehaviour caller, float timeout, Action<string, string> callback)
        {
            bool isCompleted = false;
            string resultAdId = "";
            string resultIsAdLimited = "";

            // ADID/IDFA取得を開始
            caller.StartCoroutine(GetCachedIFA((adId, isAdLimited) =>
            {
                if (!isCompleted)
                {
                    isCompleted = true;
                    resultAdId = adId;
                    resultIsAdLimited = isAdLimited;
                }
            }));

            // タイムアウト監視
            float elapsedTime = 0f;
            while (!isCompleted && elapsedTime < timeout)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // タイムアウトの場合
            if (!isCompleted)
            {
                Debug.LogWarning($"[Ad-Virtua] ADID/IDFA acquisition timed out after {timeout} seconds.");
                resultAdId = "";
                resultIsAdLimited = "";
            }

            callback(resultAdId, resultIsAdLimited);
        }

        #endregion

        #region Android ADID取得

        /// <summary>
        /// AndroidのADIDを取得する
        /// </summary>
        /// <returns>コルーチン</returns>
        private static IEnumerator GetAdIdFromAndroid()
        {
            yield return GetAndroidAdvertiserIdCoroutine((adId, trackingEnabled) =>
            {
                _cachedAdId = trackingEnabled ? adId : "";
                _cachedIsAdLimited = trackingEnabled ? "0" : "1";
            });
        }

        /// <summary>
        /// Android Advertiser IDを取得するコルーチン
        /// </summary>
        /// <param name="callback">取得完了時のコールバック</param>
        /// <returns>コルーチン</returns>
        public static IEnumerator GetAndroidAdvertiserIdCoroutine(Action<string, bool> callback)
        {
            yield return GetAdvertisingIdFromAndroid((adInfo) =>
            {
                OnAdInfoReceived(adInfo, callback);
            });
        }

        /// <summary>
        /// AdInfo受信時の処理
        /// </summary>
        /// <param name="adInfo">広告情報</param>
        /// <param name="callback">コールバック</param>
        private static void OnAdInfoReceived(AdInfo adInfo, Action<string, bool> callback)
        {
            if (adInfo != null && !string.IsNullOrEmpty(adInfo.advertisingId))
            {
                callback(adInfo.advertisingId, adInfo.trackingEnabled);
            }
            else
            {
                Debug.LogError($"[Ad-Virtua] Getting Android ADID failed.");
                callback(null, false);
            }
        }

        /// <summary>
        /// AndroidからAdvertising IDを取得する
        /// </summary>
        /// <param name="callback">取得完了時のコールバック</param>
        /// <returns>コルーチン</returns>
        private static IEnumerator GetAdvertisingIdFromAndroid(Action<AdInfo> callback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AdInfo adInfo = null;
            Exception error = null;

            // try-catchブロックをyield returnの外に配置
            try
            {
                // Google Play Servicesを使用した実装
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");

                AndroidJavaClass advertiserIdClient = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
                AndroidJavaObject adInfoTask = advertiserIdClient.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", context);

                if (adInfoTask != null)
                {
                    string advertisingId = adInfoTask.Call<string>("getId");
                    bool isLimitAdTrackingEnabled = adInfoTask.Call<bool>("isLimitAdTrackingEnabled");

                    adInfo = new AdInfo
                    {
                        advertisingId = advertisingId,
                        trackingEnabled = !isLimitAdTrackingEnabled
                    };
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            // エラーハンドリング
            if (error != null)
            {
                Debug.LogError($"[Ad-Virtua] Android ADID acquisition error: {error.Message}");
                callback(null);
            }
            else
            {
                callback(adInfo);
            }

            yield break;
#else
            Debug.LogWarning($"[Ad-Virtua] Android ADID is only available on Android platform.");
            callback(null);
            yield break;
#endif
        }

        #endregion

        #region iOS IDFA取得

        /// <summary>
        /// iOSのIDFAを取得する
        /// </summary>
        /// <returns>コルーチン</returns>
        private static IEnumerator GetIdfaFromIOS()
        {
            yield return GetIDFACoroutine((idfa, trackingEnabled) =>
            {
                _cachedAdId = trackingEnabled ? idfa : "";
                _cachedIsAdLimited = trackingEnabled ? "0" : "1";
            });
        }

        /// <summary>
        /// iOS IDFAを取得するコルーチン
        /// </summary>
        /// <param name="callback">取得完了時のコールバック</param>
        /// <returns>コルーチン</returns>
        public static IEnumerator GetIDFACoroutine(Action<string, bool> callback)
        {
            // iOS以外では処理を行わないようにする
            if (Application.platform != RuntimePlatform.IPhonePlayer)
            {
                Debug.LogWarning($"[Ad-Virtua] IDFA feature is only available on iOS.");
                callback?.Invoke(null, false);
                yield break;
            }

            yield return GetIDFA(adInfo =>
            {
                if (adInfo != null && !string.IsNullOrEmpty(adInfo.advertisingId))
                {
                    callback?.Invoke(adInfo.advertisingId, adInfo.trackingEnabled);
                }
                else
                {
                    Debug.LogError($"[Ad-Virtua] Getting iOS IDFA failed.");
                    callback?.Invoke(null, false);
                }
            });
        }

        /// <summary>
        /// iOS IDFAを取得する
        /// </summary>
        /// <param name="callback">取得完了時のコールバック</param>
        /// <returns>コルーチン</returns>
        private static IEnumerator GetIDFA(Action<AdInfo> callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            AdInfo resultAdInfo = null;
            Exception error = null;

            // try-catchブロックをyield returnの外に配置
            try
            {
                // iOS App Tracking Transparencyを使用した実装
                // リフレクションを使用してUnityAdsのIDFA取得を試行
                Assembly unityAdsAssembly = null;
                Type advertisementType = null;

                try
                {
                    unityAdsAssembly = Assembly.Load("UnityEngine.Advertisements");
                    advertisementType = unityAdsAssembly?.GetType("UnityEngine.Advertisements.Advertisement");
                }
                catch
                {
                    // UnityAdsが利用できない場合は別の方法を試行
                }

                if (advertisementType != null)
                {
                    var getIdMethod = advertisementType.GetMethod("GetDeviceId", BindingFlags.Static | BindingFlags.Public);
                    var isLimitedMethod = advertisementType.GetMethod("IsLimitAdTrackingEnabled", BindingFlags.Static | BindingFlags.Public);

                    if (getIdMethod != null && isLimitedMethod != null)
                    {
                        string deviceId = (string)getIdMethod.Invoke(null, null);
                        bool isLimited = (bool)isLimitedMethod.Invoke(null, null);

                        resultAdInfo = new AdInfo
                        {
                            advertisingId = deviceId,
                            trackingEnabled = !isLimited
                        };
                    }
                }

                // フォールバック：システム情報を使用
                if (resultAdInfo == null)
                {
                    string fallbackId = SystemInfo.deviceUniqueIdentifier;
                    resultAdInfo = new AdInfo
                    {
                        advertisingId = fallbackId,
                        trackingEnabled = false // デバイスIDの場合は制限ありとして扱う
                    };
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            // エラーハンドリング
            if (error != null)
            {
                Debug.LogError($"[Ad-Virtua] iOS IDFA acquisition error: {error.Message}");
                callback(null);
            }
            else
            {
                callback(resultAdInfo);
            }

            yield break;
#else
            Debug.LogWarning($"[Ad-Virtua] iOS IDFA is only available on iOS platform.");
            callback(null);
            yield break;
#endif
        }

        #endregion

        #region UUID/セッションID管理

        /// <summary>
        /// 新しいセッションIDを生成する
        /// </summary>
        private static void GenerateNewSessionId()
        {
            _uniqueId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// セッションIDを取得する
        /// </summary>
        /// <returns>セッションID</returns>
        public static string GetSessionId()
        {
            if (string.IsNullOrEmpty(_uniqueId))
            {
                GenerateNewSessionId();
            }
            return _uniqueId;
        }

        /// <summary>
        /// 新しいUUIDを生成する（汎用）
        /// </summary>
        /// <returns>新しいUUID</returns>
        public static string GenerateUUID()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// セッションIDをリセットする（テスト用）
        /// </summary>
        public static void ResetSessionId()
        {
            GenerateNewSessionId();
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// 広告IDをマスクして表示用文字列を返す（先頭4文字のみ表示）
        /// </summary>
        /// <param name="adId">広告ID</param>
        /// <returns>マスクされた広告ID</returns>
        public static string MaskAdId(string adId)
        {
            if (string.IsNullOrEmpty(adId) || adId.Length <= 4) return adId;
            return adId.Substring(0, 4) + new string('*', adId.Length - 4);
        }

        #endregion

        #region キャッシュクリア

        /// <summary>
        /// ADID/IDFAキャッシュをクリアする（テスト用）
        /// </summary>
        public static void ClearDeviceInfoCache()
        {
            _cachedAdId = null;
            _cachedIsAdLimited = null;
            _isFetching = false;
            _hasCompleted = false;
            Debug.Log($"[Ad-Virtua] Device info cache cleared.");
        }

        /// <summary>
        /// 全てのキャッシュをクリアする（テスト用）
        /// </summary>
        public static void ClearAllCache()
        {
            ClearDeviceInfoCache();
            _uniqueId = null;
            Debug.Log($"[Ad-Virtua] All device info cache cleared.");
        }

        #endregion
    }
}