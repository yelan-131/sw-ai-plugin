# SW AI Plugin 文档中心

AI 驱动的 SolidWorks 参数化建模助手插件。

---

## 文档目录

| 文档 | 内容概要 |
|------|----------|
| [架构总览](架构总览.md) | 系统架构图、技术栈一览、3 条核心数据流、WPF 键盘修复 / COM 双轨注册 / 特征建模抽象层等关键设计决策 |
| [模块详解](模块详解.md) | 6 个子项目逐一拆解：SwComAddin（COM 入口 / WPF 宿主 / 连接器 / 建模器 / 主界面 5 页签 / 3D 预览 / 数据模型 / 14 种特征模板 / 40+ 标准件库）、SwAiBackend、SwNativeShim、SwMacroPlugin 等 |
| [接口与数据协议](接口与数据协议.md) | 后端 4 个 HTTP 端点详细定义、AI 命令协议 19 种命令、标准件 JSON 数据结构、特征建模 C# 类型、3 种配置文件格式 |
| [部署与运维指南](部署与运维指南.md) | 前置条件、构建步骤、COM 注册/卸载、3 种运行方式、API Key 配置、日志位置、5 类常见故障排查、完整项目文件树 |

---

## 快速了解

项目由 4 个子系统组成：

```
SolidWorks
  │
  ├── SwComAddin (COM 插件，嵌入 SolidWorks TaskPane)
  │     ├── SwAddin.cs          — COM 注册/连接生命周期
  │     ├── SwTaskPaneControl.cs — WPF 宿主 + 键盘修复
  │     ├── MainTaskPaneView     — 5 页签主界面
  │     ├── PartPreviewView      — 3D 预览渲染
  │     ├── SwConnector          — SolidWorks API 桥接
  │     └── ParametricBuilder    — 参数化建模执行
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

- 插件版本：v1.0
- AI 模型：Claude Sonnet 4.6
- 支持 SolidWorks：2020+
- 运行平台：Windows (x64)
