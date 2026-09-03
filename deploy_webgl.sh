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
set -euo pipefail
trap 'exit 1' ERR

# --- デプロイ前ゲート（handoff §3-C 施策1 / 正本 incident-recovery-and-maintenance.md §9）---
# 「戻せる版が無い」「キャッシュが焼き付く」状態で出させないための機械ゲート。
# リリース宣言済みサービスは NG で exit 1 する。SKIP_PREFLIGHT=1 で明示的に飛ばせる。
PREFLIGHT="$HOME/.claude/skills/poikatsu-deploy/scripts/preflight_master.sh"
if [ -z "${SKIP_PREFLIGHT:-}" ] && [ -x "$PREFLIGHT" ]; then
  "$PREFLIGHT" warukyure || { echo "preflight NG のためデプロイ中止（直すか SKIP_PREFLIGHT=1）" >&2; exit 1; }
fi

REGION="ap-northeast-1"
BUCKET="poicasi-lp"
# 2026-09-04: ベータ第2波の掲出URL正本(poicasi-platform/config/beta-lineup.json)に合わせ既定を本番 game/warukyure へ。
# dev配信は WARUKYURE_S3_PREFIX=game/warukyure-dev ./deploy_webgl.sh で明示指定する。
S3_PREFIX="${WARUKYURE_S3_PREFIX:-game/warukyure}"
DISTRIBUTION_ID="E3L7ISRXI1446E"
BUILD_DIR="$(cd "$(dirname "$0")" && pwd)/Builds/WebGL"
CC_HTML="no-cache, no-store, must-revalidate"
CC_ASSET="public, max-age=31536000, immutable"
REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
  SHORT="$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null)" || { echo "Error: git short hash を取得できない。" >&2; exit 1; }
  VERSION="${SHORT}-$(date -u +%Y%m%dT%H%MZ)"
fi
[[ "$VERSION" =~ ^[A-Za-z0-9_.~:-]+$ ]] || { echo "Error: unsafe version: $VERSION" >&2; exit 1; }

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

# 3. index.html に ?v=$VERSION を打ち込む（既存の ?v= は付け替え＝多重付与しない）
echo "== index.html (?v=$VERSION を付与) =="
TMP_HTML="$(mktemp -t deploy_index)"
python3 - "$BUILD_DIR/index.html" "$TMP_HTML" "$VERSION" "$NAME" "$EXT" <<'PY'
import re, sys
src, dst, ver, name, ext = sys.argv[1:]
text = open(src, encoding="utf-8").read()
def fail(msg): print("!! " + msg, file=sys.stderr); raise SystemExit(1)
def local(url): return bool(url.strip()) and not re.match(r"^(?:[a-z][a-z0-9+.-]*:|//|#)", url.strip(), re.I)
def versioned(url):
    base, mark, frag = url.partition("#"); base = base.split("?", 1)[0]
    return base + "?v=" + ver + (mark + frag if mark else "")
text = text.replace("__BUILD_VERSION__", ver).replace("__BUILD_ID__", ver)
text = re.sub(r"var\s+(buildVer|buildQ|cb)\s*=\s*[^;]*;", lambda m: f'var {m.group(1)} = "?v={ver}";', text)
text = re.sub(r"buildUrl\s*\+\s*([\"'])(/[^\"']+)\1(?:\s*\+\s*[A-Za-z_$][\w$]*)?", lambda m: repr(versioned("Build" + m.group(2))), text)
text = re.sub(r"([\"'])((?:\.?\.?/)?(?:Build|TemplateData|StreamingAssets)/[^\"']+)\1", lambda m: m.group(1) + versioned(m.group(2)) + m.group(1), text)
tags = re.compile(r"<(script|link|img)\b[^>]*>", re.I | re.S); attrs = re.compile(r"\b(src|href)\s*=\s*([\"'])(.*?)\2", re.I | re.S)
def edit_tag(tm):
    wanted = "src" if tm.group(1).lower() in ("script", "img") else "href"
    return attrs.sub(lambda am: f'{am.group(1)}={am.group(2)}{versioned(am.group(3))}{am.group(2)}' if am.group(1).lower() == wanted and local(am.group(3)) else am.group(0), tm.group(0))
text = tags.sub(edit_tag, text)
count = 0
for tm in tags.finditer(text):
    wanted = "src" if tm.group(1).lower() in ("script", "img") else "href"
    for am in attrs.finditer(tm.group(0)):
        if am.group(1).lower() == wanted and local(am.group(3)):
            count += 1
            if am.group(3).partition("#")[0].partition("?")[2] != "v=" + ver: fail("未付与または異なる版: " + am.group(3))
expected = [f"Build/{name}.loader.js?v={ver}", f"Build/{name}.data{ext}?v={ver}", f"Build/{name}.framework.js{ext}?v={ver}", f"Build/{name}.wasm{ext}?v={ver}"]
if count == 0: fail("ローカル script/link/img URL が0件")
for url in expected:
    if url not in text: fail("成果物URLが無い: " + url)
artifact = re.compile(rf"Build/{re.escape(name)}\.(?:loader\.js|data|framework\.js|wasm)(?:\.(?:br|gz|unityweb))?(?:\?[^\"'\s<]*)?")
for url in artifact.findall(text):
    if url.partition("?")[2] != "v=" + ver: fail("未付与または異なる成果物URL: " + url)
open(dst, "w", encoding="utf-8").write(text)
print(f"LOCAL_VERIFY=PASS local_assets={count} build_assets=4 version={ver}")
PY

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
aws s3 cp "$BUILD_DIR/Build/$NAME.loader.js" "$S3_ROOT/Build/$NAME.loader.js" --region "$REGION" \
  --content-type application/javascript --cache-control "$CC_ASSET"
if [ -d "$BUILD_DIR/TemplateData" ]; then
  aws s3 sync "$BUILD_DIR/TemplateData/" "$S3_ROOT/TemplateData/" --region "$REGION" --no-progress --cache-control "$CC_ASSET"
fi
if [ -d "$BUILD_DIR/StreamingAssets" ]; then
  aws s3 sync "$BUILD_DIR/StreamingAssets/" "$S3_ROOT/StreamingAssets/" --region "$REGION" --no-progress --cache-control "$CC_ASSET"
fi
aws s3 cp "$TMP_HTML" "$S3_ROOT/index.html" --region "$REGION" \
  --content-type "text/html; charset=utf-8" --cache-control "$CC_HTML"

# 版アーカイブ: 事故時に poi-rollback で戻せるようにする。
# 正本: ~/.claude/manuals/incident-recovery-and-maintenance.md / 参照: poi-rollback warukyure --list
echo "== 版アーカイブ (_archive/$VERSION) =="
aws s3 sync "$S3_ROOT/" "$S3_ROOT/_archive/$VERSION/" --region "$REGION" --no-progress \
  --exclude "_archive/*"

echo "== CloudFront invalidation =="
INVALIDATION_ID="$(aws cloudfront create-invalidation --distribution-id "$DISTRIBUTION_ID" \
  --paths "/${S3_PREFIX}/*" --region "$REGION" --query 'Invalidation.Id' --output text)"
[[ -n "$INVALIDATION_ID" && "$INVALIDATION_ID" != "None" ]] || { echo "Error: invalidation ID が無い。" >&2; exit 1; }
aws cloudfront wait invalidation-completed --distribution-id "$DISTRIBUTION_ID" --id "$INVALIDATION_ID" --region "$REGION"

REMOTE_HTML="$(mktemp -t deployed_index)"
curl -fsSL --max-time 30 "${DIST_URL}index.html?v=${VERSION}" -o "$REMOTE_HTML"
python3 - "$REMOTE_HTML" "$VERSION" "$NAME" "$EXT" <<'PY'
import re, sys
path, ver, name, ext = sys.argv[1:]; text = open(path, encoding="utf-8").read()
def fail(msg): print("!! " + msg, file=sys.stderr); raise SystemExit(1)
def local(url): return bool(url.strip()) and not re.match(r"^(?:[a-z][a-z0-9+.-]*:|//|#)", url.strip(), re.I)
tags = re.compile(r"<(script|link|img)\b[^>]*>", re.I | re.S); attrs = re.compile(r"\b(src|href)\s*=\s*([\"'])(.*?)\2", re.I | re.S); count = 0
for tm in tags.finditer(text):
    wanted = "src" if tm.group(1).lower() in ("script", "img") else "href"
    for am in attrs.finditer(tm.group(0)):
        if am.group(1).lower() == wanted and local(am.group(3)):
            count += 1
            if am.group(3).partition("#")[0].partition("?")[2] != "v=" + ver: fail("本番HTMLの未付与または異なる版: " + am.group(3))
expected = [f"Build/{name}.loader.js?v={ver}", f"Build/{name}.data{ext}?v={ver}", f"Build/{name}.framework.js{ext}?v={ver}", f"Build/{name}.wasm{ext}?v={ver}"]
if count == 0: fail("本番HTMLのローカル URL が0件")
for url in expected:
    if url not in text: fail("本番HTMLの成果物URLが無い: " + url)
artifact = re.compile(rf"Build/{re.escape(name)}\.(?:loader\.js|data|framework\.js|wasm)(?:\.(?:br|gz|unityweb))?(?:\?[^\"'\s<]*)?")
for url in artifact.findall(text):
    if url.partition("?")[2] != "v=" + ver: fail("本番HTMLの未付与または異なる成果物URL: " + url)
print(f"REMOTE_VERIFY=PASS local_assets={count} build_assets=4 version={ver}")
PY

echo "DONE: ${DIST_URL}  (v$VERSION)"
