#!/usr/bin/env bash
# JEM分離(jem-warukyure): Unity Editor/Player アセンブリのscripting-define境界問題
# (EditorアセンブリはWebGL Playerのdefineを見れない)を避けるため、
# WarukyureBuilder.cs/WebGLBuilder.cs は無変更のまま、ビルド後にこの外部スクリプトで
# ビルド種別マーカーを書く。
# 判定の正本: ProjectSettings/ProjectSettings.asset の scriptingDefineSymbols.WebGL に
# JEM_BUILD が含まれるかどうか(このワークツリーは実際にJEM_BUILDでコンパイル・ビルドしたか)。
# 参照実装: Poinoshin-wt-jem-poinoshin-20260918/stamp_build_marker.sh
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT_SETTINGS="$REPO_ROOT/ProjectSettings/ProjectSettings.asset"
MARKER_DIR="$REPO_ROOT/Builds"
MARKER="$MARKER_DIR/.build-marker"

[[ -f "$PROJECT_SETTINGS" ]] || { echo "Error: ProjectSettings.asset が無い。" >&2; exit 1; }
# Unityビルド出力の存在確認: 通常=~/Desktop/warukyure/client、JEM=~/Desktop/warukyure/client-jem
if awk '/^  scriptingDefineSymbols:/{f=1;next} f && /^  [A-Za-z]/{f=0} f' "$PROJECT_SETTINGS" | grep -q 'JEM_BUILD'; then
  BUILD_TYPE="JEM"
  BUILD_OUT="$HOME/Desktop/warukyure/client-jem"
else
  BUILD_TYPE="NORMAL"
  BUILD_OUT="$HOME/Desktop/warukyure/client"
fi

[[ -d "$BUILD_OUT/Build" && -f "$BUILD_OUT/index.html" ]] || { echo "Error: WebGL build が無い: $BUILD_OUT。先にビルドを実行すること。" >&2; exit 1; }

mkdir -p "$MARKER_DIR"
{
  echo "BUILD_TYPE=$BUILD_TYPE"
  echo "BUILD_OUT=$BUILD_OUT"
  echo "STAMPED_AT=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "GIT_COMMIT=$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)"
  echo "GIT_BRANCH=$(git -C "$REPO_ROOT" branch --show-current 2>/dev/null || echo unknown)"
} > "$MARKER"

echo "MARKER_STAMPED build_type=$BUILD_TYPE marker=$MARKER"
