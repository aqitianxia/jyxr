#!/bin/bash
# MOD 编辑器语法守门：index.html 内联脚本体量很大且直接决定整页能否初始化，
# 任何一处语法错误都会导致整站白屏。提交前运行本脚本做一次解析校验。
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
INDEX="$DIR/index.html"
TMP="$(mktemp /tmp/jyxr_mod_editor_check.XXXXXX.js)"
trap 'rm -f "$TMP"' EXIT

if ! command -v node >/dev/null 2>&1; then
  echo "❌ 未找到 node，无法校验。请先安装 Node.js。" >&2
  exit 1
fi

python3 - "$INDEX" "$TMP" <<'PYEOF'
import re, sys
html = open(sys.argv[1], encoding='utf-8').read()
scripts = re.findall(r'<script>(.*?)</script>', html, re.S)
if not scripts:
    print("❌ index.html 中未找到内联 <script> 块", file=sys.stderr)
    sys.exit(1)
open(sys.argv[2], 'w', encoding='utf-8').write('\n;\n'.join(scripts))
PYEOF

if node --check "$TMP"; then
  echo "✅ index.html 内联脚本语法校验通过"
else
  echo "❌ index.html 内联脚本存在语法错误，请修复后再提交" >&2
  exit 1
fi

bash -n "$DIR/启动MOD编辑器.command" && echo "✅ 启动MOD编辑器.command 语法校验通过"
