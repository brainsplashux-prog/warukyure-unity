// PoicasiChromeBridge.cs — 正本
// 2026-09-17 ヘッダー中央切替 Phase2（社長指示: 全ゲーム共通の setScreen 配線）。
//
// 対になる JS 側の正本: web/shared/poicasi-chrome/unity/PoicasiChromeBridge.jslib
// 全Unityゲームへ同一内容をコピーして使う（Assets/Plugins/WebGL/ 配下など、
// 各リポジトリの WebGL プラグイン配置ルールに合わせる）。
//
// 使い方: 画面が切り替わる1か所（画面状態を管理しているクラス）から
//   PoicasiChromeBridge.SetScreen(true)  // プレイ中 → ヘッダーAのみ
//   PoicasiChromeBridge.SetScreen(false) // タイトル/選択/リザルト等 → ヘッダーA+B
// を呼ぶ。既存の自前ヘッダーB描画コンポーネント（MissionHeaderUI 等）は
// シーン上で無効化し、呼び出しはこちらへ一本化する（ファイルは削除しない）。

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Poicasi.Chrome
{
    public static class PoicasiChromeBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PoicasiChrome_SetScreen(string screen);
#endif

        /// <summary>
        /// isPlay=true: プレイ画面（ヘッダーAのみ）。
        /// isPlay=false: タイトル・ゲーム選択・リザルト等（ヘッダーA+B）。
        /// Editor実行時・非WebGLビルド時は何もしない（DllNotFoundExceptionを避ける）。
        /// </summary>
        public static void SetScreen(bool isPlay)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                PoicasiChrome_SetScreen(isPlay ? "play" : "menu");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PoicasiChromeBridge] setScreen failed: " + e.Message);
            }
#endif
        }
    }
}
