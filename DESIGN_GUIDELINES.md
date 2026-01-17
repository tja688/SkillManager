# Skill Manager 设计规范

本文档定义了 Skill Manager 应用程序的设计规范，所有后续开发都应遵循这些规范。

## 图标风格

### 主要原则

统一使用 **WPF UI 默认灰色风格**，保持界面简洁一致。

### 图标颜色规范

| 用途 | 颜色资源 | 说明 |
|------|----------|------|
| 列表项图标 | `TextFillColorSecondaryBrush` | 用于卡片、列表项中的图标 |
| 次要/装饰图标 | `TextFillColorTertiaryBrush` | 用于空状态、禁用状态等 |
| 标题栏图标 | 默认（继承文本颜色） | 与标题文字保持一致 |
| 操作按钮图标 | 继承按钮 Appearance | 由 WPF UI 按钮样式控制 |

### 不要使用

- ❌ `AccentTextFillColorPrimaryBrush` - 彩色强调
- ❌ `SystemFillColorSuccessBrush` - 成功绿色
- ❌ `SystemFillColorCautionBrush` - 警告黄色
- ❌ 自定义 Hex 颜色值

### XAML 示例

```xml
<!-- 正确示例：列表项图标 -->
<ui:SymbolIcon
    FontSize="24"
    Foreground="{DynamicResource TextFillColorSecondaryBrush}"
    Symbol="Code24" />

<!-- 正确示例：空状态图标 -->
<ui:SymbolIcon
    FontSize="64"
    Foreground="{DynamicResource TextFillColorTertiaryBrush}"
    Symbol="Box24" />
```

## 按钮风格

### 按钮 Appearance 使用规范

| Appearance | 用途 |
|------------|------|
| `Primary` | 主要操作（如"扫描"、"导入"） |
| `Secondary` | 次要操作（如"查看"、"打开文件夹"） |
| `Info` | 信息性操作（如"全局扫描"） |
| `Success` | 确认性批量操作（如"全部导入"） |
| `Caution` | 需要注意的操作（如"取消"） |
| `Danger` | 破坏性操作（如"删除"） |

## 布局规范

### 间距

- 页面边距：`24px`
- 卡片内边距：`16px`
- 列表项间距：`4px (Margin="0,4")`
- 标题与内容间距：`16px`

### 图标容器

```xml
<Border
    Width="48"
    Height="48"
    Margin="0,0,16,0"
    Background="{DynamicResource ControlFillColorDefaultBrush}"
    CornerRadius="8">
    <ui:SymbolIcon
        FontSize="24"
        Foreground="{DynamicResource TextFillColorSecondaryBrush}"
        Symbol="Code24" />
</Border>
```

## 文本样式

| 用途 | 字体大小 | 字重 | 颜色资源 |
|------|----------|------|----------|
| 页面标题 | 28 | SemiBold | TextFillColorPrimaryBrush |
| 页面副标题 | 默认 | Normal | TextFillColorSecondaryBrush |
| 卡片标题 | 16 | SemiBold | TextFillColorPrimaryBrush |
| 卡片描述 | 默认 | Normal | TextFillColorSecondaryBrush |
| 次要信息 | 默认 | Normal | TextFillColorTertiaryBrush |

---

*最后更新: 2026-01-17*
