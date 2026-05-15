# 后端 HTTP API

基础地址：`http://localhost:8765`

### GET /api/health — 健康检查

**响应**：
```json
{ "status": "ok" }
```

### POST /api/chat — 自然语言建模

发送中文自然语言，获取建模命令。

**请求**：
```json
{ "message": "创建一个M10×50的六角螺栓" }
```

**成功响应**：
```json
{
  "reply": "正在创建M10×50六角螺栓",
  "commands": [
    { "action": "new_part" },
    { "action": "select_plane", "plane": "top" },
    { "action": "sketch_start" },
    { "action": "sketch_circle", "center_x": 0, "center_y": 0, "radius": 5.0 },
    { "action": "sketch_end" },
    { "action": "extrude", "depth": 50.0 }
  ]
}
```

**错误响应**（未配置 API Key）：
```json
{ "reply": "错误：未设置ANTHROPIC_API_KEY环境变量", "commands": [] }
```

### GET /api/config — 查询配置

获取 API Key 配置状态（脱敏显示）。

**响应**：
```json
{
  "anthropic_api_key_set": true,
  "masked_key": "sk-ant-a...x1y2"
}
```

### POST /api/config — 保存配置

保存 API Key 到后端 config.json。

**请求**：
```json
{ "anthropic_api_key": "sk-ant-api03-xxxxx" }
```

**响应**：
```json
{ "status": "ok", "message": "配置已保存" }
```

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| HTTP API | [HTTP-API](HTTP-API.md) |
| AI 命令 | [AI命令协议](AI命令协议.md) |
| 数据结构 | [数据结构](数据结构.md) |
| 配置文件 | [配置文件](配置文件.md) |
| manifest | [manifest](manifest.md) |
| 错误码 | [错误码](错误码.md) |
