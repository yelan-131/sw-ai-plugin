# SwNativeShim — C++ 原生 COM 垫片

> 当托管 DLL 未正确注册时的安全降级模块

---

## 概述

**目的**：当托管 DLL (`SwComAddin.dll`) 未通过 RegAsm 正确注册时，SolidWorks 会加载此原生 DLL 作为安全降级。

**行为**：
- 实现 `ISwAddin` 接口（`ConnectToSW` / `DisconnectFromSW`）
- `ConnectToSW` 仅写日志并返回 `VARIANT_TRUE`，不创建任何 UI
- 日志写入桌面 `SwAddin.log`，提示用户运行 RegAsm

**COM 注册**：
- `DllRegisterServer()` — 写入 CLSID + InprocServer32 + SW Addins 注册表
- `DllUnregisterServer()` — 清除注册表
- 通过 `regsvr32` 注册

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| 项目配置与 COM 入口 | [SwComAddin 核心](SwComAddin-核心.md) |
| 主界面 Tab1-Tab6 详解 | [SwComAddin 主界面](SwComAddin-主界面.md) |
| 参数化建模与 Builder 模式 | [SwComAddin 参数化建模](SwComAddin-参数化建模.md) |
| 自动更新系统 | [SwComAddin 更新系统](SwComAddin-更新系统.md) |
| Python AI 后端 | [SwAiBackend](SwAiBackend.md) |
| VBA 宏启动器与旧版 | [SwMacroPlugin](SwMacroPlugin.md) |
