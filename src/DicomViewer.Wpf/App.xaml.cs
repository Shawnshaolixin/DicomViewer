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

		var viewModel = new MainViewModel(new WorkspaceService(
			new FileSystemStudyCatalogService(),
			new PlaceholderRenderService(),
			new DicomViewportImageService()));

		var mainWindow = new MainWindow(viewModel);
		MainWindow = mainWindow;
		mainWindow.Show();
	}
}

