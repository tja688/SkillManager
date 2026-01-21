using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using SkillManager.Views;
using System.IO;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SkillManager.ViewModels;

/// <summary>
/// 主窗口ViewModel
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly SkillScannerService _scannerService;
    private readonly ProjectService _projectService;
    private readonly GroupService _groupService;
    private readonly TranslationService _translationService;
    private readonly SkillManagerSettingsService _settingsService;
    private readonly DownloadLinksService _downloadLinksService;
    private readonly SkillCleanupService _cleanupService;
    private readonly SkillAutomationService _automationService;

    public MainWindowViewModel()
    {
        // 初始化服务
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var libraryPath = Path.Combine(baseDirectory, "library");
        _libraryService = new LibraryService(libraryPath);
        _scannerService = new SkillScannerService(libraryPath);
        _projectService = new ProjectService(baseDirectory, _libraryService);
        _groupService = new GroupService(libraryPath);
        _translationService = CreateTranslationService(baseDirectory, libraryPath);
        var manualTranslationPath = Path.Combine(baseDirectory, "translations", "skill_translations.json");
        var manualTranslationStore = new ManualTranslationStore(manualTranslationPath, libraryPath);
        _settingsService = new SkillManagerSettingsService(baseDirectory);
        _downloadLinksService = new DownloadLinksService(baseDirectory);
        _cleanupService = new SkillCleanupService();
        _automationService = new SkillAutomationService(_scannerService, _libraryService);

        // 初始化子ViewModel
        ScanViewModel = new ScanViewModel(_scannerService, _libraryService);
        LibraryViewModel = new LibraryViewModel(_libraryService, _groupService, _translationService, manualTranslationStore);
        DownloadSkillsViewModel = new DownloadSkillsViewModel(_downloadLinksService);
        CleanupViewModel = new CleanupViewModel(_scannerService, _cleanupService, _settingsService);
        AutomationViewModel = new AutomationViewModel(_automationService, _settingsService);
        AutomationViewModel.AutomationCompleted += () => _ = LibraryViewModel.RefreshSkills();

        // 初始加载
        LibraryViewModel.RefreshSkills();
        _ = AutomationViewModel.RunAutoImportOnStartupAsync();
    }

    public ScanViewModel ScanViewModel { get; }
    public LibraryViewModel LibraryViewModel { get; }
    public DownloadSkillsViewModel DownloadSkillsViewModel { get; }
    public CleanupViewModel CleanupViewModel { get; }
    public AutomationViewModel AutomationViewModel { get; }
    public ProjectService ProjectService => _projectService;
    public GroupService GroupService => _groupService;

    [ObservableProperty]
    private string _applicationTitle = "Skill Manager";

    /// <summary>
    /// 创建项目列表ViewModel
    /// </summary>
    public ProjectListViewModel CreateProjectListViewModel(Action<Project> navigateToDetail)
    {
        return new ProjectListViewModel(_projectService, navigateToDetail);
    }

    /// <summary>
    /// 创建项目详情ViewModel
    /// </summary>
    public ProjectDetailViewModel CreateProjectDetailViewModel(Action navigateBack)
    {
        return new ProjectDetailViewModel(_projectService, navigateBack);
    }

    private static TranslationService CreateTranslationService(string baseDirectory, string libraryPath)
    {
        var cachePath = Path.Combine(libraryPath, ".translation_cache.json");

        // 从配置文件或环境变量读取翻译服务地址
        var translationServiceUrl = Environment.GetEnvironmentVariable("TRANSLATION_SERVICE_URL") 
            ?? "http://localhost:5123";

        // 创建翻译设置
        var settings = new TranslationSettings
        {
            EngineId = "remote-translation",
            EngineVersion = "v1",
            SourceLang = "en",
            TargetLang = "zh-CN",
            MaxConcurrency = 2,
            MaxLength = 256,
            Timeout = TimeSpan.FromSeconds(60),
            EnableTranslation = false
        };

        // 尝试从 .translation_meta.json 读取配置覆盖
        var metaPath = Path.Combine(libraryPath, ".translation_meta.json");
        if (File.Exists(metaPath))
        {
            try
            {
                var json = File.ReadAllText(metaPath);
                var meta = System.Text.Json.JsonSerializer.Deserialize<TranslationMeta>(json);
                if (meta != null)
                {
                    settings.EnableTranslation = !meta.DisableTranslation;
                    if (meta.MaxConcurrency.HasValue) settings.MaxConcurrency = meta.MaxConcurrency.Value;
                    if (meta.MaxLength.HasValue) settings.MaxLength = meta.MaxLength.Value;
                    if (!string.IsNullOrWhiteSpace(meta.EngineVersion)) settings.EngineVersion = meta.EngineVersion;
                }
            }
            catch
            {
                // 忽略配置读取错误
            }
        }

        var protectedTerms = new[]
        {
            "Unity",
            "Unreal Engine",
            "Blender",
            "DOTween",
            "Yarn Spinner",
            "Text Animator",
            "VR",
            "AR",
            "Cinema 4D"
        };

        var mappedPhrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Typewriter"] = "打字机效果",
            ["Per-character"] = "逐字",
            ["Per character"] = "逐字"
        };

        var protector = new TranslationTermProtector(protectedTerms, mappedPhrases);
        var cacheStore = new TranslationCacheStore(cachePath);
        
        // 使用远程翻译服务
        var provider = new RemoteTranslationProvider(translationServiceUrl, (int)settings.Timeout.TotalSeconds);
        
        DebugService.Instance.Log("Translation", $"Using remote translation service: {translationServiceUrl}", "MainWindowViewModel", DebugLogLevel.Info);
        
        return new TranslationService(cacheStore, provider, settings, protector);
    }
}
