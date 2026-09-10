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
  }
});
