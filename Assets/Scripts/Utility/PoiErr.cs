using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class PoiErr
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PoiErrShow(string code, string runId, int refunded);

    [DllImport("__Internal")]
    private static extern void PoiErrHide();
#endif

    public static Action OnRetry { get; set; }
    public static Action OnBack { get; set; }

    public static void Show(string code, string runId, bool refunded, Action onRetry = null, Action onBack = null)
    {
        OnRetry = onRetry;
        OnBack = onBack;
        EnsureManager();

#if UNITY_WEBGL && !UNITY_EDITOR
        PoiErrShow(code ?? "E-0000", runId ?? string.Empty, refunded ? 1 : 0);
#else
        Debug.Log($"[PoiErr] show: code={code} runId={runId} refunded={refunded}");
#endif
    }

    public static void Hide()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PoiErrHide();
#else
        Debug.Log("[PoiErr] hide");
#endif
    }

    private static void EnsureManager()
    {
        var go = GameObject.Find("PoiErrManager");
        if (go == null)
        {
            go = new GameObject("PoiErrManager");
            go.AddComponent<PoiErrManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        else if (go.GetComponent<PoiErrManager>() == null)
        {
            go.AddComponent<PoiErrManager>();
        }
    }
}
