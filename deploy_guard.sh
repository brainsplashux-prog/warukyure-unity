#!/usr/bin/env bash
# JEMビルド/通常ビルドの取り違えデプロイを機械的に止めるガード(jem-games.md §4)。
# deploy_webgl.sh(通常/game-warukyure系) と deploy_webgl_jem.sh(JEM/game-jem-warukyure系) の
# どちらも、aws呼び出しの前に必ずこれを通す。
# 使い方: ./deploy_guard.sh <target-s3-prefix>   例: ./deploy_guard.sh game/warukyure
# 参照実装: Poinoshin-wt-jem-poinoshin-20260918/deploy_guard.sh
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
MARKER="$REPO_ROOT/Builds/.build-marker"
TARGET_PREFIX="${1:?使い方: deploy_guard.sh <target-s3-prefix>}"

[[ -f "$MARKER" ]] || { echo "GUARD_FAIL: ビルドマーカーが無い。先に ./stamp_build_marker.sh を実行すること。" >&2; exit 1; }
BUILD_TYPE="$(grep '^BUILD_TYPE=' "$MARKER" | head -1 | cut -d= -f2)"

case "$BUILD_TYPE" in
  JEM)
    case "$TARGET_PREFIX" in
      game/warukyure|game/warukyure-dev)
        echo "GUARD_FAIL: JEMビルドを${TARGET_PREFIX}(元WARU側)へデプロイしようとした。禁止。" >&2
        exit 1 ;;
      game/jem-warukyure|game/jem-warukyure-dev) ;;
      *) echo "GUARD_FAIL: 未知のprefix: $TARGET_PREFIX" >&2; exit 1 ;;
    esac
    ;;
  NORMAL)
    case "$TARGET_PREFIX" in
      game/jem-warukyure|game/jem-warukyure-dev)
        echo "GUARD_FAIL: 通常ビルドを${TARGET_PREFIX}(JEM側)へデプロイしようとした。禁止。" >&2
        exit 1 ;;
      game/warukyure|game/warukyure-dev) ;;
      *) echo "GUARD_FAIL: 未知のprefix: $TARGET_PREFIX" >&2; exit 1 ;;
    esac
    ;;
  *)
    echo "GUARD_FAIL: 不明なBUILD_TYPE: $BUILD_TYPE" >&2
    exit 1
    ;;
esac

echo "GUARD_PASS build_type=$BUILD_TYPE target=$TARGET_PREFIX"
