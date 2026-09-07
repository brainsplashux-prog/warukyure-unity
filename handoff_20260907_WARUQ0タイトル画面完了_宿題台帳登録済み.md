# 引き継ぎ: WARUQ0 QUEST タイトル画面（2026-09-07, from ワルキューレのタイトル画面実装〜本番デプロイ〜宿題台帳登録セッション）

## 0. 起動時に必ず守ること（省略禁止）
1. 読む: 本MD → `/Users/suzukimasahiro/Unity/warukyure/DECISIONS.md` → §4の正本。
2. 現在地の確認: `cd ~/Unity/warukyure && git log --oneline -3`。HEAD が `542b84f` で origin/main と一致していれば本MDの記述どおり。
3. 🛑 **指示があるまで実装／ビルド／デプロイはしない。** 本件は完了済みで、残っているのは「次の開発とマージして直す宿題」だけ。
4. Unity案件: Unity MCP は全コールで `unity_instance` を毎回明示、破壊操作前に `productName` を検証。
5. 同じ確認・質問を社長に投げる前に DECISIONS.md・memory・`search_session_transcripts` で自己解決を試みる。
6. ポイカジ案件なので、着手前に `~/Desktop/poicasi-org/reports/` の最新 `digest.md` を読む。
7. このリポで開発を始めると SessionStart フックが「未是正の宿題」を3件（+横断1件）通知する。**その開発とマージして直すのが既定**（単独では着手しない）。

## 1. 目的とゴール
WARUQ0 QUEST（内部id `warukyure`）に、承認済みタイトル画像を出して START タップまで待機するタイトル画面を新設し、本番配信する。完了条件＝仕様↔実装照合表が全PASS＋本番デプロイ＋postfix-watch +60分pass。**→ 全て達成済み。**

## 2. 現在地（完了）

### 版管理状態
- git あり / origin `https://github.com/brainsplashux-prog/warukyure-unity.git`
- ブランチ `main` / 最新 `542b84f`（2026-09-07・**push済み**・origin/main と同一、ahead 0）
- 未コミット 49件 = `design-state/**` の raw.json/raw.png 大量差分（自動生成物）＋ `Assets/WebGLTemplates/PoiLoader/{index.html,style.css}` ＋未追跡 `design-approved/`。**今回は意図的に触っていない。広域ステージング禁止＝明示パスのみ add。**
- 版: 本番配信 version `8d48063-20260907T1244Z`

### やったこと
- `Assets/Scripts/TitleScreen.cs`（新規146行）／`Assets/Scripts/WarukyureBoard.cs`（追加4箇所のみ・座標変更0件）
- 台紙は **案C＝JPEG q90 のランタイムデコード**（`Assets/Resources/title_bg_v4.bytes` 263,816 B）。元PNGは `Assets/ArtSource/title_bg_v4.png` に退避。
- 本番デプロイ済み `https://lp.poicasi.co.jp/game/warukyure/`
- 照合表（13行全PASS・容量4案実測・配信検証5行PASS）: `/Users/suzukimasahiro/Desktop/poicasi-org/design-intent/warukyure/TITLE_SPEC_IMPL_VERIFY_20260907.md`
- postfix-watch `20260907214651-e9e4` = **+5m/+30m/+60m すべて pass・closed**（＝「直った」と言ってよい状態）
- 宿題4件を `~/Desktop/poicasi-org/state/pending-remediation.json` に登録（poicasi-org `c091664`・push済み）＋ `BACKLOG.md` に月次見直し1件。

## 3. 残作業（優先順）
すべて「単独で着手しない。**このリポで次の開発が発生したときにマージして直す**」もの。詳細は `~/Desktop/poicasi-org/state/pending-remediation.json` を読む。
1. `WARU-BUILD-NODELETE`（重要度A）`Assets/Editor/WarukyureBuilder.cs:88-91` の `Directory.Delete` を撤去（§1-27抵触）。併せて `~/Desktop/warukyure/client` → `Builds/WebGL/` のコピーが手動なのを自動化（`deploy_webgl.sh:29` は `Builds/WebGL` を読むだけ）。
2. `LAYOUT-V7-HEADERB-FOOTER`（A・横断）Title/Result 等「ゲーム以外の画面」にヘッダーB(112px)と広告フッター(100px)が無い。`Builds/WebGL/index.html`(61,170 B) は `--common-header-height: 56px` のみ。**Unity側だけでは満たせない＝index.html/共通ヘッダーキット側の対応が要る。**
3. `WARU-TITLE-RESULT-RETURN`（B）`WarukyureBoard.cs:1486` `OnPoiResultDone` がフラグを下ろすだけで画面遷移しない。リザルト後にタイトルへ戻す仕様なら `TitleScreen` に `Reopen()` が要る。**仕様は未確定＝社長に確認してから実装。**
4. `WARU-TITLE-SESSION-DEADLOCK`（B）`TitleScreen.cs:121` でセッション未確立中のタップを捨てるが、通信が恒久失敗すると無表示のまま詰む。エラー表示＋再試行導線が要る。

## 4. 制約・ルール（絶対パス・必読）
- `/Users/suzukimasahiro/Unity/warukyure/DECISIONS.md`（**先頭必読**）
- `/Users/suzukimasahiro/.claude/CLAUDE.md` §1-19 §1-27 §1-29 §1-30 §1-16②③ §1-24
- `/Users/suzukimasahiro/Desktop/poicasi-org/standards/game-layout-standard.md`（v7）
- `/Users/suzukimasahiro/Desktop/poicasi-org/state/pending-remediation.json`
- `/Users/suzukimasahiro/Desktop/poicasi-org/design-intent/warukyure/TITLE_SPEC_IMPL_VERIFY_20260907.md`
- `/Users/suzukimasahiro/.claude/manuals/no-delete-rule.md` / `payload-weight-rule.md` / `webgl-cache-standard.md`
- 🚨 事故モード active（2026-09-07 11:50 JST〜・scope `secrets-leak`）。解除は社長の明示宣言のみ。事実は実測のみ・着手順の自律判断は停止。

## 5. ハマりどころ（このセッションで踏んだ罠）
- **Unity WebGL の出力サイズは brotli 圧縮完了を待ってから測る。** 途中で測ると部分ファイルを読む（実際に誤値 7,008,256 を掴んだ）。判定＝`pgrep -f 'Unity.app/Contents/MacOS/Unity'` が空 かつ ログ末尾が `[Package Manager] Server process was shutdown`。
- `WarukyureBuilder.cs` が出力先を `Directory.Delete` するので、ビルド前に `~/Desktop/warukyure/client` を `~/.claude/trash-manual/<日付>-<名前>/` へ mv して回避する（§1-27）。
- `rsync --delete*` は `no-delete-gate.py` にブロックされる。`mv`＋`cp -R` を使う。
- 台帳を Bash で書くときはコマンド本文に `[AI仮]` 等の4語のいずれかを必ず入れる（`provenance-gate.py`。`[実測]` タグでは通らない）。
- `Main.unity` の毎ビルド 200行超の差分は `WarukyureBuilder.cs:57 NewScene()`→`:83 SaveScene()` による再生成。fileID 正規化＋ソート比較で内容同一を確認できる。
- `sleep N; <check>` の連結は Bash ツールにブロックされる。`until ...; do sleep 15; done` を丸ごと `run_in_background: true` で渡す。

## 6. 次セッションに最初に貼る文言
```
/Users/suzukimasahiro/Unity/warukyure/handoff_20260907_WARUQ0タイトル画面完了_宿題台帳登録済み.md を読んで、§0→§3の順で着手して
```

## 7. ステータス
- ①完了: タイトル画面の実装・案C軽量化・照合表・本番デプロイ・postfix-watch +60分pass・宿題4件の台帳登録と自動通知の実測検証。
- ②途中: なし（残作業3は全て「次の開発とマージ」待ちで単独着手しない）。
- ③判断待ち: `WARU-TITLE-RESULT-RETURN` のリザルト後の遷移仕様（タイトルへ戻すのか盤面に留まるのか）。
