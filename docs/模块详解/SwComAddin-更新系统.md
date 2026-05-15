# SwComAddin 更新系统模块

> UpdateService、UpdateLogger、SemanticVersion、PluginConfigService

---

## Services/UpdateService.cs — 自动更新

管理完整的自动更新生命周期：多源检查 → 下载 ZIP → SHA256 校验 → 生成安装脚本 → 执行。

**构造函数**：`UpdateService(PluginMeta meta, UserConfig userCfg)`

**公开方法**：

| 方法 | 说明 |
|------|------|
| `GetCurrentVersion()` | 返回当前版本号，取 `PluginMeta.Version`，缺省 `"0.1.1"` |
| `CheckForUpdateAsync(cancel)` | 多源检查更新（Gitee / GitHub / Mirror），返回 `CheckResult` |
| `DownloadUpdateAsync(manifest, progress, cancel)` | 下载 ZIP 包，按 primary_url → mirrors[] 顺序尝试，SHA256 校验失败抛 `IOException` |
| `PrepareUpdate(zipPath, installDir, manifest)` | 解压 ZIP（含 Zip Slip 防护）+ 生成 `update.bat` 接力脚本 |
| `ExecuteUpdate(batPath)` | 启动安装脚本（隐藏窗口），脚本等待 SW 退出后执行 xcopy + RegAsm |
| `ComputeSha256(filePath)` | 计算文件 SHA256 哈希，返回小写十六进制字符串 |

**CheckResult 类**（检查结果封装）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `HasUpdate` | `bool` | 是否有可用更新 |
| `Skipped` | `bool` | 用户是否已跳过此版本 |
| `Deferred` | `bool` | 是否在「稍后提醒」窗口内 |
| `Source` | `string` | 更新源标识（gitee / github / mirror） |
| `Manifest` | `UpdateManifest?` | 远端发布元数据 |
| `ErrorCode` | `string?` | 错误码（如 `UPD-CHECK-NETWORK`） |
| `ErrorMessage` | `string?` | 错误描述 |

**DownloadProgress 类**（下载进度）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `BytesReceived` | `long` | 已下载字节数 |
| `TotalBytes` | `long?` | 总字节数（可能未知） |
| `BytesPerSecond` | `double` | 当前下载速度 |
| `Fraction` | `double` | 下载进度比例（0~1），计算属性 |

**内部流程**：
1. **检查阶段**：按 `UpdateSource` 配置决定查询顺序（auto 时 Gitee 主 + GitHub 备 + 可选 Mirror），各源并发取最高版本；支持通道过滤（stable / beta / dev）、跳过列表、「稍后提醒」窗口判断
2. **下载阶段**：逐源尝试下载，SHA256 校验失败自动切换下一镜像；使用 `IProgress<DownloadProgress>` 上报进度（节流 ~10 次/秒）
3. **安装阶段**：解压 → Zip Slip 路径检查 → 生成 `update.bat`（等待 SW 退出 → xcopy 覆盖 → RegAsm 注册 → 写 `plugin_meta.json` → 重启 SW）

## Services/UpdateLogger.cs — 更新日志

更新流程结构化日志，写入 `%LOCALAPPDATA%\SwAiPlugin\logs\update.log`。

**格式**：JSON Lines，每行一条 JSON，字段包含 `ts`（UTC ISO8601）、`level`（info/warn/error）、`stage`、可选 `code` 及自定义字段。

**三个公开方法**：

| 方法 | 说明 |
|------|------|
| `Info(stage, fields?)` | 记录信息级别日志 |
| `Warn(stage, errorCode?, fields?)` | 记录警告级别日志 |
| `Error(stage, errorCode, fields?)` | 记录错误级别日志 |

**轮转策略**：日志超过 1 MB 时自动重命名为 `update.log.1`，旧轮转文件删除。

**线程安全**：通过 `lock` 保证并发写入安全。

## Services/SemanticVersion.cs — 版本管理

SemVer 2.0.0 版本号解析与比较，替代 `System.Version`。

**支持格式**：`MAJOR.MINOR.PATCH[-PRERELEASE][+BUILDMETADATA]`，例如 `1.2.0`、`1.2.0-rc.1`、`1.2.0-beta.3+build.42`。

**比较规则**（遵循 semver.org §11）：
1. 数字段（MAJOR/MINOR/PATCH）逐位比较
2. 有预发布段的版本 < 无预发布段的同主版本号
3. 预发布段按 dot 拆分，逐段比较：纯数字按数值比；混合字符串按 ASCII；纯数字 < 字符串；段多者大
4. 构建元数据（`+` 后缀）不参与比较

**属性**：

| 属性 | 类型 | 说明 |
|------|------|------|
| `Major` | `int` | 主版本号 |
| `Minor` | `int` | 次版本号 |
| `Patch` | `int` | 修订号 |
| `PreRelease` | `string` | 预发布标识（不含前导 `-`），空字符串表示正式版 |
| `BuildMetadata` | `string` | 构建元数据（不含前导 `+`），不参与比较 |
| `IsPreRelease` | `bool` | 是否为预发布版本（PreRelease 非空） |

**运算符重载**：`>`、`<`、`>=`、`<=`、`==`、`!=`

**解析方法**：`TryParse(string, out SemanticVersion?)` 和 `Parse(string)`

## Services/PluginConfigService.cs — 配置管理

配置职责拆分为双文件：`user_config.json`（用户私有运行时设置，更新时 preserve）和 `plugin_meta.json`（插件元数据，随发布包覆盖）。同时提供从旧版 `plugin_config.json` 的一次性迁移。

**公开方法**：

| 方法 | 说明 |
|------|------|
| `LoadUserConfig()` | 加载用户配置，自动触发旧版迁移 |
| `SaveUserConfig(config)` | 保存用户配置到 `user_config.json` |
| `LoadPluginMeta()` | 加载插件元数据，自动触发旧版迁移 |
| `WritePluginMeta(meta)` | 仅 Updater 在更新成功后调用，写入 `plugin_meta.json` |
| `MigrateFromLegacyIfNeeded()` | 将旧 `plugin_config.json` 拆为双文件，幂等，旧文件改名为 `.legacy.bak` |

**UserConfig 字段**（8 个）：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `BackendUrl` | `string` | `"http://localhost:8765"` | AI 后端地址 |
| `ModelLibraryPath` | `string` | `""` | 模型库路径 |
| `UpdateSource` | `string` | `"auto"` | 更新源策略：auto / gitee / github / mirror |
| `SkippedVersions` | `List<string>` | `[]` | 用户跳过的版本号列表 |
| `DeferUntilUtc` | `string?` | `null` | 「稍后提醒」到期时间（UTC ISO8601） |
| `CheckIntervalHours` | `int` | `4` | 定时检查周期（小时） |
| `ReceivePrerelease` | `bool` | `false` | 是否接收预发布版（Beta 通道） |
| `MirrorUrl` | `string?` | `null` | 企业镜像 URL，非空时强制使用 |

**PluginMeta 字段**（6 个）：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Schema` | `string` | `"1.0"` | 配置文件版本 |
| `Version` | `string` | `"0.1.1"` | 当前插件版本号 |
| `Channel` | `string` | `"stable"` | 发布通道（stable / beta / dev） |
| `BuildDate` | `string` | `""` | 构建日期 |
| `UpdateRepo` | `string` | `"yelan-131/sw-ai-plugin"` | GitHub 更新仓库 |
| `GiteeRepo` | `string` | `"yelan1387/sw-ai-plugin"` | Gitee 更新仓库 |

**旧版迁移**：自动检测 `plugin_config.json`，拆分 `backend_url`/`model_library_path` 到 `user_config.json`，`version`/`update_repo`/`gitee_repo` 到 `plugin_meta.json`，完成后旧文件改名为 `plugin_config.json.legacy.bak`。迁移失败不致命，下次启动重试。

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| 项目配置与 COM 入口 | [SwComAddin 核心](SwComAddin-核心.md) |
| 主界面 Tab1-Tab6 详解 | [SwComAddin 主界面](SwComAddin-主界面.md) |
| 参数化建模与 Builder 模式 | [SwComAddin 参数化建模](SwComAddin-参数化建模.md) |
| Python AI 后端 | [SwAiBackend](SwAiBackend.md) |
| C++ 原生 COM 垫片 | [SwNativeShim](SwNativeShim.md) |
| VBA 宏启动器与旧版 | [SwMacroPlugin](SwMacroPlugin.md) |
