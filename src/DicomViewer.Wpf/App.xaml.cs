using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Services;
using DicomViewer.Infrastructure.Data;
using DicomViewer.Infrastructure.Dicom;
using DicomViewer.Infrastructure.Imaging;
using DicomViewer.Infrastructure.Pacs;
using DicomViewer.Infrastructure.Persistence;
using DicomViewer.Infrastructure.Services;
using DicomViewer.Infrastructure.Simulation;
using DicomViewer.Infrastructure.Worklist;
using DicomViewer.Rendering.Abstractions;
using DicomViewer.Rendering.Services;
using DicomViewer.Wpf.Navigation;
using DicomViewer.Wpf.Views;
using DicomViewer.Wpf.ViewModels;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;

namespace DicomViewer.Wpf;

/// <summary>
/// WPF 应用程序入口，负责配置 Prism 容器、导航和主窗口。
/// </summary>
public partial class App : PrismApplication
{
    /// <summary>
    /// 创建应用的主壳窗口。
    /// </summary>
    protected override System.Windows.Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    /// <summary>
    /// 注册应用运行所需的核心服务、ViewModel 和导航视图。
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IStudyCatalogService, FileSystemStudyCatalogService>();
        // 保留 Mock 实现作为 DICOM MWL 查询失败时的降级数据源。
        containerRegistry.RegisterSingleton<MockWorklistService>();
        // 默认走标准 MWL 查询，业务层只依赖 IWorklistService 抽象。
        containerRegistry.RegisterSingleton<IWorklistService, DicomMwlWorklistService>();
        containerRegistry.RegisterSingleton<IInterlockService, DefaultInterlockService>();
        containerRegistry.RegisterSingleton<IAppDbConnectionFactory, SqliteAppDbConnectionFactory>();
        containerRegistry.RegisterSingleton<SqliteDatabaseInitializer>();
        containerRegistry.RegisterSingleton<IConsoleConfigurationStore, SqliteConsoleConfigurationStore>();
        containerRegistry.RegisterSingleton<IExamSessionStore, SqliteExamSessionStore>();
        containerRegistry.RegisterSingleton<IPacsSendRecordStore, SqlitePacsSendRecordStore>();
        containerRegistry.RegisterSingleton<SimulatedDicomBuilder>();
        containerRegistry.RegisterSingleton<IExposureSimulationService, MockExposureSimulationService>();
        containerRegistry.RegisterSingleton<ILocalDicomStoreScpService, LocalDicomStoreScpService>();
        // MPPS 使用标准 DICOM N-CREATE/N-SET，上层只依赖 IMppsService 抽象。
        containerRegistry.RegisterSingleton<IMppsService, DicomMppsService>();
        containerRegistry.RegisterSingleton<IPacsStoreService, OrthancStoreService>();
        containerRegistry.RegisterSingleton<IAuditService, SqliteAuditService>();
        containerRegistry.RegisterSingleton<IImageRenderService, PlaceholderRenderService>();
        containerRegistry.RegisterSingleton<IViewportImageService, DicomViewportImageService>();
        containerRegistry.RegisterSingleton<WorkspaceService>();
        containerRegistry.RegisterSingleton<ExamWorkflowService>();
        containerRegistry.RegisterSingleton<ShellViewModel>();
        containerRegistry.RegisterSingleton<ViewerWorkspaceViewModel>();
        containerRegistry.RegisterSingleton<ExposureConsoleViewModel>();
        containerRegistry.RegisterForNavigation<ViewerWorkspaceView>(ViewNames.ViewerWorkspaceView);
        containerRegistry.RegisterForNavigation<ExposureConsoleView>(ViewNames.ExposureConsoleView);
        containerRegistry.RegisterSingleton<MainWindow>();
    }

    /// <summary>
    /// 显式绑定视图与 ViewModel，便于学习者理解 Prism 的定位规则。
    /// </summary>
    protected override void ConfigureViewModelLocator()
    {
        base.ConfigureViewModelLocator();
        ViewModelLocationProvider.Register<MainWindow, ShellViewModel>();
        ViewModelLocationProvider.Register<ViewerWorkspaceView, ViewerWorkspaceViewModel>();
        ViewModelLocationProvider.Register<ExposureConsoleView, ExposureConsoleViewModel>();
    }

    /// <summary>
    /// 预留应用初始化扩展点。
    /// </summary>
    protected override void OnInitialized()
    {
        Container.Resolve<SqliteDatabaseInitializer>().EnsureCreated();
        base.OnInitialized();
    }
}
