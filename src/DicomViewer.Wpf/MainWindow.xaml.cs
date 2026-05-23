using System.Windows;
using DicomViewer.Wpf.ViewModels;

namespace DicomViewer.Wpf;

/// <summary>
/// 主窗口壳，负责在界面加载后触发 ShellViewModel 的初始化流程。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 初始化主窗口并订阅加载事件。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_OnLoaded;
    }

    /// <summary>
    /// 在窗口首次显示时启动主导航流程。
    /// </summary>
    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel viewModel)
        {
            viewModel.Initialize();
        }
    }
}
