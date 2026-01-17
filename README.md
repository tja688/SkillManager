# SkillManager

一个用于管理 AI Agent 技能（SKILL.md）的 WPF 桌面应用程序。

## 功能特性

- **技能库管理**：导入、查看、删除技能
- **技能分组**：创建分组、按分组筛选、一个技能可加入多个分组
- **扫描导入**：扫描指定目录或全局搜索包含 `SKILL.md` 的文件夹
- **项目管理**：管理多个项目的技能区
- **快速搜索**：基于名称和描述的即时搜索

## 技术栈

- .NET 10 / WPF
- WPF UI (现代 Fluent Design)
- CommunityToolkit.Mvvm

---

## 升级日志

### v1.2.0 - 分组管理功能 (2026-01-17)

#### 📁 技能分组管理

**新功能**：为技能库添加分组管理功能，支持按分组筛选技能

**功能说明**：

1. **分组管理**
   - 在搜索框右侧添加「管理分组」按钮
   - 可创建自定义分组（如：常用、开发工具、文档编写等）
   - 可删除不需要的分组（技能不会被删除，只移除关联）

2. **分组筛选**
   - 工具栏新增分组下拉框，默认显示「全部」
   - 选择分组后，技能库只显示该分组内的技能
   - 分组筛选状态在软件运行期间保持（切换页面不会重置）

3. **技能分组关联**
   - 每个技能条目右侧新增「管理分组」按钮
   - 一个技能可以加入多个分组
   - 可随时将技能从分组中移除

4. **删除技能优化**
   - 删除技能时增加确认提示
   - 物理删除技能文件夹
   - 自动从所有关联分组中移除

**改动文件**：
- `Models/SkillGroup.cs` - 新增分组数据模型
- `Services/GroupService.cs` - 新增分组管理服务
- `Views/ManageGroupsDialog.xaml(.cs)` - 新增管理分组对话框
- `Views/ManageSkillGroupsDialog.xaml(.cs)` - 新增技能分组管理对话框
- `ViewModels/LibraryViewModel.cs` - 添加分组过滤和管理命令
- `ViewModels/MainWindowViewModel.cs` - 注入 GroupService
- `Views/LibraryPage.xaml` - 添加分组管理 UI 元素

**数据存储**：
- 分组索引持久化到 `library/.groups_index.json`

---

### v1.1.0 - 性能优化 (2026-01-17)

#### 🚀 技能库页面性能优化

**问题**：切换到「技能库」页面时出现明显卡顿

**原因**：每次页面加载都会同步执行磁盘扫描（`Directory.GetDirectories`、`File.ReadAllLines` 等），阻塞 UI 线程

**解决方案**：

1. **JSON 索引缓存**
   - 技能索引持久化到 `.library_index.json`
   - 页面加载时优先读取索引（毫秒级）
   - 后台静默增量刷新，不阻塞 UI

2. **异步加载**  
   - `LibraryService.GetAllSkillsAsync()` 替代同步方法
   - 页面 `Loaded` 事件改为 `async`

3. **加载指示器**
   - 新增 `IsLoading` 属性
   - UI 显示加载动画，提升用户体验

4. **内存搜索**
   - 搜索过滤在内存中完成
   - 不触发额外的磁盘扫描

**改动文件**：
- `Models/SkillIndexModels.cs` - 新增 `LibrarySkillIndex` 类
- `Services/LibraryService.cs` - 重构为异步索引缓存模式
- `ViewModels/LibraryViewModel.cs` - 添加 `IsLoading` 和异步刷新
- `Views/LibraryPage.xaml` - 添加加载指示器
- `Views/LibraryPage.xaml.cs` - 异步加载

---

## 开发

```powershell
# 编译
dotnet build

# 运行
dotnet run
```
