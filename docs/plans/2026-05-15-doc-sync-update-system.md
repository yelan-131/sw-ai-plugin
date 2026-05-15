# 文档同步：更新系统 + Builder 模式 + 发布流水线

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or dispatching-parallel-agents to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 5 份项目文档从 v0.1.1 状态同步到 v0.1.2 代码现状，覆盖更新系统、Builder 模式重构、发布流水线、配置拆分等 22 个补充点。

**Architecture:** 5 份文档按上下游依赖关系更新。每份文档由一个独立 subagent 负责写入，互不冲突。每个 agent 需先读取对应源代码文件，再按计划中的内容要点编写文档。

**Tech Stack:** Markdown 文档，内容来源于 C# / Python 代码文件和 JSON 配置。

**Source code files to read (shared reference):**
- `SwComAddin/Services/UpdateService.cs` — 完整更新流程
- `SwComAddin/Services/UpdateLogger.cs` — 结构化日志
- `SwComAddin/Services/SemanticVersion.cs` — SemVer 2.0
- `SwComAddin/Services/PluginConfigService.cs` — 双配置 + UserConfig + PluginMeta 类定义
- `SwComAddin/Models/UpdateManifest.cs` — 数据模型 + 错误码 + 状态机枚举
- `SwComAddin/Views/MainTaskPaneView.Settings.cs` — 更新 UI 集成
- `SwComAddin/Views/MainTaskPaneView.Core.cs` — 初始化、字段、模板定义
- `SwComAddin/Services/Builders/IPartBuilder.cs` — Builder 接口
- `SwComAddin/Services/Builders/BoltBuilder.cs` — Builder 示例
- `SwComAddin/Helpers/UIHelpers.cs` — UI 辅助
- `SwComAddin/package.bat` — 发布打包脚本

**Documentation style conventions:**
- 中文撰写，技术术语保留英文
- 表格用 Markdown 标准格式
- 代码块标注语言（csharp / json / bash / text）
- 文档间交叉引用用相对链接 `[标题](文件名.md)`
- 章节编号不用数字前缀，用 Markdown 标题层级
- 保持与现有文档一致的语气和格式风格

---

## File Structure

| 文件 | 操作 | 补充点数 |
|------|------|---------|
| `docs/README.md` | 修改 | 5 |
| `docs/架构总览.md` | 修改 | 4 |
| `docs/模块详解.md` | 修改 | 8 |
| `docs/接口与数据协议.md` | 修改 | 3 |
| `docs/部署与运维指南.md` | 修改 | 5 |

---

## Task 1: 更新 docs/README.md

**Files:**
- Modify: `C:\Users\12938\Desktop\Software_Plugin\sw-ai-plugin\docs\README.md`

**Content to update:**

- [ ] **Step 1: 更新版本信息**
  - "插件版本：v1.0" → "插件版本：v0.1.2"

- [ ] **Step 2: 更新文档目录描述**
  - 架构总览："3 条核心数据流" → "4 条核心数据流（含更新系统流）"
  - 模块详解：补充"更新系统（UpdateService / UpdateLogger / SemanticVersion / PluginConfigService）、Builder 模式（9 种零件构建器）、发布流水线（package.bat）"
  - 接口与数据协议：补充"manifest.json 发布清单、13 个更新错误码、双配置文件（user_config.json + plugin_meta.json）"
  - 部署与运维指南：补充"更新日志、更新故障排查、发布打包流程"

- [ ] **Step 3: 更新快速了解的系统组成树**
  - SwComAddin 下新增：
    ```
    │     ├── UpdateService       — 自动更新（多源检查/下载/校验/安装）
    │     ├── PluginConfigService — 双配置管理 + 旧版迁移
    │     ├── Builders/           — 9 种零件构建器（Builder 模式）
    │     ├── UIHelpers           — Visual Tree 辅助 + 对话框
    ```
  - SwComAddin 主界面说明："5 页签主界面" → "6 页签主界面（含帮助页）"

---

## Task 2: 更新 docs/架构总览.md

**Files:**
- Modify: `C:\Users\12938\Desktop\Software_Plugin\sw-ai-plugin\docs\架构总览.md`

- [ ] **Step 1: 更新技术栈表**
  在技术栈表末尾新增两行：
  - "版本管理 | SemanticVersion (自实现) | SemVer 2.0 | 插件版本号解析与比较"
  - "更新日志 | UpdateLogger (自实现) | JSON Lines | 更新流程结构化日志，1MB 自动轮转"

- [ ] **Step 2: 更新目录** — 新增"更新系统数据流"和"更新系统架构"条目

- [ ] **Step 3: 新增第 4 条数据流 — 更新系统流**
  在"3. 标准件浏览流"之后新增章节，包含：
  - 流程图（ASCII art）：
    ```
    定时器(4h) / 手动点击「检查更新」
        → UpdateService.CheckForUpdateAsync()
            → ResolveSources() — 按 userCfg.UpdateSource 决定查询顺序
                → FetchGiteeManifestAsync() — Gitee Release API
                → FetchGitHubManifestAsync() — GitHub Release API
                → FetchMirrorManifestAsync() — 企业镜像直连
            → 取最高版本号 (SemanticVersion 比较)
            → 通道过滤 (stable/beta) + 跳过列表 + 稍后提醒窗口
        → UI 展示新版本信息 + 结构化更新日志
        → 用户点击「立即更新」
            → DownloadUpdateAsync() — primary_url → mirrors[] 逐一尝试
            → SHA256 校验
            → PrepareUpdate() — 解压 + 防 Zip Slip + 生成 update.bat
        → ExecuteUpdate() — 启动 update.bat
            → 等待 SW 退出 → xcopy 覆盖 → RegAsm 注册 → 写 plugin_meta.json → 重启 SW
    ```
  - 说明文字：描述检查→决策→下载→校验→安装的完整流程
  - 涉及文件列表

- [ ] **Step 4: 更新系统架构图**
  在现有架构图中的 SwComAddin 区域新增：
  - UpdateService / PluginConfigService / UpdateLogger 方框
  - 与 MainTaskPaneView 的连接线（更新 UI）
  - 与外部 GitHub/Gitee Release API 的 HTTP 连线

- [ ] **Step 5: 新增关键设计决策 — 更新系统架构**
  包含：
  - **配置拆分**：user_config.json（用户私有，更新时 preserve）vs plugin_meta.json（随发布包覆盖）
  - **多源策略**：auto 模式下 Gitee 主 + GitHub 备，取最高版本
  - **安全措施**：SHA256 校验、Zip Slip 防护、TLS 1.2 强制
  - **离线更新**：手动选择本地 ZIP + SHA256 校验
  - **状态机**：按钮 5 态（Idle → Downloading → Downloaded → Executed → Error）

---

## Task 3: 更新 docs/模块详解.md

**Files:**
- Modify: `C:\Users\12938\Desktop\Software_Plugin\sw-ai-plugin\docs\模块详解.md`

- [ ] **Step 1: 更新目录** — 新增所有新模块的目录条目

- [ ] **Step 2: 新增 Services/UpdateService.cs 小节**
  内容要点：
  - 类职责说明
  - 公开方法表：GetCurrentVersion / CheckForUpdateAsync / DownloadUpdateAsync / PrepareUpdate / ExecuteUpdate / ComputeSha256
  - CheckResult 类字段说明：HasUpdate / Skipped / Deferred / Source / Manifest / ErrorCode
  - DownloadProgress 类字段说明：BytesReceived / TotalBytes / BytesPerSecond / Fraction
  - 内部流程：ResolveSources → CheckSourceAsync → FetchXxxManifestAsync → ResolveManifestAssetUrlAsync → DownloadManifestAsync
  - 多源降级逻辑：bestResult == null || bestResult.Manifest == null 时取新结果

- [ ] **Step 3: 新增 Services/UpdateLogger.cs 小节**
  内容要点：
  - JSON Lines 格式：{ts, level, stage, code?, ...fields}
  - 三个级别方法：Info / Warn / Error
  - 日志路径：%LOCALAPPDATA%\SwAiPlugin\logs\update.log
  - 轮转策略：超过 1MB → update.log.1
  - 线程安全：lock + 异常吞掉（不影响主流程）

- [ ] **Step 4: 新增 Services/SemanticVersion.cs 小节**
  内容要点：
  - SemVer 2.0.0 完整实现
  - 支持 MAJOR.MINOR.PATCH[-PRERELEASE][+BUILDMETADATA]
  - 比较规则：数字段逐位 → 有预发布 < 无预发布 → 预发布段逐段比较
  - 属性：Major / Minor / Patch / PreRelease / BuildMetadata / IsPreRelease
  - 运算符重载 > < >= <= == !=

- [ ] **Step 5: 新增 Services/PluginConfigService.cs 小节**
  内容要点：
  - 配置职责拆分说明（user_config.json vs plugin_meta.json vs 旧 plugin_config.json）
  - LoadUserConfig / SaveUserConfig / LoadPluginMeta / WritePluginMeta / MigrateFromLegacyIfNeeded
  - UserConfig 类字段表：BackendUrl / ModelLibraryPath / UpdateSource / SkippedVersions / DeferUntilUtc / CheckIntervalHours / ReceivePrerelease / MirrorUrl
  - PluginMeta 类字段表：Schema / Version / Channel / BuildDate / UpdateRepo / GiteeRepo
  - 旧版迁移流程：plugin_config.json → 拆分 → .legacy.bak

- [ ] **Step 6: 新增 Services/Builders/ 小节**
  内容要点：
  - IPartBuilder 接口：`(bool success, string message) Build(Dictionary<string, object> parameters, ISldWorks swApp)`
  - 9 个构建器列表：BoltBuilder / NutBuilder / WasherBuilder / DowelPinBuilder / FlangeBuilder / SteppedShaftBuilder / ConnectionPlateBuilder / BracketBuilder / BearingBlockBuilder
  - Builder 模式设计意图：统一接口，便于扩展新零件类型
  - 参数模板从 5 种扩展到 9 种（新增螺栓/螺母/垫圈/圆柱销）

- [ ] **Step 7: 新增 Helpers/ 小节**
  内容要点：
  - UIHelpers 类：FindVisualChild<T> / FindAncestor<T> / ShowInputDialog

- [ ] **Step 8: 更新 Tab5 描述并新增 Tab6**
  Tab5 系统设置扩展描述：
  - 版本弹窗（底部点击版本号弹出，ESC 关闭）
  - 更新面板（底部展开，结构化更新日志 + 按钮 + 进度条 + 手动下载区）
  - 底部蓝点通知
  - 按钮状态机：Idle(立即更新) → Downloading(下载中...禁用) → Downloaded(关闭SW并安装) → Executed(已启动) → Error(重新下载)
  - 手动下载三条路径：GitHub Release / Gitee Release / 本地 ZIP
  - 未保存文档检测（HasUnsavedSwDocuments）
  - 定时检查（DispatcherTimer，默认 4 小时）

  Tab6 帮助页：
  - PageHelp 存在于 MainTaskPaneView.Core.cs
  - 通过 HelpBtn_Click 导航

- [ ] **Step 9: 更新 Models/ 目录**
  - 新增 UpdateManifest.cs 条目
  - 说明包含：UpdateManifest / UpdatePackage / UpdateFileEntry / BackendInfo / ReleaseNoteSection / UpdateErrorCodes / UpdateStage

---

## Task 4: 更新 docs/接口与数据协议.md

**Files:**
- Modify: `C:\Users\12938\Desktop\Software_Plugin\sw-ai-plugin\docs\接口与数据协议.md`

- [ ] **Step 1: 更新目录** — 新增 manifest.json、错误码、配置文件更新条目

- [ ] **Step 2: 重写配置文件一节**
  将"插件配置 (plugin_config.json)"替换为三个子节：

  **用户配置 (user_config.json)**：
  - 位置：SwComAddin 输出目录
  - 更新时 preserve，不随包发布
  - 完整字段表：
    | 字段 | 类型 | 默认值 | 说明 |
    |------|------|--------|------|
    | backend_url | string | "http://localhost:8765" | AI 后端地址 |
    | model_library_path | string | "" | 用户模型库路径 |
    | update_source | string | "auto" | 更新源：auto/gitee/github/mirror |
    | skipped_versions | string[] | [] | 用户跳过的版本列表 |
    | defer_until_utc | string? | null | "稍后提醒"到期时间（ISO8601） |
    | check_interval_hours | int | 4 | 自动检查周期（小时） |
    | receive_prerelease | bool | false | 是否接收 Beta 通道 |
    | mirror_url | string? | null | 企业镜像 URL |

  **插件元数据 (plugin_meta.json)**：
  - 位置：SwComAddin 输出目录，随发布包覆盖
  - 完整字段表：
    | 字段 | 类型 | 默认值 | 说明 |
    |------|------|--------|------|
    | schema | string | "1.0" | 数据格式版本 |
    | version | string | "0.1.1" | 当前安装版本（SemVer） |
    | channel | string | "stable" | 发布通道 |
    | build_date | string | "" | 构建时间 |
    | update_repo | string | "yelan-131/sw-ai-plugin" | GitHub 更新仓库 |
    | gitee_repo | string | "yelan1387/sw-ai-plugin" | Gitee 更新仓库 |

  **旧版迁移**：
  - 说明 plugin_config.json 自动迁移为 user_config.json + plugin_meta.json
  - 旧文件重命名为 plugin_config.json.legacy.bak
  - 迁移幂等，失败不致命

- [ ] **Step 3: 新增 manifest.json 格式小节**
  标题：发布清单 (manifest.json)
  - 用途：随 Release 上传，客户端检查更新时下载（< 5KB）
  - 完整 JSON 示例
  - 完整字段表：
    | 字段 | 类型 | 必填 | 说明 |
    |------|------|------|------|
    | schema | string | 是 | 数据格式版本 "1.0" |
    | version | string | 是 | 发布版本号（SemVer） |
    | channel | string | 是 | stable / beta / dev |
    | released_at | string | 是 | 发布时间（ISO8601） |
    | min_plugin_version | string? | 否 | 最低可升级版本 |
    | min_sw_version | int? | 否 | 最低 SW 主版本号 |
    | min_dotnet | string? | 否 | 所需 .NET 版本 |
    | force_update | bool | 否 | 强制更新（不提供"稍后"） |
    | package | object | 是 | 下载包信息 |
    | package.name | string | 是 | ZIP 文件名 |
    | package.size | long | 是 | 文件大小（字节） |
    | package.sha256 | string | 是 | SHA256 校验值 |
    | package.primary_url | string | 是 | 主下载 URL |
    | package.mirrors | string[] | 否 | 镜像 URL 列表 |
    | files | array | 否 | 逐文件清单 |
    | files[].path | string | 是 | 文件路径 |
    | files[].sha256 | string? | 否 | 文件校验值 |
    | files[].action | string | 是 | replace / delete |
    | preserve | string[] | 否 | 更新时不覆盖的文件 |
    | backend | object? | 否 | 后端兼容信息 |
    | release_notes | array? | 否 | 结构化更新日志 |
    | release_notes_summary | string? | 否 | 更新日志摘要（fallback） |
    | release_notes_url | string? | 否 | Release 页面链接 |

  - ReleaseNoteSection 格式：{ section: string, items: string[] }
  - BackendInfo 格式：{ version: string, requirements_changed: bool }

- [ ] **Step 4: 新增错误码表小节**
  标题：更新错误码
  分组表格：

  | 阶段 | 错误码 | 说明 |
  |------|--------|------|
  | 检查 | UPD-CHECK-NETWORK | 无法联系更新源 |
  | 检查 | UPD-CHECK-PARSE | 版本号解析失败 |
  | 检查 | UPD-CHECK-INCOMPATIBLE | 版本不兼容 |
  | 下载 | UPD-DL-TIMEOUT | 下载超时 |
  | 下载 | UPD-DL-HTTP | HTTP 请求失败 |
  | 下载 | UPD-DL-CANCELLED | 用户取消 |
  | 校验 | UPD-VERIFY-HASH | SHA256 不匹配 |
  | 校验 | UPD-VERIFY-SIG | 签名验证失败（预留） |
  | 安装 | UPD-INSTALL-EXTRACT | 解压失败 |
  | 安装 | UPD-INSTALL-COPY | 文件复制失败 |
  | 安装 | UPD-INSTALL-REGASM | RegAsm 注册失败 |
  | 安装 | UPD-INSTALL-SW-BUSY | SW 有未保存文档 |
  | 回滚 | UPD-ROLLBACK-OK | 回滚成功 |
  | 回滚 | UPD-ROLLBACK-FAIL | 回滚失败 |

---

## Task 5: 更新 docs/部署与运维指南.md

**Files:**
- Modify: `C:\Users\12938\Desktop\Software_Plugin\sw-ai-plugin\docs\部署与运维指南.md`

- [ ] **Step 1: 更新目录** — 新增"发布打包"和"更新相关配置"条目

- [ ] **Step 2: 更新项目文件结构树**
  在 SwComAddin/Services/ 下新增：
  ```
  │   │   ├── SwConnector.cs       # SW API 桥接
  │   │   ├── ParametricBuilder.cs # 参数化建模
  │   │   ├── UpdateService.cs     # 自动更新核心
  │   │   ├── UpdateLogger.cs      # 更新日志（JSON Lines）
  │   │   ├── SemanticVersion.cs   # SemVer 2.0 版本管理
  │   │   ├── PluginConfigService.cs # 双配置管理
  │   │   ├── CustomCategoryService.cs # 自定义分类
  │   │   ├── PartsSearchService.cs   # 零件搜索
  │   │   ├── PreviewRenderer.cs      # 3D 预览渲染
  │   │   └── Builders/               # Builder 模式构建器
  │   │       ├── IPartBuilder.cs     # 统一接口
  │   │       ├── BoltBuilder.cs      # 螺栓
  │   │       ├── NutBuilder.cs       # 螺母
  │   │       ├── WasherBuilder.cs    # 垫圈
  │   │       ├── DowelPinBuilder.cs  # 圆柱销
  │   │       ├── FlangeBuilder.cs    # 法兰
  │   │       ├── SteppedShaftBuilder.cs # 阶梯轴
  │   │       ├── ConnectionPlateBuilder.cs # 连接板
  │   │       ├── BracketBuilder.cs   # 支架
  │   │       └── BearingBlockBuilder.cs # 轴承座
  ```
  新增 Helpers/：
  ```
  │   ├── Helpers/
  │   │   └── UIHelpers.cs         # Visual Tree 辅助 + 对话框
  ```
  Models/ 下新增 UpdateManifest.cs。
  根目录新增 package.bat。

- [ ] **Step 3: 更新日志一节**
  在现有"日志"小节中补充：
  - 更新日志位置：`%LOCALAPPDATA%\SwAiPlugin\logs\update.log`
  - 格式：JSON Lines，每行一条 JSON
  - 字段：ts / level / stage / code? / 自定义字段
  - 轮转：超过 1MB 自动轮转为 update.log.1
  - 示例行：`{"ts":"2026-05-14T10:30:00.000Z","level":"info","stage":"check","phase":"started","current":"0.1.2"}`

- [ ] **Step 4: 新增"发布打包"小节**
  标题：发布打包
  - package.bat 使用说明
  - 流程：构建 → 打包 → SHA256 → 生成 manifest.json
  - 产出：SwAiPlugin_vX.Y.Z.zip + manifest.json
  - 发布步骤：GitHub/Gitee 创建 Release → 上传两个资产
  - 注意：package.bat 中 VERSION 需手动更新

- [ ] **Step 5: 新增"更新相关配置"到配置一节**
  在现有配置内容后新增：
  - 更新源选择：自动(auto) / 仅 Gitee / 仅 GitHub / 企业镜像
  - 检查周期：默认 4 小时
  - Beta 通道开关
  - 跳过版本 / 稍后提醒

- [ ] **Step 6: 新增更新相关故障排查条目**
  在故障排查中新增：

  **更新检查失败**
  - 确认网络可达 GitHub/Gitee
  - 查看 `%LOCALAPPDATA%\SwAiPlugin\logs\update.log` 中 `UPD-CHECK-NETWORK` 记录
  - 检查 plugin_meta.json 中 update_repo / gitee_repo 是否正确

  **下载失败 / SHA256 不匹配**
  - 查看日志中 `UPD-DL-HTTP` 或 `UPD-VERIFY-HASH` 记录
  - 尝试切换更新源（设置 → Gitee / GitHub）
  - 或使用本地 ZIP 手动更新

  **安装后版本号未升级**
  - 确认 update.bat 正常执行完成
  - 检查安装目录 plugin_meta.json 中 version 字段
  - 查看临时目录 `%TEMP%\SwAiPlugin_update\update.log`

  **RegAsm 注册失败**
  - 确认以管理员权限运行
  - 检查 .NET Framework 4.8 是否安装
  - 手动执行：`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe SwComAddin.dll /codebase /tlb`

---

## Self-Review Checklist

- [ ] 架构总览.md 的 4 条数据流是否都有对应模块详解条目
- [ ] 模块详解中每个类的描述是否与接口文档的数据格式一致
- [ ] 接口文档的 manifest.json 字段是否与 UpdateManifest.cs 属性一一对应
- [ ] 接口文档的 UserConfig 字段是否与 PluginConfigService.cs 一一对应
- [ ] 部署指南的文件树是否包含所有实际存在的文件
- [ ] 部署指南的排障条目是否覆盖了错误码表中的所有阶段
- [ ] README.md 的快速总览树是否与架构总览一致
- [ ] 所有文档间的交叉引用链接是否正确
- [ ] 版本号统一为 v0.1.2
- [ ] 参数模板数量统一为 9 种
- [ ] 页签数量统一为 6 个
