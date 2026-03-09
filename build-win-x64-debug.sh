#!/usr/bin/env bash
set -euo pipefail

PROJECT="src/SkylarkTerminal/SkylarkTerminal.csproj"
RUNTIME="win-x64"
CONFIG="Debug"
SINGLE_FILE=true
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/src/SkylarkTerminal/bin/$CONFIG/net10.0/$RUNTIME/publish"
OUTPUT="$SCRIPT_DIR/publish-debug.zip"

while [[ $# -gt 0 ]]; do
  case $1 in
    --no-single-file) SINGLE_FILE=false; shift ;;
    *) echo "未知参数: $1"; exit 1 ;;
  esac
done

PUBLISH_ARGS=(
  "$PROJECT" -c "$CONFIG" -r "$RUNTIME" --self-contained true
)

if [ "$SINGLE_FILE" = true ]; then
  PUBLISH_ARGS+=(-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true)
  echo "=== 构建 SkylarkTerminal ($RUNTIME) [调试单文件模式] ==="
else
  echo "=== 构建 SkylarkTerminal ($RUNTIME) [调试多文件模式] ==="
fi

dotnet publish "${PUBLISH_ARGS[@]}"

echo ""
echo "=== 发布目录统计 ==="
echo "文件数: $(find "$PUBLISH_DIR" -type f | wc -l)"
echo "总大小: $(du -sh "$PUBLISH_DIR" | cut -f1)"

rm -f "$OUTPUT"
(cd "$PUBLISH_DIR/.." && zip -r "$OUTPUT" publish)

echo ""
echo "=== 构建完成 ==="
echo "输出: $OUTPUT ($(du -sh "$OUTPUT" | cut -f1))"
