mergeInto(LibraryManager.library, {
  // poicasi-auth の認可コードを URL hash から 1 回だけ受け取る。
  // 値はページ側（index.html）が window.__wkPaCode に置き、ここで取り出して消す。
  WkTakePaCode: function () {
    var code = "";
    try {
      if (window.__wkPaCode) {
        code = String(window.__wkPaCode);
        window.__wkPaCode = undefined;
        try { sessionStorage.removeItem("wk_pa_code"); } catch (e) {}
      }
    } catch (e) {}

    if (!code) {
      return 0;
    }

    var length = lengthBytesUTF8(code) + 1;
    var buffer = _malloc(length);
    stringToUTF8(code, buffer, length);
    return buffer;
  },

  WkFreePaCode: function (ptr) {
    if (ptr) {
      _free(ptr);
    }
  },

  // poicasi-auth ブローカーへ遷移する（ログイン開始）。
  WkNavigate: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (url) {
      window.location.href = url;
    }
  }
});
