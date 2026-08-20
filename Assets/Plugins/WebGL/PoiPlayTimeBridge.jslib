mergeInto(LibraryManager.library, {
    PoiPlayTimeGetFloat: function (keyPtr) {
        var key = UTF8ToString(keyPtr);
        var v = window.localStorage.getItem(key);
        return v ? parseFloat(v) : 0.0;
    },
    PoiPlayTimeSetFloat: function (keyPtr, value) {
        var key = UTF8ToString(keyPtr);
        window.localStorage.setItem(key, value.toString());
    },
    PoiPlayTimeGetInt: function (keyPtr) {
        var key = UTF8ToString(keyPtr);
        var v = window.localStorage.getItem(key);
        return v ? parseInt(v, 10) : 0;
    },
    PoiPlayTimeSetInt: function (keyPtr, value) {
        var key = UTF8ToString(keyPtr);
        window.localStorage.setItem(key, value.toString());
    }
});
