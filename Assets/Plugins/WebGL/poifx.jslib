mergeInto(LibraryManager.library, {
  // Calls the shared poifx/v4 kit (loaded externally from lp.poicasi.co.jp).
  // onDoneMethod is the C# method name on gameObjectName to receive the callback.
  // se is an optional absolute URL for the jackpot SE; empty/null uses the kit default.
  PoiFxJackpot: function (tierPtr, amount, unitPtr, gameObjectNamePtr, onDoneMethodPtr, sePtr) {
    var tier = UTF8ToString(tierPtr);
    var unit = UTF8ToString(unitPtr);
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var onDoneMethod = UTF8ToString(onDoneMethodPtr);
    var se = (sePtr && UTF8ToString(sePtr)) || undefined;

    if (typeof window.poiFxJackpot !== 'function') return;

    window.poiFxJackpot({
      tier: tier,
      amount: amount,
      unit: unit,
      se: se || undefined,
      onDone: function () {
        // Use Module.SendMessage, which is the WebGL bridge to C#.
        if (Module && typeof Module.SendMessage === 'function') {
          Module.SendMessage(gameObjectName, onDoneMethod, "");
        }
      }
    });
  },

  PoiFxSkip: function () {
    if (typeof window.poiFxSkip === 'function') {
      window.poiFxSkip();
    }
  }
});
