# 发布清单 (manifest.json)


随 Release 上传到 GitHub/Gitee 的元数据文件。客户端检查更新时只需下载 manifest.json（< 5KB），即可判定是否升级、是否兼容，无需下载完整 ZIP。

```json
{
  "schema": "1.0",
  "version": "0.1.2",
  "channel": "stable",
  "released_at": "2026-05-14T17:52:33Z",
  "min_plugin_version": "0.1.0",
  "min_sw_version": 2020,
  "min_dotnet": "4.8",
  "force_update": false,
  "package": {
    "name": "SwAiPlugin_v0.1.2.zip",
    "size": 1402330,
    "sha256": "5cc5ceaa9662dce83687b1aac7cb739d0fa1a6d416c76ee87bcf8d8ba1cdb3c0",
    "primary_url": "https://github.com/yelan-131/sw-ai-plugin/releases/download/v0.1.2/SwAiPlugin_v0.1.2.zip",
    "mirrors": [
      "https://gitee.com/yelan1387/sw-ai-plugin/releases/download/v0.1.2/SwAiPlugin_v0.1.2.zip"
    ]
  },
  "files": [
    { "path": "SwComAddin.dll", "sha256": "6d765ba...", "action": "replace" }
  ],
  "preserve": ["user_config.json", "Data/custom_library.json"],
  "backend": { "version": "0.1.2", "requirements_changed": false },
  "release_notes_url": "https://github.com/yelan-131/sw-ai-plugin/releases/tag/v0.1.2",
  "release_notes_summary": "v0.1.2 - Builder 模式重构 + 完整更新系统",
  "release_notes": [
    {
      "section": "参数化建模",
      "items": ["重构为 Builder 模式，新增 9 种零件构建器"]
    }
  ]
}
```

**顶层字段：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| schema | string | 是 | 数据格式版本 "1.0" |
| version | string | 是 | 发布版本号（SemVer） |
| channel | string | 是 | stable / beta / dev |
| released_at | string | 是 | 发布时间（ISO8601） |
| min_plugin_version | string? | 否 | 最低可增量升级版本，低于此版本需重装 |
| min_sw_version | int? | 否 | 最低 SolidWorks 主版本号（如 2020） |
| min_dotnet | string? | 否 | 所需 .NET Framework 版本（如 "4.8"） |
| force_update | bool | 否 | 关键安全更新，UI 不提供"稍后"选项 |
| package | object | 是 | 下载包信息（见下表） |
| files | array | 否 | 逐文件清单（见下表） |
| preserve | string[] | 否 | 更新时不覆盖的文件（相对安装目录） |
| backend | object? | 否 | 后端兼容信息（见下表） |
| release_notes_url | string? | 否 | Release 页面链接 |
| release_notes_summary | string? | 否 | 更新日志纯文本摘要（release_notes 为空时 fallback） |
| release_notes | array? | 否 | 结构化更新日志（见下表） |

**package 子对象：**

| 字段 | 类型 | 说明 |
|------|------|------|
| name | string | ZIP 文件名 |
| size | long | 文件大小（字节） |
| sha256 | string | SHA256 校验值（小写十六进制） |
| primary_url | string | 主下载 URL |
| mirrors | string[] | 镜像 URL 列表（fallback） |

**files 数组元素：**

| 字段 | 类型 | 说明 |
|------|------|------|
| path | string | 文件路径（相对安装目录） |
| sha256 | string? | 文件 SHA256 校验值 |
| action | string | replace（覆盖） / delete（新版本中删除） |

**release_notes 数组元素（ReleaseNoteSection）：**

| 字段 | 类型 | 说明 |
|------|------|------|
| section | string | 分区标题（如"参数化建模"） |
| items | string[] | 编号列表项 |

**backend 子对象（BackendInfo）：**

| 字段 | 类型 | 说明 |
|------|------|------|
| version | string | 后端版本号 |
| requirements_changed | bool | Python 依赖是否有变更（变更时提示用户重新 pip install） |

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| HTTP API | [HTTP-API](HTTP-API.md) |
| AI 命令 | [AI命令协议](AI命令协议.md) |
| 数据结构 | [数据结构](数据结构.md) |
| 配置文件 | [配置文件](配置文件.md) |
| manifest | [manifest](manifest.md) |
| 错误码 | [错误码](错误码.md) |
