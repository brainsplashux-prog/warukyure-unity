// Warukyure WebGL から poicasi-platform エンドポイントへリクエストを送る際、
// UnityWebRequest の C# 側からは withCredentials を設定できないため、
// fetch() / XMLHttpRequest をパッチして同一生成元 cookie を送る。
mergeInto(LibraryManager.library, {
  // C# 側がこの関数を呼ぶだけで jslib がリンクされる。
  PoiPlatformCredentialsNoop: function () { },

  // WebGL ローダーが設定した同一生成元 platform base URL を返す。
  PoiGetPlatformBaseUrl: function () {
    var prefix = (typeof window !== 'undefined' && typeof window.API_PREFIX === 'string') ? window.API_PREFIX : '';
    var s = (typeof window !== 'undefined' && typeof window.location !== 'undefined') ? (window.location.origin + prefix) : '';
    var len = lengthBytesUTF8(s) + 1;
    var buf = _malloc(len);
    stringToUTF8(s, buf, len);
    return buf;
  },
});

(function () {
  if (typeof window === 'undefined') return;

  function getPlatformBaseUrl() {
    var prefix = (typeof window.API_PREFIX === 'string') ? window.API_PREFIX : '';
    return window.location.origin + prefix;
  }

  function isPlatformUrl(url) {
    return typeof url === 'string' && url.indexOf(getPlatformBaseUrl()) === 0;
  }

  if (typeof window.fetch === 'function') {
    var origFetch = window.fetch;
    window.fetch = function (input, init) {
      var url = (typeof input === 'string') ? input : (input && input.url);
      if (isPlatformUrl(url)) {
        init = init || {};
        if (init.credentials !== 'include') {
          var patched = {};
          for (var k in init) {
            if (Object.prototype.hasOwnProperty.call(init, k)) patched[k] = init[k];
          }
          patched.credentials = 'include';
          init = patched;
        }
      }
      return origFetch(input, init);
    };
  }

  if (typeof XMLHttpRequest !== 'undefined') {
    var origOpen = XMLHttpRequest.prototype.open;
    XMLHttpRequest.prototype.open = function (method, url, async, user, password) {
      if (isPlatformUrl(url)) {
        this.withCredentials = true;
      }
      return origOpen.apply(this, arguments);
    };
  }
})();
