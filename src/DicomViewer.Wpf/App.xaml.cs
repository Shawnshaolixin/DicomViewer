using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Services;
using DicomViewer.Infrastructure.Data;
using DicomViewer.Infrastructure.Imaging;
using DicomViewer.Rendering.Abstractions;
using DicomViewer.Rendering.Services;
using DicomViewer.Wpf.ViewModels;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;

namespace DicomViewer.Wpf;

public partial class App : PrismApplication
{
    protected override System.Windows.Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IStudyCatalogService, FileSystemStudyCatalogService>();
        containerRegistry.RegisterSingleton<IImageRenderService, PlaceholderRenderService>();
        containerRegistry.RegisterSingleton<IViewportImageService, DicomViewportImageService>();
        containerRegistry.RegisterSingleton<WorkspaceService>();
        containerRegistry.RegisterSingleton<MainViewModel>();
        containerRegistry.RegisterSingleton<MainWindow>();
    }

    protected override void ConfigureViewModelLocator()
    {
        base.ConfigureViewModelLocator();
        ViewModelLocationProvider.Register<MainWindow, MainViewModel>();
    }

    protected override async void OnInitialized()
    {
        base.OnInitialized();

        if (MainWindow?.DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
