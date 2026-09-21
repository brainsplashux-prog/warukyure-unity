#!/usr/bin/env bash
# JEM WebGLビルド後処理: Builds/WebGL-jem/index.html をJEM向けに書き換える。
# 対象はBuilds/.build-marker(stamp_build_marker.shが生成)のBUILD_TYPE=JEMのときのみ(fail-closed)。
# WarukyureBuilder.cs(既存warukyure本体ビルドと共有)は無変更のまま、
# 「ビルド後に生成物を外部スクリプトで読み書きする」方式に統一する
# (Editor/Playerアセンブリのscripting-define境界問題を避けるため、Editor C#へは手を入れない)。
# 実行順序: WarukyureBuilder.BuildWebGLJem → stamp_build_marker.sh → deploy_webgl_jem.sh
# (内部で patch_build_index.sh → deploy_guard.sh の順に通す)
# 参照実装: Poinoshin-wt-jem-poinoshin-20260918/patch_build_index.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
MARKER="$REPO_ROOT/Builds/.build-marker"
INDEX="$REPO_ROOT/Builds/WebGL-jem/index.html"

[[ -f "$MARKER" ]] || { echo "Error: Builds/.build-marker が無い。先に stamp_build_marker.sh を実行すること。" >&2; exit 1; }
[[ -f "$INDEX" ]] || { echo "Error: $INDEX が無い。先にJEMビルドとステージングを行うこと。" >&2; exit 1; }

BUILD_TYPE="$(awk -F= '/^BUILD_TYPE=/{print $2}' "$MARKER")"
[[ "$BUILD_TYPE" == "JEM" ]] || { echo "Error: BUILD_TYPE=$BUILD_TYPE はJEMではない。JEM_BUILD付きでビルドし直すこと。" >&2; exit 1; }

python3 - "$INDEX" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()

def fail(msg):
    print("!! " + msg, file=sys.stderr)
    raise SystemExit(1)

if "<title>JEM WARUQ0 QUEST | ポイカジ</title>" not in text:
    if "<title>WARUQ0 QUEST | ポイカジ</title>" not in text:
        fail("置換対象の<title>WARUQ0 QUEST | ポイカジ</title>が見つからない(テンプレートが変わった可能性)")
    text = text.replace("<title>WARUQ0 QUEST | ポイカジ</title>", "<title>JEM WARUQ0 QUEST | ポイカジ</title>")

if 'name="poicasi-build"' not in text:
    if "</head>" not in text:
        fail("</head>が見つからない")
    text = text.replace("</head>", '  <meta name="poicasi-build" content="jem-warukyure">\n</head>', 1)

# jslib(poiresult.jslib)が game_id を window.POI_GAME_ID から読むための注入。
# 共有UI bridge等が window.POI_GAME_ID を参照していても誤検知しないよう、
# 「代入文そのもの」の有無で判定する。
POI_GAME_ID_ASSIGN = 'window.POI_GAME_ID = "jem-warukyure";'
if POI_GAME_ID_ASSIGN not in text:
    if "</head>" not in text:
        fail("</head>が見つからない")
    text = text.replace("</head>", '  <script>' + POI_GAME_ID_ASSIGN + '</script>\n</head>', 1)

if "<title>JEM WARUQ0 QUEST | ポイカジ</title>" not in text:
    fail("title置換の検証に失敗した")
if 'name="poicasi-build" content="jem-warukyure"' not in text:
    fail("meta marker挿入の検証に失敗した")
if text.count(POI_GAME_ID_ASSIGN) != 1:
    fail("POI_GAME_ID代入文がちょうど1件でない(実際: %d件)" % text.count(POI_GAME_ID_ASSIGN))

open(path, "w", encoding="utf-8").write(text)
print("PATCH_INDEX=PASS title=JEM meta=jem-warukyure poi_game_id=jem-warukyure")
PY
