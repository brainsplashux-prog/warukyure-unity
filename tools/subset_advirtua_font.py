#!/usr/bin/env python3
"""Ad-Virtua SDK同梱フォント MPLUS1p-Bold.ttf をサブセットして配信容量を減らす。

使い方:
    python3 tools/subset_advirtua_font.py            # 既定=jis1（案B）
    python3 tools/subset_advirtua_font.py cp932      # 豆腐(□)が目立つ時の逃げ先（案A）
    python3 tools/subset_advirtua_font.py original   # 原本に戻す

【なぜ必要か】このフォントは Resources/Ad-Virtua/AdPlaySettings.asset →
Ad-Virtua_SurveyCanvas.prefab から参照されるため、アンケート広告を出さなくても常に
WebGL.data に焼き込まれる。原本 1,717,220 B は配信容量として重い（CLAUDE.md §1-29）。

【やってよい根拠】Ad-Virtua公式 https://docs.ad-virtua.com/ を2026-09-06に確認。
  - 禁止は「Prefabの構成を変更しないでください／スクリプトの追記・書き換えを行わないでください」のみ。
    本スクリプトは .ttf のバイト列だけを差し替え、Prefab・スクリプト・guid には一切触れない。
  - 「UIデザインの変更で可能な要素」に「フォント種別、フォントカラー」と明記＝フォント差し替えは公式に許可。
  - フォント自体は SIL OFL 1.1（Ad-Virtua/Font/Documents/）で "modify" が明示的に許可。
    予約フォント名の指定は無く（プレースホルダのまま）、ライセンス文は同梱したまま残す。

【範囲の選び方】アンケート広告の文言はサーバ配信で事前に文字が分からない。
  jis1  = JIS第1水準まで。4,660字 / 1,009,716 B。稀な漢字（苗字・地名・商品名）は豆腐(□)になる。
  cp932 = JIS X 0208 全域。5,779字 / 1,335,284 B。日本語の通常文で使う文字は全て残る。
  [社長仮] 2026-09-06 社長原文「一旦Bで対応しておいておれが見ながら文字化け多いなと思ったら
  A対応に切り替える」→ jis1 から開始。**見直し条件＝社長が実機で豆腐(□)を多いと感じた時点で
  cp932 へ切替**（本スクリプトに cp932 を渡して再実行→ビルド→デプロイするだけ）。

【SDK更新時】ガイドライン第12項でSDKは最新版推奨。更新するとフォントは原本に戻るので再実行すること。
原本は常に退避コピーから読むので、何度でも安全に掛け直せる。

【配置差の吸収】SDKの取り込み位置はプロジェクトごとに違う（例: kakekko/clanegame は
Assets/Ad-Virtua/Font/、coinroler2 は Assets/AdVirtua/Resources/Ad-Virtua/Font/）ので、
Assets/ 配下から MPLUS1p-Bold.ttf を自動探索し、見つかった全てに同じ処理を掛ける。
※Unityのアセットは内容アドレスで格納されるため、バイト同一の複製が1つでも残っていると
  そちらが焼かれて削減が効かない（2026-09-05 kakekko v1.0.2-beta で実測・−2,695Bに留まった）。
"""
import os
import shutil
import sys

from fontTools.ttLib import TTFont
from fontTools import subset

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
ORIGINAL = os.path.expanduser(
    "~/.claude/trash-manual/20260906-advirtua-font-original/MPLUS1p-Bold.ttf")
ORIGINAL_SIZE = 1717220
FONT_NAME = "MPLUS1p-Bold.ttf"


def find_fonts():
    """Assets/ 配下の MPLUS1p-Bold.ttf を全部拾う（複製が残ると削減が効かないため）。"""
    hits = []
    for dirpath, _dirnames, filenames in os.walk(ASSETS):
        if FONT_NAME in filenames:
            hits.append(os.path.join(dirpath, FONT_NAME))
    if not hits:
        sys.exit(f"[subset] {FONT_NAME} が見つからない: {ASSETS}")
    return sorted(hits)


def in_cp932(cp):
    try:
        chr(cp).encode("cp932")
        return True
    except Exception:
        return False


def in_jis1(cp):
    """JIS第1水準まで（Shift_JIS 先頭バイトが 0x9F 以下＝第2水準を除く）。"""
    try:
        b = chr(cp).encode("cp932")
    except Exception:
        return False
    return len(b) == 1 or b[0] <= 0x9F


LEVELS = {"jis1": in_jis1, "cp932": in_cp932}


def ensure_original(fonts):
    """原本の退避コピーを確保する（CLAUDE.md §1-27: このMac内のデータは削除しない）。"""
    if os.path.exists(ORIGINAL):
        return
    src = next((f for f in fonts if os.path.getsize(f) == ORIGINAL_SIZE), None)
    if src is None:
        sys.exit(f"[subset] 原本が見つからない: {ORIGINAL}\n"
                 f"  現在のフォントも既にサブセット済み。SDKを再インポートして原本を戻すこと。")
    os.makedirs(os.path.dirname(ORIGINAL), exist_ok=True)
    shutil.copy2(src, ORIGINAL)
    print(f"[subset] 原本を退避: {ORIGINAL}")


def main():
    level = sys.argv[1] if len(sys.argv) > 1 else "jis1"
    if level not in LEVELS and level != "original":
        sys.exit(f"[subset] 不明な範囲: {level}（jis1 / cp932 / original）")

    fonts = find_fonts()
    ensure_original(fonts)

    if level == "original":
        for f in fonts:
            before = os.path.getsize(f)
            shutil.copy2(ORIGINAL, f)
            print(f"[subset] 原本に戻した: {os.path.relpath(f, ROOT)} "
                  f"{before:,} B -> {os.path.getsize(f):,} B")
        return

    font = TTFont(ORIGINAL)
    keep = {cp for cp in font.getBestCmap() if LEVELS[level](cp)}
    ss = subset.Subsetter()
    ss.options.layout_features = ["*"]
    ss.options.notdef_outline = True
    ss.populate(unicodes=keep)
    ss.subset(font)

    tmp = os.path.join(ROOT, f".{FONT_NAME}.subset.tmp")
    font.save(tmp)
    after = os.path.getsize(tmp)

    for f in fonts:
        before = os.path.getsize(f)
        shutil.copyfile(tmp, f)
        print(f"[subset] {os.path.relpath(f, ROOT)}: {before:,} B -> {after:,} B "
              f"（{after - before:+,} B）")
    os.replace(tmp, os.path.join(os.path.dirname(ORIGINAL), f".{FONT_NAME}.last"))

    print(f"[subset] {level}: {len(keep):,}字 / {after:,} B "
          f"（原本 {ORIGINAL_SIZE:,} B から -{ORIGINAL_SIZE - after:,} B / 対象 {len(fonts)} ファイル）")


if __name__ == "__main__":
    main()
