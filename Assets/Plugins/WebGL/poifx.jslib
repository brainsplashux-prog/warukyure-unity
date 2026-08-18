mergeInto(LibraryManager.library, {
  // Calls the shared poifx/v1 kit (loaded externally from lp.poicasi.co.jp).
  // onDoneMethod is the C# method name on gameObjectName to receive the callback.
  PoiFxJackpot: function (tierPtr, amount, unitPtr, gameObjectNamePtr, onDoneMethodPtr) {
    var tier = UTF8ToString(tierPtr);
    var unit = UTF8ToString(unitPtr);
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var onDoneMethod = UTF8ToString(onDoneMethodPtr);

    if (typeof window.poiFxJackpot !== 'function') return;

    window.poiFxJackpot({
      tier: tier,
      amount: amount,
      unit: unit,
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
