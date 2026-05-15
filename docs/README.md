# SW AI Plugin 文档中心

AI 驱动的 SolidWorks 参数化建模助手插件，v0.1.4。

---

## 文档索引

### 架构 — 系统是什么

| 文档 | 说明 |
|------|------|
| [架构总览](架构/总览.md) | 系统架构图、4 条核心数据流、关键设计决策 |
| [技术栈](架构/技术栈.md) | 11 项技术选型及用途说明 |
| [设计决策](架构/设计决策.md) | 5 个 ADR：WPF 宿主 / COM 双轨 / 特征抽象层 / 更新系统 / Builder 模式 |

### 模块详解 — 怎么构成的

| 文档 | 说明 |
|------|------|
| [SwComAddin 核心](模块详解/SwComAddin-核心.md) | COM 入口 / WPF 宿主 / SW API 桥接 |
| [SwComAddin 主界面](模块详解/SwComAddin-主界面.md) | 6 页签 UI + 3D 预览 + 数据模型 |
| [SwComAddin 参数化建模](模块详解/SwComAddin-参数化建模.md) | ParametricBuilder + 9 种 Builder + 14 种特征模板 |
| [SwComAddin 更新系统](模块详解/SwComAddin-更新系统.md) | UpdateService / Logger / SemanticVersion / ConfigService |
| [SwAiBackend](模块详解/SwAiBackend.md) | Python FastAPI 后端 + Claude 命令解析 |
| [SwNativeShim](模块详解/SwNativeShim.md) | C++ 原生 COM 安全降级垫片 |
| [SwMacroPlugin + 旧版](模块详解/SwMacroPlugin.md) | VBA 宏启动 + 旧版独立前端 + DumpApi |

### 接口与数据协议 — 数据长什么样

| 文档 | 说明 |
|------|------|
| [HTTP API](接口与数据协议/HTTP-API.md) | 后端 4 个端点（health / chat / config） |
| [AI 命令协议](接口与数据协议/AI命令协议.md) | 19 种命令（基础 4 + 草图 7 + 特征 5 + 装配 2） |
| [数据结构](接口与数据协议/数据结构.md) | 标准件 JSON + 特征建模 C# 类型 |
| [配置文件](接口与数据协议/配置文件.md) | user_config.json + plugin_meta.json + 旧版迁移 |
| [manifest](接口与数据协议/manifest.md) | 远端发布清单格式（完整字段表） |
| [错误码](接口与数据协议/错误码.md) | 13 个 UPD-* 更新错误码 |

### 部署与运维 — 怎么用

| 文档 | 说明 |
|------|------|
| [构建与注册](部署与运维/构建与注册.md) | 前置条件 + 构建 + COM 注册/卸载 |
| [运行](部署与运维/运行.md) | 3 种运行方式（COM 插件 / 启动脚本 / VBA 宏） |
| [配置](部署与运维/配置.md) | API Key + 后端地址 + 更新配置 |
| [发布打包](部署与运维/发布打包.md) | package.bat 自动化打包 + 发布步骤 |
| [日志](部署与运维/日志.md) | SwAddin.log + update.log（JSON Lines） |
| [故障排查](部署与运维/故障排查.md) | 8 类常见问题及解决方法 |

### UI设计 — 界面长什么样

| 文档 | 说明 |
|------|------|
| [主界面布局](UI设计/主界面布局.md) | 6 页签 + 底部状态栏 + 导航机制 |
| [更新模块交互设计](UI设计/更新模块交互设计.md) | 零弹窗更新流程：状态栏通知 / 内联面板 / 版本选择 |

### 其他

| 文档 | 说明 |
|------|------|
| [更新日志](CHANGELOG.md) | 版本变更记录（Keep a Changelog 格式） |

---

## 快速了解

项目由 4 个子系统组成：

```
SolidWorks
  │
  ├── SwComAddin (COM 插件，嵌入 SolidWorks TaskPane)
  │     ├── SwAddin.cs          — COM 注册/连接生命周期
  │     ├── SwTaskPaneControl.cs — WPF 宿主 + 键盘修复
  │     ├── MainTaskPaneView     — 6 页签主界面（含帮助页）
  │     ├── PartPreviewView      — 3D 预览渲染
  │     ├── SwConnector          — SolidWorks API 桥接
  │     ├── ParametricBuilder    — 参数化建模执行
  │     ├── UpdateService        — 自动更新（多源检查/下载/校验/安装）
  │     ├── PluginConfigService  — 双配置管理 + 旧版迁移
  │     ├── Builders/            — 9 种零件构建器（Builder 模式）
  │     └── UIHelpers            — Visual Tree 辅助 + 对话框
  │
  ├── SwAiBackend (Python FastAPI 后端)
  │     ├── app.py              — HTTP API 服务
  │     ├── sw_command_parser.py — Claude AI 命令解析
  │     └── config_manager.py   — API Key 管理
  │
  ├── SwNativeShim (C++ 原生 COM 垫片)
  │     └── 安全降级，防止 SW 崩溃
  │
  └── SwMacroPlugin (VBA 宏启动器)
        └── 备用启动方式
```

---

## 版本信息

- 插件版本：v0.1.4
- AI 模型：Claude Sonnet 4.6
- 支持 SolidWorks：2020+
- 运行平台：Windows (x64)
