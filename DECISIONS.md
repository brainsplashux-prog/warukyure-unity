# DECISIONS — WARUQ0 QUEST（内部id: warukyure）意図・決定ログ

出典タグ必須（CLAUDE.md §1-24）: `[社長確定]` `[社長仮]` `[AI仮]` `[?未確認]`。タグ無し＝`[?未確認]`。

## 2026-09-06 対外表示名は「WARUQ0 QUEST」
[社長確定] 社長原文「ワルキューレ自体はコナミの商標なので非常に危ない。以降はサービス台帳に『WARUQ0 QUEST』と記載して…通称はWARUと呼ぶことにする。」
→ 「ワルキューレ」は対外表示禁止。内部id/リポ名 `warukyure` はそのまま。出典 `~/Desktop/poicasi-org/config/beta-lineup.json:96`。

## 2026-09-06 画面種別ごとのレイアウト規則（レイアウト正本 v7）
[社長確定] 社長原文「今全部にADVIRTUA入っているけどADVIRTUAはゲーム画面だけだから。そういう画面ごとのルールがあるのでちゃんとしてくれ。」
→ ゲーム画面(Battle)＝ヘッダーA＋ADVIRTUA(720x405)最前面／ゲーム以外の画面(Title/RobotSelect/Result)＝ヘッダーA＋ヘッダーB(112px)＋広告フッター(100px)・ADVIRTUAは置かない。
正本 `~/Desktop/poicasi-org/standards/game-layout-standard.md`。

## 2026-09-06 まとめて変更・まとめてデプロイは禁止
[社長確定] 社長原文「キミのこと信じてないから一気に全ゲームとかを変えたくないんだけど。3つずつ反映とかで都度見ないと不安しかない」
→ 横断是正は3本単位で止めて報告する。

## 2026-09-07 タイトル画面の採用案（背景・配置）
[社長確定] 社長原文「スタートとキャラクターは今より150ピクセル下に下げて折角の背景が見えなくて勿体無いから」→ +150px版を出して「OK」。
承認原本 `design-approved/title/approved.png`（720x1280・sha256 280426d395…）。

## 2026-09-07 タイトル台紙の同梱形態は「案C＝JPEG q90 のランタイムデコード」
[社長確定] 社長原文「案Cで問題ない。」
→ `Assets/Resources/title_bg_v4.bytes`(263,816 B) を `Texture2D.LoadImage` でデコード。`client.data.br` はベースライン比 +267,389 B（悪条件で約+7秒）。PNG同梱(案A・+1,922,742 B)とDXT1(案B・+375,255 B)は不採用。実測表は `~/Desktop/poicasi-org/design-intent/warukyure/TITLE_SPEC_IMPL_VERIFY_20260907.md` §1b。

## 2026-09-07 実装せず見つけた宿題は「開発台帳に載せて次の開発セッションに気づかせる」
[社長確定] 社長原文「報告だけされても困るので実装が必要なものは各ゲームの開発セッションが気づく様に開発台帳にこれの実装が必要だよって記載しておいてくれ。」
→ `~/Desktop/poicasi-org/state/pending-remediation.json` に登録。SessionStartフック `~/.claude/hooks/pending-remediation-notice.sh` が cwd と path を照合して自動通知する。
