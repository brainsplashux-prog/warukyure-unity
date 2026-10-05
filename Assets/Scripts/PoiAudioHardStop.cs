// PoiAudioHardStop.cs — poi-audio-guard v4 の Unity 側（正本 ~/.claude/templates/game-defaults/PoiAudioHardStop.cs）。
// 置き場所: Assets/Scripts/PoiAudioHardStop.cs（Assets/Plugins/WebGL/PoiAudioHardStop.jslib と対で入れる）。
// シーン配置・他スクリプトの変更は不要。最初のシーン読込より前に jslib の PoiAudioHardStopInit() を1回呼び、
// window.__poiUnityAudio.stop を公開する（guard が離脱時に同期で呼んで Unity 音源を実停止する）。
// AudioListener.pause / volume には触れない（2026-10-01 CODEXレビュー: ゲーム側のミュート/一時停止と所有権が競合し、
// フォーカス復帰イベントが来ない環境で永久に無音になる危険があるため。停止は jslib＋guard だけで行う）。
// 既に自前で PoiAudioHardStopInit を呼んでいるゲーム（nyaruma）には入れない。
using UnityEngine;

public static class PoiAudioHardStop
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void PoiAudioHardStopInit();
#else
    static void PoiAudioHardStopInit() { }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        try { PoiAudioHardStopInit(); }
        catch (System.Exception e) { Debug.LogWarning("[poi] PoiAudioHardStopInit failed: " + e.Message); }
    }
}
