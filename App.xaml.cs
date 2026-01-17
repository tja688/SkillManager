using System.Windows;
using SkillManager.Views;
using Wpf.Ui.Appearance;

namespace SkillManager;

/// <summary>
/// App.xaml 的交互逻辑
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 应用系统主题
        ApplicationThemeManager.ApplySystemTheme();

        // 创建并显示主窗口
        var mainWindow = new MainWindow();

        // 监听系统主题变化
        SystemThemeWatcher.Watch(mainWindow);

        mainWindow.Show();
    }
}
