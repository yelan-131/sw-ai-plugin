# SwMacroPlugin — VBA 宏启动器、旧版 SwAiPlugin 与 DumpApi

> VBA 宏启动方式、独立 WPF 前端（旧版）、API 探查工具

---

## 4. SwMacroPlugin — VBA 宏启动器

**用途**：备用启动方式，从 SolidWorks 内部运行 VBA 宏来启动插件。

**流程**：
1. `GetObject(, "SldWorks.Application")` 获取 SW 实例
2. `CreateTaskpaneView2("", "SW AI Plugin")` 创建 TaskPane
3. 尝试启动独立 WPF 应用 (`SwAiPlugin.exe`)
4. 失败则 fallback 到独立启动

**注意**：路径硬编码为 `C:\Users\12938\Desktop\sw-ai-plugin\SwAiPlugin\bin\Debug\`，仅用于开发调试。

---

## 5. SwAiPlugin — 独立 WPF 前端（旧版）

独立的 WPF 应用程序，包含与 `SwComAddin` 类似的 UI 但可脱离 SolidWorks 运行。

包含 `AiClient`、`ParametricBuilder`、`PartsLibraryService`、`SwConnector` 等服务类。

**注意**：此模块已被 SwComAddin 替代，SwComAddin 集成了所有功能到 COM 插件中。

---

## 6. DumpApi — API 探查工具

用于探查 SolidWorks API 方法签名的开发辅助工具（当前为空）。

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| 项目配置与 COM 入口 | [SwComAddin 核心](SwComAddin-核心.md) |
| 主界面 Tab1-Tab6 详解 | [SwComAddin 主界面](SwComAddin-主界面.md) |
| 参数化建模与 Builder 模式 | [SwComAddin 参数化建模](SwComAddin-参数化建模.md) |
| 自动更新系统 | [SwComAddin 更新系统](SwComAddin-更新系统.md) |
| Python AI 后端 | [SwAiBackend](SwAiBackend.md) |
| C++ 原生 COM 垫片 | [SwNativeShim](SwNativeShim.md) |
