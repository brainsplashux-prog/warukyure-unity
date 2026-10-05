// PoiAudioHardStop — poi-audio-guard v4 の Unity 側停止口（正本 ~/.claude/templates/game-defaults/PoiAudioHardStop.jslib・出典 ~/Unity/nyaruma 69a6180 から nyaruma 専用のデバッグ関数だけ除去）。
// 置き場所: Assets/Plugins/WebGL/PoiAudioHardStop.jslib。C# 側は同ディレクトリの PoiAudioHardStop.cs を入れれば自動で初期化される。
// index.html の guard が visibilitychange/pagehide/freeze/blur/1秒ポーリングの
// どの経路からでも window.__poiUnityAudio.stop() を「同期で」呼べるようにする。
// iOS は visibilitychange 直後に JS を凍結するため onstatechange の非同期処理に
// 依存できない。ここで framework.js の onstatechange suspended 分岐と同じ処理を
// 同期で実行し、既に鳴っている全音源をその場で pause 状態へ倒す。
//
// framework.js（Unity 6000.4.7f1・本番展開物）の実構造:
//   var WEBAudio={audioInstances:{}, audioContext:null, audioWebEnabled:0,
//     audioContextSuspendedTime:0, audioContextResumeOffset:0, contextIsRunning:false,
//     soundsPendingContextResume:[], ...};
//   suspended 分岐:
//     WEBAudio.contextIsRunning=false;
//     WEBAudio.audioContextSuspendedTime=_GetFakemodTimeInSeconds();
//     Object.values(WEBAudio.audioInstances).forEach(audioInstance=>{
//       if(audioInstance.source!=null){if(!audioInstance.isPaused()){
//         audioInstance.pause();
//         WEBAudio.soundsPendingContextResume.push({channel:audioInstance,
//           clip:null,startTime:null,stopDelay:-1,
//           offset:audioInstance.source.playbackPausedAtPosition})}}})
mergeInto(LibraryManager.library, {
  PoiAudioHardStopInit: function () {
    try {
      if (typeof window === 'undefined') return;
      if (window.__poiUnityAudio) return;

      // framework と同じ時計（_GetFakemodTimeInSeconds）。無ければ currentTime で代替。
      function nowSec() {
        try { if (typeof _GetFakemodTimeInSeconds === 'function') return _GetFakemodTimeInSeconds(); } catch (e) {}
        try {
          if (typeof WEBAudio !== 'undefined' && WEBAudio && WEBAudio.audioContext)
            return WEBAudio.audioContext.currentTime;
        } catch (e) {}
        return 0;
      }

      var api = { _stopped: false, _attached: false };

      // state が running に戻ったら「停止中」マーカーを解除する。
      // （suspendedTime は停止エピソードの最初の時刻だけを保持するための判別子）
      function attach() {
        try {
          if (api._attached) return;
          if (typeof WEBAudio === 'undefined' || !WEBAudio || !WEBAudio.audioContext) return;
          api._attached = true;
          WEBAudio.audioContext.addEventListener('statechange', function () {
            try {
              if (WEBAudio.audioContext && WEBAudio.audioContext.state === 'running')
                api._stopped = false;
            } catch (e) {}
          });
        } catch (e) {}
      }

      // framework の onstatechange suspended 分岐と同じ処理を同期実行する。
      // WEBAudio 未初期化（undefined / audioContext 無し）の時は何もしない（例外を出さない）。
      api.stop = function () {
        try {
          if (typeof WEBAudio === 'undefined' || !WEBAudio || !WEBAudio.audioContext) return;
          attach();
          WEBAudio.contextIsRunning = false;
          // 「最初に停止した時刻」だけを記録。停止中の再 stop では上書きしない。
          if (!api._stopped) {
            api._stopped = true;
            WEBAudio.audioContextSuspendedTime = nowSec();
          }
          var inst = WEBAudio.audioInstances;
          if (!inst) return;
          Object.keys(inst).forEach(function (k) {
            var a = inst[k];
            try {
              if (!a || a.source == null) return;
              if (a.isPaused()) return;
              a.pause();
              // 再開キューへ積む。重複判定は channel 単位（既に積まれていれば積まない）。
              var pending = WEBAudio.soundsPendingContextResume;
              var dup = false;
              if (pending) {
                for (var i = 0; i < pending.length; i++) {
                  if (pending[i] && pending[i].channel === a) { dup = true; break; }
                }
              }
              if (!dup && pending) {
                pending.push({
                  channel: a, clip: null, startTime: null, stopDelay: -1,
                  offset: a.source ? a.source.playbackPausedAtPosition : 0
                });
              }
            } catch (e) {}
          });
        } catch (e) {}
      };

      // 観測口（読取のみ・本番に残してよい）:
      // 再生中インスタンス数・paused 数・ctx.state・currentTime を返す。
      api.debugState = function () {
        try {
          if (typeof WEBAudio === 'undefined' || !WEBAudio) return { ready: false };
          var inst = WEBAudio.audioInstances || {};
          var keys = Object.keys(inst);
          var playing = 0, paused = 0;
          for (var i = 0; i < keys.length; i++) {
            try {
              var a = inst[keys[i]];
              if (!a || a.source == null) continue;
              if (a.isPaused()) paused++; else playing++;
            } catch (e) {}
          }
          var c = WEBAudio.audioContext;
          return {
            ready: true,
            playing: playing,
            paused: paused,
            ctxState: c ? c.state : 'none',
            currentTime: c ? c.currentTime : -1,
            pendingResume: WEBAudio.soundsPendingContextResume
              ? WEBAudio.soundsPendingContextResume.length : 0,
            contextIsRunning: !!WEBAudio.contextIsRunning,
            suspendedTime: WEBAudio.audioContextSuspendedTime || 0
          };
        } catch (e) { return { ready: false, error: String(e) }; }
      };

      window.__poiUnityAudio = api;
    } catch (e) {}
  }
  // 注: PoiTryResumeAudio は入れない（PicopicoChance 等が自前 jslib で同名を定義済み＝重複シンボルでビルドが壊れる）。
});
