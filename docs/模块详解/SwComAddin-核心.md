# SwComAddin 核心模块

> 项目配置、COM 入口、WPF 宿主、SW API 桥接

---

## 项目配置

- **目标框架**：.NET Framework 4.8
- **平台**：x64
- **依赖**：SolidWorks.Interop.sldworks、SolidWorks.Interop.swconst、System.Text.Json 8.0.5
- **输出**：COM 可见 DLL，通过 RegAsm 注册

## SwAddin.cs — COM 入口

SolidWorks 插件生命周期管理：

| 方法 | 职责 |
|------|------|
| `ConnectToSW()` | 接收 `ISldWorks` 引用，注册 AssemblyResolver，创建 TaskPane |
| `DisconnectFromSW()` | 销毁 TaskPane，释放 COM 对象 |
| `Register()` | 写入注册表 `HKLM\SOFTWARE\SolidWorks\Addins\{GUID}` |
| `Unregister()` | 清除注册表项 |

**AssemblyResolver**：处理 SW 加载 DLL 时的程序集解析，从插件 DLL 所在目录加载依赖。

**GUID**：
- 接口 `ISwAddin`：`DA306A0D-EAC5-4406-8610-B1DA805D9270`
- 类 `SwAddin`：`B3E7D8A1-4F2C-4A91-B5D6-E8F0A1C2D3E4`
- ProgId：`SwAiPlugin.Addin`

## SwTaskPaneControl.cs — WPF 宿主

WinForms `UserControl`，承载 WPF 界面并修复键盘输入。

**核心机制**：

```
WinForms UserControl
  └── ElementHost (Dock=Fill)
        └── MainTaskPaneView (WPF UserControl)
```

**键盘钩子**（`WH_GETMESSAGE`）：
1. 捕获当前线程所有消息
2. 检测键盘范围消息 (`WM_KEYFIRST` ~ `WM_KEYLAST`)
3. 如果焦点在 TextBox/PasswordBox 且消息目标不是 WPF HWND → 重定向
4. `WM_CHAR` 处理：手动插入字符到 TextBox，设 CaretIndex，吞掉原始消息

## Services/SwConnector.cs — SW API 桥接

封装 `ISldWorks` 引用，提供：

| 方法 | 说明 |
|------|------|
| `CreateNewPart()` | 调用 `ISldWorks.NewPart()` |
| `GetActiveDocName()` | 获取当前活动文档文件名 |
| `GetSwApp()` | 返回原始 `ISldWorks` 引用 |
| `IsConnected` | 检查连接状态 |

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| 主界面 Tab1-Tab6 详解 | [SwComAddin 主界面](SwComAddin-主界面.md) |
| 参数化建模与 Builder 模式 | [SwComAddin 参数化建模](SwComAddin-参数化建模.md) |
| 自动更新系统 | [SwComAddin 更新系统](SwComAddin-更新系统.md) |
| Python AI 后端 | [SwAiBackend](SwAiBackend.md) |
| C++ 原生 COM 垫片 | [SwNativeShim](SwNativeShim.md) |
| VBA 宏启动器与旧版 | [SwMacroPlugin](SwMacroPlugin.md) |
