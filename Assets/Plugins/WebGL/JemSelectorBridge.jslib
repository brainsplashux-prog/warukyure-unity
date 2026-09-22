mergeInto(LibraryManager.library, {
  // 共通 jem-selector(/shared/jem-selector(-dev)/v1)とUnityの境界。
  // 実体は index.html 内の window.__jemSel ブリッジ。通常WARUビルドでも
  // この関数群自体は存在するが、呼び出しは JEM_BUILD 側からのみ行われ、
  // ページ側も window.POI_GAME_ID==='jem-warukyure' でない限り mount は
  // 何も読み込まず即returnする（通常ビルドへの挙動変更なし）。
  JemSelectorMount: function () {
    if (window.__jemSel && typeof window.__jemSel.mount === 'function') window.__jemSel.mount();
  },

  // statePtr = {"ready":bool,"savedRate":n,"balancesByAssetCode":{...}} のJSON文字列。
  // 厳密契約は shared/jem-selector/v1/CONTRACT.md。違反は共通側 update() が throw する。
  JemSelectorUpdate: function (statePtr) {
    if (!window.__jemSel || typeof window.__jemSel.update !== 'function') return;
    try { window.__jemSel.update(UTF8ToString(statePtr)); } catch (e) {}
  },

  JemSelectorShow: function () {
    if (window.__jemSel && typeof window.__jemSel.show === 'function') window.__jemSel.show();
  },

  JemSelectorHide: function () {
    if (window.__jemSel && typeof window.__jemSel.hide === 'function') window.__jemSel.hide();
  },

  // 旧契約(出陣ロック解除)の互換API。決定方式の新本体ではロックを持たずno-op。
  // 旧selector本体との過渡期ペアではロック解除として必要（確定後の再武装用）。
  JemSelectorReleaseStart: function () {
    if (window.__jemSel && typeof window.__jemSel.releaseStart === 'function') window.__jemSel.releaseStart();
  },

  // 共通レートパネルが開いているか。原画STARTへのタップ貫通防止に使う。
  JemSelectorIsOpen: function () {
    return (window.__jemSel && typeof window.__jemSel.isOpen === 'function' && window.__jemSel.isOpen()) ? 1 : 0;
  }
});
