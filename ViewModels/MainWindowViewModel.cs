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

        // 初始化子ViewModel
        ScanViewModel = new ScanViewModel(_scannerService, _libraryService);
        LibraryViewModel = new LibraryViewModel(_libraryService, _groupService, _translationService);

        // 初始加载
        LibraryViewModel.RefreshSkills();
    }

    public ScanViewModel ScanViewModel { get; }
    public LibraryViewModel LibraryViewModel { get; }
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
        var modelDirectory = Path.Combine(baseDirectory, "models", "translation");
        var (settings, modelConfig) = TranslationSettingsLoader.Load(modelDirectory, libraryPath);
        var cachePath = Path.Combine(libraryPath, ".translation_cache.json");

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
        var provider = new OnnxMarianProvider(modelConfig);
        return new TranslationService(cacheStore, provider, settings, protector);
    }
}
