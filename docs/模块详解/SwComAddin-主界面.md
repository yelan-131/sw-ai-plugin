# SwComAddin 主界面模块

> Views/MainTaskPaneView（Tab1-Tab6）、Views/PartPreviewView、Helpers、Models、Data

---

## Views/MainTaskPaneView — 主界面 6 页签

### Tab1: 标准件库
- 3 级 `TreeView`（Category → SubCategory → StandardPart）
- 动态构建 `HierarchicalDataTemplate`（代码中构建，非 XAML）
- 零件详情卡片（名称、标准号、参数表）
- `PartPreviewView` 3D 预览
- 搜索框（占位符文字 + Enter 搜索）
- "插入到 SolidWorks" 按钮 → `OpenDoc6`

### Tab2: 自定义库
- 我的零件（`ListBox` + 添加/删除）→ `custom_library.json`
- 我的模板（参数循环输入）→ 合并到参数建模模板列表

### Tab3: 参数建模
- `ComboBox` 选择模板（5 种内置 + 自定义模板）
- 动态参数输入表单（`ItemsControl` + `ParamInput` 数据绑定）
- "生成模型" → `ParametricBuilder` 执行

### Tab4: 智能助手
- 聊天气泡 UI（用户消息蓝底白字，回复白底黑字，错误红底）
- `HttpClient` POST 到后端 `/api/chat`
- 欢迎消息提示

### Tab5: 系统设置
- API Key 配置（PasswordBox → POST `/api/config`）
- 后端服务地址配置（可修改 `backend_url`）
- 显示设置（预览图/尺寸标注/自动展开）
- SW 集成设置（自动插入/自动显示/默认单位）
- 连接状态面板（后端/SW/API Key 三项状态指示灯）
- **版本弹窗**：点击底部版本号弹出，显示当前版本 + GitHub 链接 +「检查更新」按钮，ESC 关闭
- **更新面板**：底部可展开区域，包含结构化更新日志（分节显示 ReleaseNotes）、操作按钮（立即更新/稍后提醒/跳过此版本）、进度条、错误提示区、手动下载区（GitHub Release / Gitee Release / 本地 ZIP）
- **底部蓝点通知**：有新版本时在底栏显示蓝色圆点，点击展开更新面板
- **按钮状态机**（5 态）：`Idle`（立即更新）→ `Downloading`（下载中...）→ `Downloaded`（关闭 SW 并安装）→ `Executed`（已启动）→ `Error`（重新下载）
- **手动下载三条路径**：GitHub Release 页面、Gitee Release 页面、本地 ZIP 文件（含 SHA256 校验）
- **未保存文档检测**：安装前遍历 SW 打开文档，检测 `GetSaveFlag()`，有未保存文档时阻止更新
- **定时检查**：`DispatcherTimer`，默认 4 小时周期（可通过 `UserConfig.CheckIntervalHours` 配置），启动时首次检查

### Tab6: 帮助页
- `PageHelp` 帮助信息页
- 通过 `HelpBtn_Click` 导航
- 显示插件使用说明与帮助文档

## Views/PartPreviewView — 3D 预览

使用 WPF `Viewport3D` 渲染零件的简化 3D 模型。

**PreviewRenderer**：
- `CreateCylinder()` — 圆柱体网格（24 段）
- `CreatePrism()` — 棱柱网格（N 边形）
- `CreateBox()` — 长方体网格
- 鼠标旋转（`MouseLeftButtonDown/Move/Up`）
- 自动相机定位（基于包围盒）
- 线框叠加（圆环轮廓线）

## Helpers/ — UI 辅助工具

`UIHelpers` 静态类，提供三个 WPF 可视化树辅助方法：

| 方法 | 说明 |
|------|------|
| `FindVisualChild<T>(parent)` | 在可视化树中递归查找指定类型的子元素 |
| `FindAncestor<T>(current)` | 向上遍历可视化树查找指定类型的祖先元素 |
| `ShowInputDialog(title, prompt)` | 弹出模态输入对话框（含 TextBox + 确定/取消按钮），返回输入字符串或 `null` |

## Models/ — 数据模型

| 文件 | 内容 |
|------|------|
| `StandardPart.cs` | `StandardPartsCatalog` / `Category` / `SubCategory` / `StandardPart` |
| `CustomLibrary.cs` | `CustomLibraryData` / `CustomPart` / `CustomTemplate` / `TemplateParam` |
| `ModelingFeatures.cs` | `Profile` (Circle/Rectangle/Polygon) + `ModelFeature` (Extrude/Cut/Revolve/Chamfer/Fillet/Hole) + `FeatureList` |
| `PartFeatureTemplates.cs` | 14 种标准件的 `FeatureList` 构建器 |
| `UpdateManifest.cs` | `UpdateManifest` / `UpdatePackage` / `UpdateFileEntry` / `BackendInfo` / `ReleaseNoteSection` / `UpdateErrorCodes` / `UpdateStage` |

## Data/ — 标准件库数据

6 大类、17 小类，约 40+ 种标准件：

| 类别 | 子类 | 零件数 |
|------|------|--------|
| 紧固件 | 六角头螺栓 / 螺母 / 垫圈 / 销 | ~12 |
| 轴承 | 深沟球轴承 / 推力轴承 / 直线轴承 | ~10 |
| 电机 | 步进电机 / 伺服电机 / 交流电机 | ~9 |
| 气缸 | 迷你气缸 / 标准气缸 | ~5 |
| 传感器 | 光电 / 接近 / 压力 / 温度 | ~7 |
| 导轨 | 滚珠导轨 / 滚柱导轨 | ~7 |

每个零件包含：
- `id` / `name` / `standard` — 标识
- `geometric` — 是否可生成 3D 模型
- `feature_template` — 对应 PartFeatureTemplates 中的模板名
- `specs` — 尺寸参数（键值对）
- `performance` — 性能参数（载荷、转速等）
- `description` — 中文描述

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| 项目配置与 COM 入口 | [SwComAddin 核心](SwComAddin-核心.md) |
| 参数化建模与 Builder 模式 | [SwComAddin 参数化建模](SwComAddin-参数化建模.md) |
| 自动更新系统 | [SwComAddin 更新系统](SwComAddin-更新系统.md) |
| Python AI 后端 | [SwAiBackend](SwAiBackend.md) |
| C++ 原生 COM 垫片 | [SwNativeShim](SwNativeShim.md) |
| VBA 宏启动器与旧版 | [SwMacroPlugin](SwMacroPlugin.md) |
