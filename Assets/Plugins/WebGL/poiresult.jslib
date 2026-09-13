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
  },

  // 共通ヘッダーAの残高再取得（結果演出完了後に1回だけ呼ばれる）
  PoiRefreshHeaderBalance: function () {
    if (typeof window !== 'undefined' && typeof window.poiRefreshHeaderBalance === 'function') {
      try { window.poiRefreshHeaderBalance(); } catch (e) {}
    }
  },

  // 2026-09-13 社長指示: リザルト/エラーからタイトルへ戻る時に一度ページをリロードする
  // (PoiStartButton等の状態機械固着対策)。Yabuzame の PoiPlatformBridge.jslib から逐語移植。
  PoiReloadPage: function () {
    if (typeof window !== 'undefined' && window.location && typeof window.location.reload === 'function') {
      window.location.reload();
    }
  },

  WarukyureCampaignResultReady: function (runIdPtr) {
    try {
      var runId = UTF8ToString(runIdPtr);
      if (!runId) return;
      window.dispatchEvent(new CustomEvent('poicasi:campaign-result-ready', {
        detail: { game_id: 'warukyure', run_id: runId }
      }));
    } catch (e) {}
  },

  // poicasi:audio-state。runtimeの初期化が遅れても拾えるよう最新状態をwindowに置き、数回再送する（音量は変えない）。
  PoiCampaignAudioState: function (muted) {
    try {
      window.__poicasiAudioState = { game_id: 'warukyure', muted: !!muted };
      var fire = function () {
        var s = window.__poicasiAudioState;
        if (!s) return;
        try { window.dispatchEvent(new CustomEvent('poicasi:audio-state', { detail: { game_id: s.game_id, muted: s.muted } })); } catch (e) {}
      };
      var timers = window.__poicasiAudioStateTimers || [];
      for (var t = 0; t < timers.length; t++) clearTimeout(timers[t]);
      fire();
      window.__poicasiAudioStateTimers = [500, 2000, 5000, 10000].map(function (ms) { return setTimeout(fire, ms); });
    } catch (e) {}
  }
});
