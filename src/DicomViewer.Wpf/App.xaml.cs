using System.Windows;
using DicomViewer.Application.Services;
using DicomViewer.Infrastructure.Data;
using DicomViewer.Infrastructure.Imaging;
using DicomViewer.Rendering.Services;
using DicomViewer.Wpf.ViewModels;

namespace DicomViewer.Wpf;

public partial class App : System.Windows.Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// 当前先用手工组装依赖，后续如果模块继续增多再切换到 DI 容器。
		var viewModel = new MainViewModel(new WorkspaceService(
			new FileSystemStudyCatalogService(),
			new PlaceholderRenderService(),
			new DicomViewportImageService()));

		var mainWindow = new MainWindow(viewModel);
		MainWindow = mainWindow;
		mainWindow.Show();
	}
}

