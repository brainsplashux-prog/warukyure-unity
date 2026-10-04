// PoiPlatformBridge.jslib — poi-game-boot 共通ミュート中継（dec-20261004 フェーズ2）。
// 正本は poicasi-platform リポジトリ web/shared/poi-game-boot/unity/（各ゲームはここから
// Assets/Plugins/WebGL/ へコピーして使う。ゲーム側で改変しない）。
//
// 役割: PoiPlatform.audio（/shared/poi-game-boot/v1/poi-platform.js）と Unity 側を双方向接続する。
//   PoiPlatform_IsMuted()       … 共通ミュート正本 poicasi_audio_muted の現在値（1=ミュート）
//   PoiPlatform_SetMuted(m)     … 共通ミュートへの書き込み（設定画面・他ゲーム・別タブへ伝搬）
//   PoiPlatform_SubscribeAudio  … onChange 購読。変化時に SendMessage(go, method, 0|1) で通知。
// PoiPlatform 不在時は全て何もしない（例外を出さない＝旧来ページ・非移行ページでも安全）。
mergeInto(LibraryManager.library, {
  PoiPlatform_IsMuted: function () {
    try {
      if (typeof window === 'undefined') return 0;
      if (window.PoiPlatform && window.PoiPlatform.audio &&
          typeof window.PoiPlatform.audio.isMuted === 'function') {
        return window.PoiPlatform.audio.isMuted() ? 1 : 0;
      }
    } catch (e) {}
    return 0;
  },

  PoiPlatform_SetMuted: function (muted) {
    try {
      if (typeof window === 'undefined') return;
      if (window.PoiPlatform && window.PoiPlatform.audio &&
          typeof window.PoiPlatform.audio.setMuted === 'function') {
        window.PoiPlatform.audio.setMuted(muted !== 0);
      }
    } catch (e) {}
  },

  PoiPlatform_ToggleMuted: function () {
    try {
      if (typeof window === 'undefined') return 0;
      if (window.PoiPlatform && window.PoiPlatform.audio &&
          typeof window.PoiPlatform.audio.toggle === 'function') {
        return window.PoiPlatform.audio.toggle() ? 1 : 0;
      }
    } catch (e) {}
    return 0;
  },

  // 購読は一度だけ（同名GameObjectへ重複 SendMessage しないよう既登録を記録）。
  PoiPlatform_SubscribeAudio: function (gameObjectNamePtr, methodNamePtr) {
    try {
      if (typeof window === 'undefined') return;
      if (!(window.PoiPlatform && window.PoiPlatform.audio &&
            typeof window.PoiPlatform.audio.onChange === 'function')) return;
      var go = UTF8ToString(gameObjectNamePtr);
      var m = UTF8ToString(methodNamePtr);
      window.__poiPlatformBridgeSubs = window.__poiPlatformBridgeSubs || {};
      var key = go + '::' + m;
      if (window.__poiPlatformBridgeSubs[key]) return;
      window.__poiPlatformBridgeSubs[key] = true;
      window.PoiPlatform.audio.onChange(function (muted) {
        try { SendMessage(go, m, muted ? 1 : 0); } catch (e) {}
      });
    } catch (e) {}
  }
});
