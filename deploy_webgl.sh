#!/usr/bin/env bash
# わるきゅーれ(DEV) Unity WebGL 本番配信スクリプト（キャッシュ規約 webgl-cache-standard.md 準拠）
#
# 🛑規約:
#   - index.html は no-store 系（毎回取り直す）
#   - Build 成果物 / TemplateData / StreamingAssets は immutable
#   - Unity は固定ファイル名なので immutable は ?v=<version> 付与とセットでしか使わない
#     → 付与を grep で検証し、0件なら exit 1 でデプロイ中止
#       （TemplateData/style.css・ヘッダーAアイコンも対象。S3側に手で ?v= を足しても
#         次のビルドで退行するため、毎回ここで付け直す）
#   - 圧縮形式は自動判定（.br→br / .gz→gzip / .unityweb→エンコーディング無し / 無圧縮）
#   - aws s3 sync に --delete を新たに付けない（配信先の他資産を消さない）
set -euo pipefail

REGION="ap-northeast-1"
BUCKET="poicasi-lp"
S3_PREFIX="game/warukyure-dev"
DISTRIBUTION_ID="E3L7ISRXI1446E"
BUILD_DIR="$(cd "$(dirname "$0")" && pwd)/Builds/WebGL"
CC_HTML="no-cache, no-store, must-revalidate"
CC_ASSET="public, max-age=31536000, immutable"
VERSION="${1:-$(date +%Y%m%d%H%M%S)}"

S3_ROOT="s3://${BUCKET}/${S3_PREFIX}"
DIST_URL="https://lp.poicasi.co.jp/${S3_PREFIX}/"

[ -d "$BUILD_DIR/Build" ] || { echo "Error: $BUILD_DIR/Build が無い。先に Unity WebGL ビルドを行うこと。" >&2; exit 1; }
[ -f "$BUILD_DIR/index.html" ] || { echo "Error: $BUILD_DIR/index.html が無い。" >&2; exit 1; }

# 1. loader から成果物の basename を確定（WebGL / client 等、プロジェクトで異なる）
LOADER="$(ls "$BUILD_DIR/Build/"*.loader.js 2>/dev/null | head -1)"
[ -n "$LOADER" ] || { echo "Error: Build/*.loader.js が無い。" >&2; exit 1; }
NAME="$(basename "$LOADER" .loader.js)"

# 2. 圧縮形式を自動判定
if   [ -f "$BUILD_DIR/Build/$NAME.wasm.br" ];       then EXT=".br";       ENCODING="br"
elif [ -f "$BUILD_DIR/Build/$NAME.wasm.gz" ];       then EXT=".gz";       ENCODING="gzip"
elif [ -f "$BUILD_DIR/Build/$NAME.wasm.unityweb" ]; then EXT=".unityweb"; ENCODING=""
elif [ -f "$BUILD_DIR/Build/$NAME.wasm" ];          then EXT="";          ENCODING=""
else echo "Error: Build/$NAME.wasm* が無い。" >&2; exit 1; fi
echo "== わるきゅーれ(DEV) deploy  name=$NAME  ext=${EXT:-none}  version=$VERSION"

put() { # put <localfile> <s3key> <content-type>
  local extra=()
  [ -n "$ENCODING" ] && extra+=(--content-encoding "$ENCODING")
  aws s3 cp "$1" "$S3_ROOT/$2" --region "$REGION" \
    --content-type "$3" --cache-control "$CC_ASSET" ${extra[@]+"${extra[@]}"}
}

echo "== Build/ =="
put "$BUILD_DIR/Build/$NAME.data$EXT"         "Build/$NAME.data$EXT"         application/octet-stream
put "$BUILD_DIR/Build/$NAME.wasm$EXT"         "Build/$NAME.wasm$EXT"         application/wasm
put "$BUILD_DIR/Build/$NAME.framework.js$EXT" "Build/$NAME.framework.js$EXT" application/javascript
# loader.js は常に非圧縮
aws s3 cp "$BUILD_DIR/Build/$NAME.loader.js" "$S3_ROOT/Build/$NAME.loader.js" --region "$REGION" \
  --content-type application/javascript --cache-control "$CC_ASSET"

if [ -d "$BUILD_DIR/TemplateData" ]; then
  echo "== TemplateData/ =="
  aws s3 sync "$BUILD_DIR/TemplateData/" "$S3_ROOT/TemplateData/" --region "$REGION" --no-progress \
    --cache-control "$CC_ASSET"
fi
if [ -d "$BUILD_DIR/StreamingAssets" ]; then
  echo "== StreamingAssets/ =="
  aws s3 sync "$BUILD_DIR/StreamingAssets/" "$S3_ROOT/StreamingAssets/" --region "$REGION" --no-progress \
    --cache-control "$CC_ASSET"
fi

# 3. index.html に ?v=$VERSION を打ち込む（既存の ?v= は付け替え＝多重付与しない）
echo "== index.html (?v=$VERSION を付与) =="
TMP_HTML="$(mktemp -t deploy_index)"
trap 'rm -f "$TMP_HTML"' EXIT
sed -E \
  -e "s#__BUILD_VERSION__#$VERSION#g" \
  -e "s#(/$NAME\.(data|wasm|framework\.js|loader\.js)(\.(br|gz|unityweb))?)(\?v=[^\"]*)?\"#\1?v=$VERSION\"#g" \
  -e "s#(TemplateData/[A-Za-z0-9_.-]+)(\?v=[^\"]*)?\"#\1?v=$VERSION\"#g" \
  "$BUILD_DIR/index.html" > "$TMP_HTML"
if grep -q "__BUILD_VERSION__" "$TMP_HTML"; then
  echo "!! __BUILD_VERSION__ の置換に失敗した。中止する。" >&2; exit 1
fi
COUNT="$(grep -oF "?v=$VERSION" "$TMP_HTML" | wc -l | tr -d ' ')"
if [ "$COUNT" -eq 0 ]; then
  echo "!! index.html に ?v=$VERSION を付与できなかった。immutable で古いビルドが焼き付くため中止する。" >&2
  exit 1
fi
echo "   付与件数: $COUNT"
aws s3 cp "$TMP_HTML" "$S3_ROOT/index.html" --region "$REGION" \
  --content-type "text/html; charset=utf-8" --cache-control "$CC_HTML"

echo "== CloudFront invalidation =="
aws cloudfront create-invalidation --distribution-id "$DISTRIBUTION_ID" \
  --paths "/${S3_PREFIX}/*" --region "$REGION" --query 'Invalidation.{Id:Id,Status:Status}'

echo "DONE: ${DIST_URL}  (v$VERSION)"
