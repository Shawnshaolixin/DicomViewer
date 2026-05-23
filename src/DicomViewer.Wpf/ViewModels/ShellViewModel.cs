using DicomViewer.Wpf.Navigation;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace DicomViewer.Wpf.ViewModels;

/// <summary>
/// 主壳 ViewModel，负责在查看器与曝光控制台两个页面之间导航，并更新顶部标题说明。
/// </summary>
public sealed class ShellViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    private string _currentPageTitle = "影像查看器";
    private string _currentPageDescription = "独立查看器页面，负责导入、浏览、测量和基础回看。";
    private bool _isInitialized;

    public ShellViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
        NavigateViewerCommand = new DelegateCommand(NavigateToViewer);
        NavigateExposureConsoleCommand = new DelegateCommand(NavigateToExposureConsole);
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    public string CurrentPageDescription
    {
        get => _currentPageDescription;
        private set => SetProperty(ref _currentPageDescription, value);
    }

    public DelegateCommand NavigateViewerCommand { get; }

    public DelegateCommand NavigateExposureConsoleCommand { get; }

    /// <summary>
    /// 执行一次性初始化，默认导航到查看器页面。
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        NavigateToViewer();
    }

    /// <summary>
    /// 导航到影像查看器页面，并同步标题说明。
    /// </summary>
    private void NavigateToViewer()
    {
        Navigate(ViewNames.ViewerWorkspaceView, "影像查看器", "独立查看器页面，负责导入、浏览、测量和基础回看。");
    }

    /// <summary>
    /// 导航到曝光控制台页面，并同步标题说明。
    /// </summary>
    private void NavigateToExposureConsole()
    {
        Navigate(ViewNames.ExposureConsoleView, "曝光控制台", "独立控制台页面，负责工作列表、联锁、模拟曝光与 DICOM 生成。");
    }

    /// <summary>
    /// 统一封装页面标题和区域导航，避免两个入口重复维护页面元数据。
    /// </summary>
    private void Navigate(string viewName, string title, string description)
    {
        CurrentPageTitle = title;
        CurrentPageDescription = description;
        _regionManager.RequestNavigate(RegionNames.MainContentRegion, viewName);
    }
}