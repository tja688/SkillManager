# SkillManager 移植到 VS Code 插件方案（Webview Panel，去除翻译）

## 1. 目标与结论

把当前 WPF 工具完整迁移为 VS Code 扩展，核心界面采用 `Webview Panel`，保留除翻译外的全部能力，并按你确认的约束执行：

1. 主入口：独立 `Webview Panel`
2. 数据：自动迁移并兼容旧数据
3. 扫描策略：不保留“全盘扫描/清理”，改为受控路径扫描
4. 平台：仅 Windows
5. 前端：React + TypeScript

---

## 2. 现有实现分析（迁移依据）

现有功能边界和实现位置已经明确，迁移时按这些模块一一对齐：

1. 主导航与页面：`Views/MainWindow.xaml`、`Views/MainWindow.xaml.cs`
2. 技能库：`ViewModels/LibraryViewModel.cs` + `Services/LibraryService.cs` + `Services/GroupService.cs`
3. 扫描导入：`ViewModels/ScanViewModel.cs` + `Services/SkillScannerService.cs`
4. 项目/技能区管理：`ViewModels/ProjectListViewModel.cs`、`ViewModels/ProjectDetailViewModel.cs` + `Services/ProjectService.cs`
5. 自动化导入：`ViewModels/AutomationViewModel.cs` + `Services/SkillAutomationService.cs`
6. 清理：`ViewModels/CleanupViewModel.cs` + `Services/SkillCleanupService.cs`
7. 下载链接：`ViewModels/DownloadSkillsViewModel.cs` + `Services/DownloadLinksService.cs`
8. 设置与持久化：`Services/SkillManagerSettingsService.cs`、`Models/*.cs`
9. 调试能力：`Services/DebugService.cs` + `Views/DebugWindow.*`
10. 翻译（本次剔除）：`Services/*Translation*.cs`、`Services/ManualTranslationStore.cs`、`LibraryViewModel` 中翻译相关逻辑

---

## 3. 目标架构

采用“扩展主进程（Extension Host）+ Webview 前端”的双层架构：

1. Extension Host（TypeScript）
- 职责：文件系统、索引缓存、数据持久化、扫描、导入删除、自动化轮询、调试日志、迁移
- 技术：`vscode` API + Node `fs/promises` + 并发队列

2. Webview Panel（React + TS）
- 职责：UI 展示与交互（原多页面改为单 Panel 内多 Tab/路由）
- 通信：`postMessage` + typed RPC 协议

3. 共享契约层（shared types）
- 职责：请求/响应类型、实体 DTO、错误码、事件类型

---

## 4. 功能映射（WPF -> VS Code）

1. 技能库
- 保留：搜索、分组筛选、多选、批量加分组、删除、打开目录、详情
- 变更：移除翻译按钮/状态/缓存相关 UI 与逻辑

2. 扫描导入
- 保留：指定路径扫描、取消扫描、导入单个/全部
- 变更：去掉全盘扫描按钮；增加“工作区路径”快捷选择

3. 项目管理
- 保留：项目列表、创建项目（带技能区预览）、项目详情、技能区增删、技能加入技能区/全项目、查看 `SKILL.md`
- 保留数据文件：`projects.json`、`skill_index_{id}.json`、`expand_states_{id}.json`

4. 下载链接
- 完整保留：增删查、批量选择删除、打开链接

5. 清理
- 保留：保护区、扫描候选、批量删除
- 变更：清理扫描源改为“工作区 + 用户配置路径”，不再全盘

6. 自动化
- 完整保留：监控路径、立即执行、轮询间隔、启动/停止轮询、导入日志
- 细节：轮询定时器由扩展主进程维护，`deactivate` 时自动释放

7. 设置
- 保留：工具配置页（但主题切换改为“跟随 VS Code 主题”展示，不再单独控制应用主题）

8. 调试
- 保留能力但换形态：`DebugWindow` 改为 Webview 内“调试页”+ `OutputChannel` 双通道日志

---

## 5. 数据与存储设计

统一存到扩展全局目录：`context.globalStorageUri` 下 `skill-manager/`。

目录布局：

1. `library/`（技能目录）
2. `library/.library_index.json`
3. `library/.groups_index.json`
4. `projects.json`
5. `skill_index_{projectId}.json`
6. `expand_states_{projectId}.json`
7. `download_links.json`
8. `skill_manager_settings.json`
9. `debug_logs.json`（新增，可选）

翻译相关文件不再读写：

1. `.translation_cache.json`
2. `.translation_meta.json`
3. `translations/skill_translations.json`
4. 所有翻译模型目录

---

## 6. 自动迁移方案（首次激活）

首次激活且目标存储为空时执行：

1. 发现旧数据源
- 优先读取配置 `skillManager.legacyDataPath`
- 未配置则扫描候选路径（Windows 常见目录）并自动选“最近修改时间最新且结构完整”的目录

2. 迁移内容
- 复制上节列出的非翻译数据文件与 `library` 技能目录
- 忽略翻译缓存与翻译元数据

3. 安全策略
- 先写入 `migration_backup_{timestamp}` 再覆盖
- 写入 `migration_state.json` 防重复迁移
- 迁移失败回滚并给出错误详情

---

## 7. 对外接口与类型（重要）

### 7.1 VS Code Commands（公开）

1. `skillManager.openPanel`
2. `skillManager.refreshLibrary`
3. `skillManager.runAutomationNow`
4. `skillManager.openDebugOutput`
5. `skillManager.runMigration`

### 7.2 VS Code 配置项（公开）

1. `skillManager.legacyDataPath: string`
2. `skillManager.scan.defaultRoots: string[]`
3. `skillManager.cleanup.scanRoots: string[]`
4. `skillManager.automation.autoStartOnActivate: boolean`
5. `skillManager.automation.pollingIntervalSeconds: number`
6. `skillManager.debug.maxLogEntries: number`

### 7.3 Webview RPC 协议（公开）

统一结构：

- `UiRequest { id, method, params }`
- `UiResponse { id, ok, result?, error? }`
- `UiEvent { event, payload }`

核心 method 清单：

1. `library.list|refresh|delete|openFolder|getDetail`
2. `groups.list|create|delete|setSkillGroups|addSkillsToGroups`
3. `scan.scanPath|cancel|importOne|importAll`
4. `projects.list|create|delete|getDetail|addZone|deleteZone|addSkillToZone|addSkillToProject|deleteSkillFromZone|deleteSkillFromProject|openPath`
5. `downloads.list|add|delete|deleteSelected|open`
6. `cleanup.getProtectedPaths|setProtectedPaths|scan|deleteSelected|deleteAll`
7. `automation.getConfig|setConfig|runNow|startPolling|stopPolling|getLogs|clearLogs`
8. `settings.get|set`
9. `debug.getOptions|setOption|getLogs|clear|copy`

---

## 8. 代码结构与实施步骤

### Phase 1：扩展骨架与通信

1. 建立扩展项目（`yo code` + TS）
2. 搭建 Webview Panel 生命周期
3. 建立 shared contracts + message router
4. 打通“前端请求 -> 主进程响应”基础链路

### Phase 2：服务层移植（无翻译）

1. 逐个迁移 C# 服务到 TS：
- `LibraryService`
- `GroupService`
- `SkillScannerService`（去全盘扫描入口）
- `ProjectService`
- `SkillCleanupService`
- `SkillAutomationService`
- `DownloadLinksService`
- `SettingsService`
- `DebugService`
2. 统一错误码和日志
3. 引入并发控制（扫描时避免阻塞）

### Phase 3：Webview UI 页面迁移

1. 页面结构：`Library / Scan / Projects / Downloads / Cleanup / Automation / Settings / Debug`
2. 复刻关键交互（弹窗改为前端 Dialog）
3. 删除所有翻译 UI 元素与状态字段

### Phase 4：迁移与兼容

1. 实现首次自动迁移
2. 实现版本化数据升级器（`schemaVersion`）
3. 补全异常恢复与回滚

### Phase 5：验证与打包

1. 单测 + 集成测试 + 手工回归
2. 产出 `.vsix`
3. 发布前 checklist（权限、性能、数据安全）

---

## 9. 关键边界与失败处理

1. 删除操作二次确认，明确“物理删除不可恢复”
2. 清理/自动化永不操作扩展存储根目录
3. 路径统一标准化（大小写、尾斜杠）避免误判
4. 扫描取消必须可中断并返回部分结果
5. `SKILL.md` 缺失目录不作为有效技能
6. 同名技能冲突按“跳过并记录日志”处理
7. Webview 重载后可恢复当前页面状态与任务状态

---

## 10. 测试用例与验收标准

### 单元测试

1. `SKILL.md` 解析：frontmatter、标题、WhenToUse 规则
2. 扫描去重：同名目录、重复路径、忽略目录
3. 分组操作：创建重名校验、批量加组、移除逻辑
4. 项目索引刷新：增量更新、展开状态持久化
5. 自动化：嵌套路径过滤、导入后源删除策略
6. 迁移：可重复执行、失败回滚、忽略翻译文件

### 集成测试（Extension）

1. `openPanel` 后 UI 成功初始化
2. 扫描->导入->库刷新链路可用
3. 项目增删改与技能区操作全链路可用
4. 清理仅作用于配置扫描根，不触发全盘
5. 轮询启动/停止与日志更新正确
6. 迁移后历史数据可见且可操作

### 验收标准

1. 除翻译外功能覆盖率与原工具一致
2. 不存在全盘扫描入口
3. 首次启动可自动迁移旧数据
4. 所有文件操作在 Windows 环境稳定可用
5. 异常有可读提示，且不会导致数据静默损坏

---

## 11. 明确假设与默认值

1. 默认仅 Windows 支持
2. 默认不启用全盘扫描/清理
3. 默认自动迁移开启，且优先使用 `legacyDataPath`
4. 默认前端 React + TypeScript
5. 默认自动化轮询间隔 `10s`（范围 `5-300s`）
6. 默认最大调试日志条数 `500`
