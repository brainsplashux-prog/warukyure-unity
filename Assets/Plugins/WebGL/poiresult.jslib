mergeInto(LibraryManager.library, {
  // Calls the shared poiresult/v2 kit (loaded externally from lp.poicasi.co.jp).
  // payout drives the tiers (>=1000 MEGA / >0 BIG / 0 LOSE) inside the kit.
  // reward = メダル以外の報酬名（例: くまボール）。payout が 0 でも報酬があれば当たり扱いになる。
  // 当落を宣言するのはキット側だけで、ゲームはサーバが返した事実（枚数・報酬名）しか渡さない。
  // onDoneMethod is the C# method on gameObjectName, called when the screen closes.
  PoiResultShow: function (payout, detailPtr, rewardPtr, gameObjectNamePtr, onDoneMethodPtr) {
    var detail = (detailPtr && UTF8ToString(detailPtr)) || '';
    var reward = (rewardPtr && UTF8ToString(rewardPtr)) || '';
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var onDoneMethod = UTF8ToString(onDoneMethodPtr);

    var done = function () {
      if (Module && typeof Module.SendMessage === 'function') {
        Module.SendMessage(gameObjectName, onDoneMethod, "");
      }
    };

    if (!window.PoiResult || typeof window.PoiResult.show !== 'function') { done(); return; }
    window.PoiResult.show({ payout: payout, reward: reward, detailHtml: detail, onClose: done });
  },

  PoiResultClose: function () {
    if (window.PoiResult && typeof window.PoiResult.close === 'function') window.PoiResult.close();
  }
});
