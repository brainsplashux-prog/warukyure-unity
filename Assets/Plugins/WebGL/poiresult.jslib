mergeInto(LibraryManager.library, {
  // Calls the shared poiresult/v1 kit (loaded externally from lp.poicasi.co.jp).
  // payout drives the 3 tiers (>=1000 MEGA / >0 BIG / 0 LOSE) inside the kit.
  // onDoneMethod is the C# method on gameObjectName, called when the screen closes.
  PoiResultShow: function (payout, detailPtr, gameObjectNamePtr, onDoneMethodPtr) {
    var detail = (detailPtr && UTF8ToString(detailPtr)) || '';
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var onDoneMethod = UTF8ToString(onDoneMethodPtr);

    var done = function () {
      if (Module && typeof Module.SendMessage === 'function') {
        Module.SendMessage(gameObjectName, onDoneMethod, "");
      }
    };

    if (!window.PoiResult || typeof window.PoiResult.show !== 'function') { done(); return; }
    window.PoiResult.show({ payout: payout, detailHtml: detail, onClose: done });
  },

  PoiResultClose: function () {
    if (window.PoiResult && typeof window.PoiResult.close === 'function') window.PoiResult.close();
  }
});
