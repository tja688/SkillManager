# SkillManager

> 🤖 **AI-READABLE PROJECT DOCUMENTATION**  
> 本文档针对 AI Agent / LLM 优化，采用结构化表达以便于智能体快速理解项目架构、定位代码、执行开发任务。

---

## 📋 PROJECT METADATA

| 属性 | 值 |
|------|-----|
| **项目名称** | SkillManager |
| **项目类型** | Windows 桌面应用程序 |
| **技术栈** | .NET 10, WPF, WPF-UI 3.0.5, CommunityToolkit.Mvvm 8.4.0 |
| **架构模式** | MVVM (Model-View-ViewModel) |
| **主要用途** | AI 技能库管理工具，用于扫描、导入、组织和管理 `SKILL.md` 格式的 AI 技能定义文件 |
| **项目路径** | `c:\Users\jinji\Tools\SkillManager` |

---

## 🎯 CORE CONCEPTS (核心概念)

### 什么是 "Skill" (技能)?

在本项目语境中，**Skill** 是一个包含 `SKILL.md` 文件的文件夹。`SKILL.md` 遵循标准化的 Markdown 格式，包含：

```yaml
---
description: 技能的简短描述
---
# 技能标题

## When to Use / 使用场景
描述该技能的适用场景...

## 其他章节...
```

### 核心实体关系

```
┌─────────────────────────────────────────────────────────────┐
│                        SkillManager                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐    contains     ┌─────────────────────┐   │
│  │   Library    │ ───────────────>│   SkillFolder       │   │
│  │  (技能库)     │                 │  • Name             │   │
│  └──────────────┘                 │  • FullPath         │   │
│         ↑                         │  • SKILL.md         │   │
│         │ import                  │  • Description      │   │
│         │                         │  • SkillTitle       │   │
│  ┌──────────────┐                 │  • WhenToUse        │   │
│  │    Scan      │                 └─────────────────────┘   │
│  │  (扫描服务)   │                          │                │
│  └──────────────┘                          │ belongs to     │
│                                            ↓                │
│  ┌──────────────┐    references   ┌─────────────────────┐   │
│  │   Project    │ ───────────────>│   SkillGroup        │   │
│  │  (开发项目)   │                 │  • Id               │   │
│  │  • SkillZone │                 │  • Name             │   │
│  │    (.claude) │                 │  • SkillNames[]     │   │
│  │    (.agent)  │                 └─────────────────────┘   │
│  └──────────────┘                                           │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 📁 PROJECT STRUCTURE (项目结构)

```
SkillManager/
├── App.xaml                    # 应用程序入口配置，定义全局资源和主题
├── App.xaml.cs                 # 应用程序启动逻辑
├── SkillManager.csproj         # 项目配置文件 (.NET 10, WPF)
│
├── Models/                     # 🧩 数据模型层
│   ├── SkillFolder.cs          # 技能文件夹模型 (核心实体)
│   ├── SkillGroup.cs           # 技能分组模型
│   ├── SkillIndexModels.cs     # 索引模型 (用于缓存加速)
│   ├── Project.cs              # 项目模型 (包含 SkillZone)
│   └── ScanResult.cs           # 扫描结果模型
│
├── Services/                   # ⚙️ 业务服务层
│   ├── LibraryService.cs       # 技能库管理服务 (CRUD + 索引缓存)
│   ├── SkillScannerService.cs  # 技能扫描服务 (高性能并行扫描)
│   ├── ProjectService.cs       # 项目管理服务
│   ├── GroupService.cs         # 分组管理服务
│   └── DebugService.cs         # 调试服务 (单例模式)
│
├── ViewModels/                 # 🎭 视图模型层
│   ├── MainWindowViewModel.cs  # 主窗口 ViewModel (服务聚合)
│   ├── LibraryViewModel.cs     # 技能库 ViewModel
│   ├── ScanViewModel.cs        # 扫描页 ViewModel
│   ├── ProjectListViewModel.cs # 项目列表 ViewModel
│   └── ProjectDetailViewModel.cs # 项目详情 ViewModel
│
├── Views/                      # 🖼️ 视图层
│   ├── MainWindow.xaml(.cs)    # 主窗口 (FluentWindow + NavigationView)
│   ├── LibraryPage.xaml(.cs)   # 技能库页面
│   ├── ScanPage.xaml(.cs)      # 扫描导入页面
│   ├── ProjectListPage.xaml(.cs)     # 项目列表页面
│   ├── ProjectDetailPage.xaml(.cs)   # 项目详情页面
│   ├── SettingsPage.xaml(.cs)  # 设置页面
│   ├── SkillNavigationView.cs  # 自定义导航视图
│   ├── AllSkillsPage.cs        # 所有技能页面
│   ├── DebugWindow.xaml(.cs)   # 调试窗口
│   ├── SkillDetailDialog.xaml(.cs)   # 技能详情对话框
│   ├── ManageGroupsDialog.xaml(.cs)  # 分组管理对话框
│   ├── ManageSkillGroupsDialog.xaml(.cs) # 技能分组管理对话框
│   ├── AddSkillsToGroupsDialog.xaml(.cs) # 批量添加到分组对话框
│   ├── SelectSkillDialog.xaml(.cs)   # 技能选择对话框
│   ├── AddProjectDialog.xaml(.cs)    # 添加项目对话框
│   └── AddSkillZoneDialog.xaml(.cs)  # 添加技能区对话框
│
├── Converters/                 # 🔄 值转换器
│   └── Converters.cs           # 包含所有 IValueConverter 实现
│
├── Assets/                     # 🎨 静态资源
│
└── library/                    # 📚 技能库存储目录 (运行时数据)
    ├── .library_index.json     # 技能库索引文件 (缓存)
    ├── .groups_index.json      # 分组索引文件
    └── [skill-folders]/        # 各技能文件夹
```

---

## 🏗️ ARCHITECTURE DETAILS (架构详解)

### 依赖关系图

```
┌─────────────────────────────────────────────────────────────────┐
│                           VIEWS LAYER                            │
│  MainWindow → NavigationView → Pages (Library/Scan/Project/...)  │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ DataContext binding
                                  ↓
┌─────────────────────────────────────────────────────────────────┐
│                        VIEWMODEL LAYER                           │
│  MainWindowViewModel ────┬── LibraryViewModel                    │
│       │                  ├── ScanViewModel                       │
│       │                  ├── ProjectListViewModel                │
│       │                  └── ProjectDetailViewModel              │
│       │                                                          │
│  (Each ViewModel uses CommunityToolkit.Mvvm for:                 │
│   - [ObservableProperty] for property notification               │
│   - [RelayCommand] for command binding)                          │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ Service injection
                                  ↓
┌─────────────────────────────────────────────────────────────────┐
│                        SERVICES LAYER                            │
│  LibraryService ←───── SkillScannerService                       │
│       ↓                       ↓                                  │
│  GroupService          ProjectService                            │
│       ↓                       ↓                                  │
│  DebugService (Singleton - global debug logging)                 │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ File I/O + JSON persistence
                                  ↓
┌─────────────────────────────────────────────────────────────────┐
│                         DATA LAYER                               │
│  library/                                                        │
│   ├── .library_index.json    (LibrarySkillIndex)                 │
│   ├── .groups_index.json     (SkillGroupIndex)                   │
│   └── [skill-folder]/SKILL.md                                    │
│                                                                  │
│  projects.json               (List<Project>)                     │
│  skill_index_{projectId}.json (ProjectSkillIndex)                │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📊 MODELS REFERENCE (模型参考)

### SkillFolder (技能文件夹 - 核心实体)

| 属性 | 类型 | 描述 | 持久化 |
|------|------|------|--------|
| `Name` | `string` | 文件夹名称 | ✅ |
| `FullPath` | `string` | 完整路径 | ✅ |
| `Description` | `string` | SKILL.md frontmatter 中的 description | ✅ |
| `SkillTitle` | `string` | SKILL.md 中的一级标题 `# Title` | ✅ |
| `WhenToUse` | `string` | "When to Use" 段落内容 | ✅ |
| `CreatedTime` | `DateTime` | 创建时间 | ✅ |
| `IsInLibrary` | `bool` | 是否在库中 | ❌ (运行时) |
| `IsExpanded` | `bool` | UI 展开状态 | ❌ (运行时) |
| `IsSelected` | `bool` | UI 多选状态 | ❌ (运行时) |
| `GroupNamesDisplay` | `string` | 所属分组显示文本 | ❌ (运行时) |
| `SkillMdPath` | `string` | SKILL.md 文件路径 (计算属性) | ❌ |

### SkillGroup (技能分组)

| 属性 | 类型 | 描述 |
|------|------|------|
| `Id` | `string` | 分组 GUID |
| `Name` | `string` | 分组名称 |
| `CreatedTime` | `DateTime` | 创建时间 |
| `SkillNames` | `List<string>` | 分组包含的技能名称列表 |
| `IsSelected` | `bool` | UI 选中状态 |

### Project (项目)

| 属性 | 类型 | 描述 |
|------|------|------|
| `Id` | `string` | 项目 GUID |
| `Name` | `string` | 项目名称 |
| `Path` | `string` | 项目路径 |
| `CreatedTime` | `DateTime` | 创建时间 |
| `SkillZones` | `ObservableCollection<SkillZone>` | 技能区列表 (运行时) |
| `TotalSkillCount` | `int` | 总技能数 (计算属性) |

### SkillZone (技能区 - 如 .claude, .agent)

| 属性 | 类型 | 描述 |
|------|------|------|
| `Name` | `string` | 技能区名称 (如 `.claude`) |
| `FullPath` | `string` | 技能区完整路径 |
| `SkillsFolderPath` | `string` | 内部 skills 文件夹路径 (计算属性) |
| `Skills` | `ObservableCollection<SkillFolder>` | 技能列表 |
| `IsExpanded` | `bool` | UI 展开状态 |

---

## ⚙️ SERVICES REFERENCE (服务参考)

### LibraryService

**职责**: 技能库 CRUD + 索引缓存管理

**关键方法**:

| 方法 | 签名 | 描述 |
|------|------|------|
| `GetAllSkillsAsync` | `Task<List<SkillFolder>>(bool forceRefresh = false)` | 获取所有技能 (带缓存) |
| `ImportSkillAsync` | `Task<bool>(SkillFolder skill, IProgress<string>? progress)` | 导入单个技能 |
| `ImportSkillsAsync` | `Task<int>(IEnumerable<SkillFolder> skills, IProgress<string>? progress)` | 批量导入 |
| `DeleteSkillAsync` | `Task<bool>(SkillFolder skill, IProgress<string>? progress)` | 删除技能 |
| `OpenSkillFolder` | `void(SkillFolder skill)` | 打开文件夹 |

**索引缓存策略**:
- 首次加载: 尝试读取 `.library_index.json`，无则全量扫描
- 后续访问: 使用内存缓存 `_cachedIndex`
- 后台刷新: 静默增量刷新 (对比 `LastWriteTimeUtc`)

### SkillScannerService

**职责**: 高性能并行扫描文件系统查找技能

**关键方法**:

| 方法 | 签名 | 描述 |
|------|------|------|
| `ScanAsync` | `Task<ScanResult>(string rootPath, IProgress<string>?, CancellationToken)` | 扫描指定目录 |
| `ScanGlobalAsync` | `Task<ScanResult>(IProgress<string>?, CancellationToken)` | 全局驱动器扫描 |
| `GetExistingLibrarySkillNames` | `HashSet<string>()` | 获取库中已有技能名 |

**性能优化**:
- 使用 `Parallel.ForEach` + `ConcurrentBag` 并行扫描
- `ConcurrentDictionary` 追踪已扫描路径避免重复
- 排除系统目录: `$Recycle.Bin`, `Windows`, `node_modules`, `.git` 等

### ProjectService

**职责**: 项目管理 (项目 = 包含 `.claude/.agent` 等技能区的开发项目)

**关键方法**:

| 方法 | 签名 | 描述 |
|------|------|------|
| `GetAllProjects` | `List<Project>()` | 获取所有项目 |
| `CreateProjectAsync` | `Task<Project>(string name, string path)` | 创建项目 |
| `LoadSkillZonesAsync` | `Task(Project project)` | 加载项目技能区 |
| `RefreshProjectSkillsAsync` | `Task(Project project)` | 增量刷新项目技能 |
| `AddSkillToZoneAsync` | `Task<bool>(SkillZone zone, SkillFolder skill)` | 添加技能到技能区 |
| `DeleteSkillFromZoneAsync` | `Task<bool>(SkillZone zone, SkillFolder skill)` | 删除技能区技能 |

### GroupService

**职责**: 技能分组管理

**关键方法**:

| 方法 | 签名 | 描述 |
|------|------|------|
| `GetAllGroupsAsync` | `Task<List<SkillGroup>>()` | 获取所有分组 |
| `CreateGroupAsync` | `Task<SkillGroup>(string name)` | 创建分组 |
| `DeleteGroupAsync` | `Task<bool>(string groupId)` | 删除分组 |
| `AddSkillToGroupAsync` | `Task<bool>(string skillName, string groupId)` | 添加技能到分组 |
| `AddSkillsToGroupsAsync` | `Task<int>(IEnumerable<string> skillNames, IEnumerable<string> groupIds)` | 批量添加 |
| `RemoveSkillFromGroupAsync` | `Task<bool>(string skillName, string groupId)` | 从分组移除 |
| `GetSkillsInGroupAsync` | `Task<HashSet<string>>(string groupId)` | 获取分组内技能 |

### DebugService (Singleton)

**职责**: 调试日志和状态追踪

**获取实例**: `DebugService.Instance`

**关键方法**:

| 方法 | 描述 |
|------|------|
| `Log(category, message, source, level)` | 记录调试日志 |
| `LogIfEnabled(optionId, ...)` | 条件日志 (仅当选项启用时记录) |
| `IsOptionEnabled(optionId)` | 检查调试选项是否启用 |
| `TrackGlobalMouseWheel(...)` | 追踪全局滚轮事件 |
| `TrackScrollViewerWheel(...)` | 追踪 ScrollViewer 滚轮 |
| `TrackViewModelState(...)` | 追踪 ViewModel 状态变化 |
| `TrackCardRender(...)` | 追踪卡片渲染 |
| `TrackCardStyle(...)` | 检查卡片样式 |

**内置调试选项**:
- `scroll_global_routing` - 全局鼠标滚轮路由追踪
- `scroll_control_intercept` - 控件滚轮拦截检测
- `scroll_viewmodel_state` - ViewModel 状态检查
- `scroll_visual_tree` - 可视化树结构追踪
- `scroll_focus_tracking` - 焦点状态追踪
- `scroll_scrollable_height` - ScrollViewer 可滚动高度追踪
- `card_render_tracking` - 卡片渲染追踪
- `card_style_inspection` - 卡片样式检查
- `card_layout_tracking` - 卡片布局追踪
- `card_resource_resolution` - 卡片资源解析追踪

---

## 🔄 CONVERTERS REFERENCE (转换器参考)

| 转换器类名 | 用途 | 参数 |
|-----------|------|------|
| `BoolToInverseConverter` | 布尔值取反 | - |
| `BoolToVisibilityConverter` | 布尔值转可见性 | `"Invert"` 取反 |
| `CountToVisibilityConverter` | 0 显示，>0 隐藏 (空状态提示) | - |
| `CountToBoolConverter` | >0 为 true | - |
| `BoolToChevronConverter` | 布尔值转展开/收起箭头符号 | - |
| `StringToVisibilityConverter` | 非空字符串显示 | - |
| `WidthToColumnsConverter` | 宽度自适应计算列数 (卡片布局) | 最小卡片宽度 (默认 280) |

---

## 🛠️ DEVELOPMENT GUIDE (开发指南)

### 构建与运行

```powershell
# 进入项目目录
cd c:\Users\jinji\Tools\SkillManager

# 构建项目
dotnet build

# 运行项目
dotnet run
```

### 添加新功能的标准流程

1. **Model**: 在 `Models/` 中定义数据模型，继承 `ObservableObject`
2. **Service**: 在 `Services/` 中实现业务逻辑
3. **ViewModel**: 在 `ViewModels/` 中创建 ViewModel，使用 `[ObservableProperty]` 和 `[RelayCommand]`
4. **View**: 在 `Views/` 中创建 XAML 页面，设置 `DataContext` 绑定

### 常见开发任务

#### 添加新的技能属性解析

修改 `LibraryService.ParseSkillInfo()` 方法，该方法负责从 `SKILL.md` 提取信息：

```csharp
// 位置: Services/LibraryService.cs
private (string Description, string SkillTitle, string WhenToUse) ParseSkillInfo(string skillMdPath)
{
    // 1. 解析 YAML frontmatter (--- ... ---)
    // 2. 解析一级标题 (# Title)
    // 3. 解析二级标题段落 (## When to Use 等)
}
```

#### 添加新的页面

1. 创建 `Views/NewPage.xaml` 和 `Views/NewPage.xaml.cs`
2. 在 `MainWindow.xaml` 的 `NavigationView.MenuItems` 中添加 `NavigationViewItem`
3. 如需 ViewModel，在 `ViewModels/` 创建对应的 ViewModel

#### 添加新的调试选项

修改 `DebugService.InitializeDebugOptions()` 方法：

```csharp
// 位置: Services/DebugService.cs
DebugOptions.Add(new DebugOption
{
    Id = "your_option_id",
    Name = "选项显示名称",
    Description = "选项描述",
    Category = "调试类别"
});
```

---

## 📝 SKILL.md FORMAT SPECIFICATION (技能文件格式规范)

```markdown
---
description: 简短的技能描述 (一行)
---

# 技能标题

## When to Use / 使用场景

描述何时应该使用此技能...

## Overview / 概述

技能的详细说明...

## 其他自定义章节

...
```

**解析优先级**:
1. `description` 从 YAML frontmatter 提取
2. `skillTitle` 从第一个 `# ` 标题提取
3. `whenToUse` 从以下二级标题段落提取 (按优先级):
   - `## When to Use`
   - `## Overview`
   - `## About`
   - `## 使用场景`
   - `## 能做什么`
   - `## 功能`

---

## 🔍 TROUBLESHOOTING (常见问题)

### 性能问题

**症状**: 切换到技能库页面卡顿

**解决方案**:
1. 检查 `.library_index.json` 是否存在
2. 调用 `LibraryService.GetAllSkillsAsync(forceRefresh: true)` 重建索引
3. 检查 `library/` 目录下技能数量

### ScrollViewer 滚动问题

**症状**: 鼠标滚轮不响应

**调试方法**:
1. 打开 Debug 窗口 (标题栏 Bug 按钮)
2. 启用 `scroll_global_routing` 和 `scroll_control_intercept` 选项
3. 观察日志中的事件路由情况

### 卡片样式问题

**症状**: 技能卡片背景不显示

**调试方法**:
1. 启用 `card_style_inspection` 和 `card_resource_resolution` 选项
2. 检查 `Background` 和 `BorderBrush` 是否为 `NULL`
3. 验证 `DynamicResource` 是否正确解析

---

## 📚 DEPENDENCIES (依赖项)

| 包名 | 版本 | 用途 |
|------|------|------|
| `WPF-UI` | 3.0.5 | Fluent Design UI 组件库 |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM 工具包 (源生成器) |
| `Microsoft.ML.OnnxRuntime` | 1.18.0 | ONNX 推理引擎（离线翻译） |
| `Microsoft.ML.Tokenizers` | 0.22.0 | Tokenizer 支持（Marian/Opus-MT） |

---

## 🔗 KEY FILE PATHS (关键文件路径)

| 路径 | 描述 |
|------|------|
| `library/` | 技能库存储目录 |
| `library/.library_index.json` | 技能库索引缓存 |
| `library/.groups_index.json` | 分组索引 |
| `library/.translation_cache.json` | 翻译缓存 |
| `library/.translation_meta.json` | 翻译配置（可选） |
| `models/translation/` | 翻译模型目录 |
| `projects.json` | 项目列表持久化 |
| `skill_index_{projectId}.json` | 项目技能索引 |
| `expand_states_{projectId}.json` | 项目 UI 展开状态 |

---

## ?? OFFLINE TRANSLATION CACHE (离线翻译缓存)

### 功能概览
- 技能库卡片的 `WhenToUse` / `Description` 自动做英→中离线翻译并缓存。
- 批量预翻译：点击“批量预翻译”按钮，后台执行，支持取消。
- 增量翻译：刷新索引后，自动翻译新增/变更条目。
- 失败不阻塞：翻译失败时仍显示原文，并记录失败原因。

### 模型目录
默认读取 `models/translation/`：
- `encoder_model.onnx`
- `decoder_model.onnx`
- `tokenizer.model` / `tokenizer.spm`
- `engine.version`（可选，字符串版本号）
- `model.config.json`（可选，覆盖模型配置）

`model.config.json` 示例：
```json
{
  "SourcePrefix": ">>cmn_Hans<<",
  "BosTokenId": 0,
  "EosTokenId": 2,
  "PadTokenId": 1,
  "DecoderStartTokenId": 0,
  "MaxLength": 96
}
```

### 翻译开关与参数
在 `library/.translation_meta.json` 可选覆盖：
```json
{
  "DisableTranslation": false,
  "MaxConcurrency": 1,
  "MaxLength": 96,
  "EngineVersion": "v1"
}
```

### 最小 Demo
1. 准备 3~5 个包含英文描述的技能放入 `library/`。
2. 将 Marian/Opus-MT 模型放入 `models/translation/`。
3. 打开应用 → “技能库” → 点击“批量预翻译”。
4. 翻译完成后重启应用，卡片应直接显示中文（命中缓存）。

---

## 🤖 AI AGENT INSTRUCTIONS (AI 智能体操作指南)

### 快速定位代码

| 任务 | 定位文件 |
|------|----------|
| 修改技能卡片样式 | `Views/LibraryPage.xaml` → `DataTemplate DataType="SkillFolder"` |
| 修改技能解析逻辑 | `Services/LibraryService.cs` → `ParseSkillInfo()` |
| 添加新的扫描排除目录 | `Services/SkillScannerService.cs` → `SystemExcludedNames` |
| 修改导航菜单 | `Views/MainWindow.xaml` → `NavigationView.MenuItems` |
| 添加新的 ViewModel 属性 | 对应 ViewModel 文件 → 使用 `[ObservableProperty]` |
| 添加新的命令 | 对应 ViewModel 文件 → 使用 `[RelayCommand]` |
| 修改主题/全局样式 | `App.xaml` → `Application.Resources` |
| 添加新的转换器 | `Converters/Converters.cs` |

### 编码规范

```csharp
// 使用 CommunityToolkit.Mvvm 源生成器
[ObservableProperty]
private string _propertyName = string.Empty;  // 私有字段以 _ 开头，生成 PropertyName 属性

[RelayCommand]
public async Task DoSomethingAsync()  // 生成 DoSomethingCommand
{
    // ...
}

// partial void 用于属性变更回调
partial void OnPropertyNameChanged(string oldValue, string newValue)
{
    // 属性变更时自动调用
}
```

### 数据流

```
用户操作 → View (XAML Binding) 
         → ViewModel (Command) 
         → Service (Business Logic) 
         → File I/O (JSON Persistence)
         → Service (Update Cache)
         → ViewModel (Update Property)
         → View (Auto Refresh via Binding)
```

---

## 📄 LICENSE

MIT License

---

*最后更新: 2026-01-20*
