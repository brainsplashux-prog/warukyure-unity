mergeInto(LibraryManager.library, {
  PoiSetSurface: function (nonGame) {
    try {
      if (typeof window === 'undefined') return;
      var surface = nonGame ? 'non-game' : 'play';
      if (typeof window.poiWaruqSetSurface === 'function') {
        window.poiWaruqSetSurface(surface);
      } else {
        window.__poiWaruqSurface = surface;
      }
    } catch (e) {
      if (typeof console !== 'undefined') {
        console.warn('[PoiSetSurface] failed', e);
      }
    }
  }
});
