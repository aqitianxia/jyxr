#!/bin/bash
DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$DIR/../.." && pwd)"

echo "=================================================="
echo "    🚀 正在启动 《金庸群侠传XR》MOD 创作者工坊..."
echo "=================================================="

cd "$PROJECT_ROOT"

# 寻找可用空闲端口（默认从 8384 开始递增）
PORT=8384
while lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1 ; do
    PORT=$((PORT+1))
done

python3 -m http.server $PORT &
PID=$!

sleep 0.6
open "http://localhost:$PORT/tools/mod_editor/"

echo "✅ 工坊已在浏览器中打开：http://localhost:$PORT/tools/mod_editor/"
echo "💡 提示：关闭此终端窗口即可退出编辑器服务。"

wait $PID
