/*
 * PoicasiChromeBridge.jslib — 正本
 * 2026-09-17 ヘッダー中央切替 Phase2（社長指示: 全ゲーム共通の setScreen 配線）。
 *
 * 契約: window.PoicasiChrome.setScreen("play"|"menu")
 *   - poicasi-chrome.js 本体が読み込み完了する前に呼ばれた場合は、loader.js が置く
 *     window.__poicasiChromeQueue へ [methodName, argsArray] の形で積む必要がある
 *     （本体側 poicasi-chrome.js: publicApi[queuedMethod].apply(publicApi, queued[1] || [])
 *     で再生するため、argsArray は必ず配列でラップする＝["setScreen", [screen]]）。
 *   - 本体が既に window.PoicasiChrome.setScreen として存在する場合はそちらを直接呼ぶ。
 * 例外は握りつぶす（Unity側の描画を止めない）。
 *
 * 全Unityゲームへ同一内容をコピーして使う。中身を書き換える時はこのファイルを直し、
 * 各ゲームへ再コピーする（ゲームごとに独自実装を作らない）。
 */
mergeInto(LibraryManager.library, {
  PoicasiChrome_SetScreen: function (screenPtr) {
    try {
      var screen = UTF8ToString(screenPtr);
      if (window.PoicasiChrome && typeof window.PoicasiChrome.setScreen === "function") {
        window.PoicasiChrome.setScreen(screen);
      } else {
        window.__poicasiChromeQueue = window.__poicasiChromeQueue || [];
        window.__poicasiChromeQueue.push(["setScreen", [screen]]);
      }
    } catch (e) {
      /* Unity側の描画を止めない */
    }
  }
});
