mergeInto(LibraryManager.library, {
  PoiErrShow: function (codePtr, runIdPtr, refunded) {
    if (typeof window === 'undefined') return;
    var code = UTF8ToString(codePtr);
    var runId = UTF8ToString(runIdPtr);
    if (typeof window.PoiErr === 'object' && typeof window.PoiErr.show === 'function') {
      window.PoiErr.show(code, runId, refunded !== 0);
    } else {
      if (typeof console !== 'undefined') {
        console.log('[PoiErrShow] PoiErr not loaded. code=' + code + ' runId=' + runId);
      }
    }
  },

  PoiErrHide: function () {
    if (typeof window === 'undefined') return;
    if (typeof window.PoiErr === 'object' && typeof window.PoiErr.hide === 'function') {
      window.PoiErr.hide();
    }
  },

  // 2026-09-14 社長指摘「遷移先をゲーム一覧＝ポータルにしてくれ」への対応。
  // poierr v3の「戻る」でタイトルへ戻すループを止め、ポータルへ遷移する。
  // window.API_PREFIX は index.html の poiDeriveApiPrefix() が本番'/portal'・DEV'/portal-dev'
  // を返す既存のグローバル(API呼び出し用)。同じ規則でページのプレフィクスにもなるため流用する。
  // 参照実装: ~/Unity/Kurohige の KurohigePlatform.jslib PoiErrBackToPortal。
  PoiErrBackToPortal: function () {
    if (typeof window === 'undefined' || !window.location) return;
    var prefix = window.API_PREFIX || '/portal';
    window.location.href = window.location.origin + prefix + '/';
  }
});
