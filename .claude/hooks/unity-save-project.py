"""
PostToolUse hook: 当 Edit/Write 修改 Unity 相关文件（.cs/.asset/.unity/.prefab）后，
自动调用 MCP Unity 的 File/Save Project 清除 dirty 状态。
Unity 未开着或 MCP 未连上时静默失败，不阻塞。
"""
import sys, json, os, uuid

# 读取 hook 输入（PostToolUse JSON via stdin）
try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(0)

tool_input = data.get("tool_input", {}) or {}
path = (tool_input.get("file_path") or "").lower()
ext = os.path.splitext(path)[1]

# 只对 Unity 资产/脚本触发
if ext not in (".cs", ".asset", ".unity", ".prefab"):
    sys.exit(0)

# Unity 编辑器关闭时 websocket 连接会直接失败，静默退出
try:
    import websocket
except ImportError:
    sys.exit(0)

try:
    ws = websocket.create_connection("ws://localhost:8090/McpUnity", timeout=2)
    ws.send(json.dumps({
        "method": "execute_menu_item",
        "params": {"menuPath": "File/Save Project"},
        "id": str(uuid.uuid4()),
    }))
    ws.recv()
    ws.close()
except Exception:
    # Unity 未开 / MCP 未启动 / 网络异常 都静默，不打扰用户
    pass

sys.exit(0)
