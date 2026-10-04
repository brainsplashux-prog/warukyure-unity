using UnityEngine;
using System.Runtime.InteropServices;

// PoiPlatformBridge.cs — poi-game-boot 共通ミュート中継（dec-20261004 フェーズ2）。
// 正本は poicasi-platform リポジトリ web/shared/poi-game-boot/unity/（各ゲームはここから
// Assets/Scripts/ へコピーして使う。ゲーム側で改変しない）。
//
// 同期順序（必須）:
//   1. Awake（BGM・SEを鳴らす前）で PoiPlatform_IsMuted() を取得し AudioListener.volume へ反映
//   2. その後 PoiPlatform_SubscribeAudio で onChange を購読（変更時 OnPoiAudioChanged が呼ばれる）
// これにより ①ミュート状態で再読み込み→初音から無音 ②設定画面トグル→即反映
// ③別タブ変更→storage イベント経由で反映 ④共通スピーカー操作→設定画面に反映、を満たす。
// 旧 PlayerPrefs "wk_mute" 等のゲーム固有ミュートキーは読まない・書かない
// （共通 poicasi_audio_muted が唯一の正。旧値は放置して消さない）。
[DefaultExecutionOrder(-100)]
public class PoiPlatformBridge : MonoBehaviour
{
    void Awake()
    {
        ApplyMute(PoiPlatformIsMuted() != 0);
        PoiPlatformSubscribeAudio(gameObject.name, nameof(OnPoiAudioChanged));
    }

    // jslib の SendMessage から呼ばれる（引数 1=ミュート / 0=音オン）。
    void OnPoiAudioChanged(int muted)
    {
        ApplyMute(muted != 0);
    }

    void ApplyMute(bool mute)
    {
        AudioListener.volume = mute ? 0f : 1f; // BGM/SE 一括制御
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int PoiPlatform_IsMuted();
    [DllImport("__Internal")] static extern void PoiPlatform_SubscribeAudio(string gameObjectName, string methodName);
    static int PoiPlatformIsMuted() { try { return PoiPlatform_IsMuted(); } catch { return 0; } }
    static void PoiPlatformSubscribeAudio(string go, string method) { try { PoiPlatform_SubscribeAudio(go, method); } catch { } }
#else
    static int PoiPlatformIsMuted() { return 0; }
    static void PoiPlatformSubscribeAudio(string go, string method) { }
#endif
}
