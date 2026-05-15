# 更新日志

本项目的所有重要更改都会记录在此文件中。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/)。

## [0.1.2] - 2026-05-14

### 新增

- **更新系统**：完整的自动更新流水线（检查 → 下载 → SHA256 校验 → 安装）
  - 多源查询：Gitee 主 + GitHub 备 + 企业镜像，自动取最高版本
  - SHA256 校验 + Zip Slip 防护 + TLS 1.2 强制
  - 结构化更新日志（分区 + 编号列表），自适应滚动
  - 按钮状态机（5 态）防重复操作
  - 手动下载三条路径：GitHub / Gitee / 本地 ZIP
  - 定时检查（默认 4 小时）
  - 未保存文档检测，防止更新丢数据
- **参数化建模**：重构为 Builder 模式（IPartBuilder 接口），新增 9 种零件构建器
  - 螺栓、螺母、垫圈、圆柱销、法兰、阶梯轴、连接板、支架、轴承座
  - 参数模板从 5 种扩展到 9 种
- **配置管理**：拆分为 user_config.json（用户私有）+ plugin_meta.json（随包覆盖）
  - 旧 plugin_config.json 自动迁移（幂等，不致命）
  - 新增 8 个用户可配置字段（更新源/检查周期/Beta 通道/跳过列表等）
- **发布打包**：package.bat 自动化构建 → 打包 → SHA256 → 生成 manifest.json
- **版本管理**：SemanticVersion（SemVer 2.0 完整实现）
- **更新日志**：UpdateLogger（JSON Lines 格式，1MB 自动轮转）
- **UI**：主界面拆为 6 个 partial class，新增 Tab6 帮助页

### 修复

- 键盘钩子导致 SW 掉帧：改用快速消息类型检查，仅在键盘消息时做完整封送
- HTTP 请求在 SW 进程内永久挂起：WebRequest.GetSystemWebProxy() 问题，改为 UseProxy = false 直连
- 多源更新降级 Bug：Gitee 失败时 bestResult.Manifest 为 null 导致 GitHub 结果被丢弃
- 更新安装后版本号未升级：改由 update.bat 在 RegAsm 成功后写入 plugin_meta.json
- 下载完成后按钮可重复点击：状态机锁定已下载状态

### 变更

- MainTaskPaneView 拆分为 6 个 partial class（Core / Ai / Parts / Custom / Parametric / Settings）
- 旧红色横幅更新通知 → 底部蓝点 + 可展开更新面板
- Tab5 系统设置新增版本弹窗（ESC 关闭）+ 更新面板 + 手动下载区

## [0.1.1] - 2026-05-12

### 新增

- 初始可运行版本
- COM 插件嵌入 SolidWorks TaskPane，5 页签主界面
- 标准件库浏览 + 3D 预览（6 大类 / 17 小类 / 40+ 种标准件）
- 参数化建模（法兰/阶梯轴/连接板/支架/轴承座）
- AI 智能助手（中文 → Claude → 19 种命令 JSON）
- C++ 原生 COM 垫片安全降级
- VBA 宏备用启动

[0.1.2]: https://github.com/yelan-131/sw-ai-plugin/releases/tag/v0.1.2
[0.1.1]: https://github.com/yelan-131/sw-ai-plugin/releases/tag/v0.1.1
