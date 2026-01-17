# Skill Manager

Skill Manager is a Windows Presentation Foundation (WPF) application built with .NET 10, designed to help developers manage, organize, and discover "Agent Skills" (defined by `SKILL.md` files) across their local file system.

It serves as a central repository and management interface for modular coding skills, allowing users to scan for existing skills, import them into a managed library, group them with tags, and associate them with specific projects.

## 🛠 Technology Stack

- **Framework**: .NET 10.0 (Windows)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **UI Component Library**: [WPF-UI](https://wpfui.lepo.co/) (Modern Fluent Design)
- **MVVM Library**: [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)

## 📂 Project Structure & Module Index

The project follows a standard MVVM architecture. Below is a detailed index of the core modules to facilitate rapid navigation and maintenance.

### 1. Core Services (`/Services`)
Business logic and data manipulation layer.

| File | Description |
|------|-------------|
| `SkillScannerService.cs` | Handles recursive file system scanning to discover folders containing `SKILL.md`. Includes logic to exclude existing library items. |
| `LibraryService.cs` | Manages the local skill library ("repository"). Handles importing, deleting, and updating metadata of skills stored in the `library/` folder. |
| `ProjectService.cs` | Manages User "Projects" (Workspaces). Handles logic for associating skills with specific working directories. |
| `GroupService.cs` | Manages the tagging/grouping system. persistent storage of `SkillGroup` data and their relationships to skills. |

### 2. Data Models (`/Models`)
Data entities and transfer objects.

| File | Description |
|------|-------------|
| `SkillFolder.cs` | The core entity representing a Skill. Properties include Path, Name, Description, and its status (InLibrary, etc.). |
| `Project.cs` | Represents a user's local project/workspace. Contains a list of standard Agent Directories. |
| `SkillGroup.cs` | Defines a Tag/Group (e.g., "UI Tools", "Backend") including its color and ID. |
| `ScanResult.cs` | Usage in scanning operations to return found items and statistics. |
| `SkillIndexModels.cs` | Lightweight JSON structures for indexing skills to optimize performance. |

### 3. View Models (`/ViewModels`)
The glue between Logic and UI, handling state and commands.

| File | Description |
|------|-------------|
| `MainWindowViewModel.cs` | Navigation state and global commands for the main application shell. |
| `ScanViewModel.cs` | Logic for the "Scan" page: handling directory selection, progress reporting, and import commands. |
| `LibraryViewModel.cs` | Logic for the "Library" page: filtering, grouping display, and searching skills. |
| `ProjectListViewModel.cs` | Logic for displaying the list of all managed projects. |
| `ProjectDetailViewModel.cs` | Logic for a single project view, showing its associated skills and agent directories. |

### 4. Views (`/Views`)
XAML definitions for the User Interface.

#### Main Pages
| File | Description |
|------|-------------|
| `MainWindow.xaml` | The main application window shell (Navigation + Frame). |
| `ScanPage.xaml` | Page for scanning directories and importing new skills. |
| `LibraryPage.xaml` | The main grid/list view of all imported skills. |
| `ProjectListPage.xaml` | Dashboard showing all configured projects. |
| `ProjectDetailPage.xaml` | Detailed view for managing a specific project. |
| `SettingsPage.xaml` | Application settings. |

#### Dialogs
| File | Description |
|------|-------------|
| `AddProjectDialog.xaml` | Modal to register a new project path. |
| `ManageGroupsDialog.xaml` | Modal to Create/Edit/Delete skill groups (tags). |
| `AddSkillsToGroupsDialog.xaml` | Batch operation dialog to assign groups to multiple skills. |
| `SelectSkillDialog.xaml` | Dialog to pick a skill from the library (e.g. adding to a project). |
| `SkillDetailDialog.xaml` | Popup showing full details of a skill. |

### 5. Utilities & Assets
| Directory/File | Description |
|----------------|-------------|
| `/Converters` | XAML binding converters (e.g., BooleanToVisibility, StatusToColor). |
| `/Assets` | Images and static resources. |
| `DESIGN_GUIDELINES.md` | **Critical**: Contains the official design rules for Icons, Colors, and Typography. |

## 🚀 Key Workflows

### Skill Discovery
1.  User initiates scan in `ScanPage`.
2.  `ScanViewModel` triggers `SkillScannerService`.
3.  Results are displayed; User selects skills to "Import".
4.  `LibraryService` copies files to the local `library/` folder.

### Group Management
1.  User accesses Group Manager.
2.  `GroupService` acts as the CRUD backend.
3.  Associations are stored in `.groups_index.json` (managed by `GroupService` inside the library folder).

## ⚠️ Development Notes

*   **Design Compliance**: Strictly follow `DESIGN_GUIDELINES.md` when adding new UI elements. Use standard WPF UI brushes (e.g., `TextFillColorSecondaryBrush`) and icons.
*   **Performance**: The `library/` folder can grow large. `SkillIndexModels` suggests an indexing strategy is in place or planned to avoid expensive disk I/O on every startup.
*   **Navigation**: Navigation flows through `MainWindowViewModel` using the WPF UI `NavigationService`.

---
*Generated by SkillManager Analysis Tool*
