# 引き継ぎ: YABU鮫・WARUQ0 ヘッダーB／フッター広告対応（2026-09-13, from poicasi-org cwdの共通ヘッダーフッター対応セッション f24364a9）

## 0. 起動時に必ず守ること
1. 読む: 本MD → `/Users/suzukimasahiro/Unity/warukyure/DECISIONS.md` → `/Users/suzukimasahiro/.claude/manuals/game-layout-standard.md`（v7）→ `~/Desktop/poicasi-org/reports/` の最新 `digest.md`（昨日の全社決定・裁定待ちを把握）。
2. 現在地確認: `git -C ~/Unity/Yabuzame merge-base --is-ancestor 0b8aaf2 HEAD` と `git -C ~/Unity/warukyure merge-base --is-ancestor 62f89de HEAD` が両方成功すること。本番は `curl -fsS https://lp.poicasi.co.jp/game/<yabuzame|warukyure>/index.html | grep -c portal/poi-a8-footer.js` が1以上。
3. **指示があるまで実装／ビルド／デプロイはしない。**
4. Unity操作をするなら Unity MCPは全コールで unity_instance を明示し、破壊操作前に productName を検証する。
5. 同じ確認・質問を社長へ投げる前に DECISIONS.md・memory・`search_session_transcripts` で自己解決を試みる。
6. このMac内のファイル削除は禁止（退避＝`~/.claude/trash-manual/<日付>-<名前>/` へmv）。

## 1. 目的とゴール
社長原文（2026-09-12 13:56, iPhoneスクショ4枚付き）:
> 共通ヘッダーフッター系対応 / YABU鮫・WARUQ0のタイトルとリザルト画面のフッター広告を対応してほしいのと、WARUQ0のタイトル画面がヘッダーBないので追加して

完了条件: (a) YABU鮫のタイトル/リザルトのフッターに「POICASI AD」ではなく実広告(A8) (b) WARUQ0も同じ (c) WARUQ0タイトルにヘッダーB。**3点ともAI側は実装・本番反映・+60分観測まで完了。残りは社長の実機目視のみ。**

## 2. 現在地
**版管理状態**
| | Yabuzame | warukyure |
|---|---|---|
| origin | github.com/brainsplashux-prog/yabuzame | github.com/brainsplashux-prog/warukyure-unity |
| branch | main（origin同期済み） | main（origin同期済み） |
| 本件コミット | `0b8aaf2`（push済） | `043f9f7`/`ed8d18c`/`62f89de`（push済） |
| 以後の他セッションコミット | 7本（HEAD `ad8f977` 09-13 17:28） | 9本（HEAD `19174f0` 09-13 19:48） |
| 未コミット | なし（本件分） | `Assets/Scripts/Utility.meta` 未追跡＝本件前からある。触らない |

**完了**
- YABU鮫 `/Users/suzukimasahiro/Unity/Yabuzame/Assets/WebGLTemplates/Cosmic/index.html`: 古いローカル `TemplateData/poi-a8-footer.js` の読み込みを外し、`poicasi-chrome:ready` でmount後に正本 `/portal/poi-a8-footer.js` を読み込む＋フッター内広告CSS。ローカル旧ファイルは残置（削除しない）。
- WARUQ0: `Assets/Scripts/Core/AdVirtuaMonitorSetup.cs`（Show先頭で non-game／Hideで play を通知）、新規 `Assets/Plugins/WebGL/PoiSurfaceBridge.jslib`(+meta)、`Assets/WebGLTemplates/PoiLoader/index.html`（ステージ上端/下端をヘッダー/フッター変数に追従、初期surface=non-game、正本A8フッター読込）。`Main.unity` はビルドによるID再採番のみを別コミット。
- 本番: warukyure `v62f89de-20260912T1455Z`、yabuzame `v0b8aaf2-20260912T1456Z` ともに REMOTE_VERIFY=PASS。yabuzame はその後他セッションが `ad8f977` で再デプロイ済みだが正本A8フッター読込は維持（09-13 curl確認）。
- 事後観測: `20260913000122-fb5f`(warukyure)／`-14a3`(yabuzame) が +5/+30/+60分すべてpass。**完了時刻＝2026-09-13 01:04:24 JST**。
- CODEXレビュー: 1回目FAIL（A8が10→9件）→反論（ゲオオンラインは09-04品質チェック2周目の指摘5番で「リンク先403」により意図的除去・09-12再測定も403）→2回目PASS。
- 仕様↔実装照合表10行 全PASS（前セッション報告内）。容量 yabuzame +4.1KB／warukyure +2.5KB。

## 3. 残作業（優先順）
1. **社長の実機目視待ち**（AIはブラウザで見た目確認しない＝§1-28）: YABU鮫タイトル/リザルト、WARUQ0タイトル/リザルトの4画面で ①フッターが実A8バナー ②WARUQ0タイトルにヘッダーBがあり盤面を隠していない。NGが来たら §5 を読んでから直す。
2. **ゲオオンライン(A8 aid=260706993418)の再追加可否**＝社長判断待ち。再追加するなら poicasi-platform をcwdにしたセッションで `web/portal/poi-a8-footer.js` に適用（下書き: 旧scratchpadの `geo-readd.patch`／消えていれば 0b8aaf2 以前の `Yabuzame/Assets/WebGLTemplates/Cosmic/TemplateData/poi-a8-footer.js` から該当エントリを byte 一致で移植）。403のままなら入れない。

## 4. 制約・ルール
- `/Users/suzukimasahiro/Unity/warukyure/DECISIONS.md`（Yabuzameには無し）
- `/Users/suzukimasahiro/.claude/manuals/game-layout-standard.md` v7（非ゲーム＝ヘッダーA+B+広告フッター／プレイ＝ヘッダーA+ADVIRTUA）
- 広告: A8の `html`/`aid=` は1バイトも変えない、PR表記を消さない、`weights.a8=100` を下げない、AdSense先入れ禁止。
- `git add -A`/`.`/`-u`/`commit -a` 禁止（明示パスのみ）。委譲文に【スコープ外変更禁止】。
- 09-13 17:00までの時限例外（実装=Claude/レビュー=CODEX）は**失効済み**。通常体制に戻っている。
- ポータル正本 `/Users/suzukimasahiro/Desktop/poicasi-platform/web/portal/poi-a8-footer.js`（全ゲーム共通。ゲーム側にコピーを持たない）。

## 5. ハマりどころ
- ゲーム側 `TemplateData/poi-a8-footer.js` は古い10件版で死にリンク(403)入り。**正本9件が正しい**。「件数が減った」と誤認して戻さない。
- chrome kit v1.1.23 には footer に広告HTMLを入れるAPIが無い。正本スクリプトが `footer.poicasi-chrome-footer` を探して注入する方式。
- poiresult/v3 はリザルト表示時に自前で non-game に切替え、閉じると play に戻す。WARUQ0タイトルはUnity側から surface 通知が要る（jslib経由）。chrome mount直後に play へ戻される事があり、index.html で 200〜3200ms に non-game を再主張している。
- warukyure のbatchmodeビルドは `Main.unity` を再採番する→ deploy-ancestry-gate が未コミットで止める。数字を除いた差分が同一なら別コミットで取り込む（コミット文に出典タグ `[AI仮]` 必須）。
- deploy は `WARUKYURE_S3_PREFIX=game/warukyure ./deploy_webgl.sh`（warukyure）／`./deploy_webgl.sh`（Yabuzame）。
- poicasi-org をcwdにしたセッションからは poicasi-platform へ書けない（product-boundary-gate）。

## 6. 次セッションに最初に貼る文言
```
/Users/suzukimasahiro/Unity/warukyure/handoff_20260913_YABU鮫WARUQ0ヘッダーBフッター広告.md を読んで、§0→§3の順で着手して
```

## 7. ステータス
- ①完了: 3点の実装・push・本番反映・+60分観測pass（09-13 01:04:24）・CODEX PASS
- ②途中: なし
- ③判断待ち: 社長の実機目視（4画面）／ゲオオンライン再追加の可否
