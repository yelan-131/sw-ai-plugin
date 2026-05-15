# SwAiBackend — Python AI 后端

> FastAPI 服务、AI 命令解析、配置管理、AI 提示词

---

## app.py — FastAPI 服务

运行在 `localhost:8765`，CORS 全开放。

| 端点 | 方法 | 功能 |
|------|------|------|
| `/api/health` | GET | 健康检查，返回 `{"status": "ok"}` |
| `/api/chat` | POST | 自然语言转命令，返回 `{reply, commands}` |
| `/api/config` | GET | 查询 API Key 状态（脱敏显示） |
| `/api/config` | POST | 保存 API Key 到 config.json |

## sw_command_parser.py — AI 命令解析

调用 Anthropic Claude Sonnet，通过 System Prompt 将中文自然语言转换为结构化 JSON 命令。

**支持的 19 种命令**：

| 类别 | 命令 |
|------|------|
| 基础操作 | `new_part`, `new_assembly`, `open_file`, `save_file` |
| 草图 | `select_plane`, `sketch_start`, `sketch_end`, `sketch_circle`, `sketch_rectangle`, `sketch_line`, `sketch_arc` |
| 特征 | `extrude`, `revolve`, `cut_extrude`, `add_fillet`, `add_chamfer` |
| 装配体 | `insert_component`, `add_mate` |

**返回格式**：
```json
{
  "reply": "中文文字回复",
  "commands": [
    {"action": "命令类型", "参数1": 值1}
  ]
}
```

## config_manager.py — 配置管理

- 存储位置：同目录 `config.json`
- 支持环境变量 `ANTHROPIC_API_KEY` 优先
- API Key 脱敏显示：前 8 位 + ... + 后 4 位

## prompts/system_prompt.txt — AI 提示词

包含完整的命令定义、参数说明、5 个示例（六角螺栓、法兰、阶梯轴、基础操作、非 SW 问题）。与 `get_default_system_prompt()` 代码内 fallback 完全相同。

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| 项目配置与 COM 入口 | [SwComAddin 核心](SwComAddin-核心.md) |
| 主界面 Tab1-Tab6 详解 | [SwComAddin 主界面](SwComAddin-主界面.md) |
| 参数化建模与 Builder 模式 | [SwComAddin 参数化建模](SwComAddin-参数化建模.md) |
| 自动更新系统 | [SwComAddin 更新系统](SwComAddin-更新系统.md) |
| C++ 原生 COM 垫片 | [SwNativeShim](SwNativeShim.md) |
| VBA 宏启动器与旧版 | [SwMacroPlugin](SwMacroPlugin.md) |
