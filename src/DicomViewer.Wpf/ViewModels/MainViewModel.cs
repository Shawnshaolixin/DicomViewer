using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Enums;
using DicomViewer.Shared.Mvvm;

namespace DicomViewer.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;
    private bool _isApplyingSnapshot;
    private SeriesSummary? _selectedSeries;
    private string _importPath = string.Empty;
    private BitmapSource? _viewportImageSource;
    private string _viewerTitle = "WPF Medical Viewer";
    private string _viewerSubtitle = "Workspace initializing";
    private string _placeholderText = "Preparing workspace";
    private string _statusText = "Starting";
    private string _patientText = "-";
    private string _studyText = "-";
    private string _toolText = ViewerToolMode.None.ToString();
    private string _windowText = "WW 0 / WL 0";
    private string _sliceText = "Slice 0 / 0";
    private string _viewText = "Zoom 1.00x | Pan (0,0)";
    private string _notesText = "Viewer shell not loaded yet.";

    public MainViewModel(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        SeriesItems = new ObservableCollection<SeriesSummary>();

        // 当前 ViewModel 只负责把 UI 命令转成工作区动作，不直接处理 DICOM 解析或像素运算。
        ImportFolderCommand = new RelayCommand(async () => await ImportFolderAsync());
        PreviousSliceCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.MoveSlice(-1)), () => SeriesItems.Count > 0);
        NextSliceCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.MoveSlice(1)), () => SeriesItems.Count > 0);
        PanToolCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.Pan)));
        WindowLevelToolCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.WindowLevel)));
        LengthToolCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.MeasureLength)));
        ZoomInCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.Zoom(1.2)), () => SeriesItems.Count > 0);
        ZoomOutCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.Zoom(1.0 / 1.2)), () => SeriesItems.Count > 0);
        SoftTissuePresetCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.ApplyWindowLevelPreset(new(400, 40))), () => SeriesItems.Count > 0);
        LungPresetCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.ApplyWindowLevelPreset(new(1500, -600))), () => SeriesItems.Count > 0);
        BonePresetCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.ApplyWindowLevelPreset(new(2000, 300))), () => SeriesItems.Count > 0);
        IncreaseWindowCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(50, 0)), () => SeriesItems.Count > 0);
        DecreaseWindowCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(-50, 0)), () => SeriesItems.Count > 0);
        IncreaseLevelCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(0, 25)), () => SeriesItems.Count > 0);
        DecreaseLevelCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(0, -25)), () => SeriesItems.Count > 0);
        ResetViewCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.ResetView()));
    }

    public ObservableCollection<SeriesSummary> SeriesItems { get; }

    public string ImportPath
    {
        get => _importPath;
        set => SetProperty(ref _importPath, value);
    }

    public BitmapSource? ViewportImageSource
    {
        get => _viewportImageSource;
        private set
        {
            if (SetProperty(ref _viewportImageSource, value))
            {
                OnPropertyChanged(nameof(ViewportImageVisibility));
                OnPropertyChanged(nameof(PlaceholderVisibility));
            }
        }
    }

    public Visibility ViewportImageVisibility => ViewportImageSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PlaceholderVisibility => ViewportImageSource is null ? Visibility.Visible : Visibility.Collapsed;

    public SeriesSummary? SelectedSeries
    {
        get => _selectedSeries;
        set
        {
            if (!SetProperty(ref _selectedSeries, value) || value is null || _isApplyingSnapshot)
            {
                return;
            }

            ApplySnapshot(_workspaceService.SelectSeries(value.SeriesInstanceUid));
        }
    }

    public string ViewerTitle
    {
        get => _viewerTitle;
        private set => SetProperty(ref _viewerTitle, value);
    }

    public string ViewerSubtitle
    {
        get => _viewerSubtitle;
        private set => SetProperty(ref _viewerSubtitle, value);
    }

    public string PlaceholderText
    {
        get => _placeholderText;
        private set => SetProperty(ref _placeholderText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string PatientText
    {
        get => _patientText;
        private set => SetProperty(ref _patientText, value);
    }

    public string StudyText
    {
        get => _studyText;
        private set => SetProperty(ref _studyText, value);
    }

    public string ToolText
    {
        get => _toolText;
        private set => SetProperty(ref _toolText, value);
    }

    public string WindowText
    {
        get => _windowText;
        private set => SetProperty(ref _windowText, value);
    }

    public string SliceText
    {
        get => _sliceText;
        private set => SetProperty(ref _sliceText, value);
    }

    public string ViewText
    {
        get => _viewText;
        private set => SetProperty(ref _viewText, value);
    }

    public string NotesText
    {
        get => _notesText;
        private set => SetProperty(ref _notesText, value);
    }

    public RelayCommand PreviousSliceCommand { get; }

    public RelayCommand ImportFolderCommand { get; }

    public RelayCommand NextSliceCommand { get; }

    public RelayCommand PanToolCommand { get; }

    public RelayCommand WindowLevelToolCommand { get; }

    public RelayCommand LengthToolCommand { get; }

    public RelayCommand ZoomInCommand { get; }

    public RelayCommand ZoomOutCommand { get; }

    public RelayCommand SoftTissuePresetCommand { get; }

    public RelayCommand LungPresetCommand { get; }

    public RelayCommand BonePresetCommand { get; }

    public RelayCommand IncreaseWindowCommand { get; }

    public RelayCommand DecreaseWindowCommand { get; }

    public RelayCommand IncreaseLevelCommand { get; }

    public RelayCommand DecreaseLevelCommand { get; }

    public RelayCommand ResetViewCommand { get; }

    public async Task InitializeAsync()
    {
        ApplySnapshot(await _workspaceService.LoadAsync());
    }

    private async Task ImportFolderAsync()
    {
        ApplySnapshot(await _workspaceService.LoadAsync(ImportPath));
    }

    private void ApplySnapshot(WorkspaceSnapshot snapshot)
    {
        // 所有界面状态都从同一份快照同步，便于后续把状态来源收敛到 Application 层。
        _isApplyingSnapshot = true;
        SeriesItems.Clear();
        foreach (var item in snapshot.SeriesItems)
        {
            SeriesItems.Add(item);
        }

        SelectedSeries = SeriesItems.FirstOrDefault(item => item.SeriesInstanceUid == snapshot.ActiveSeriesInstanceUid);
        _isApplyingSnapshot = false;

        ViewerTitle = snapshot.ViewerTitle;
        ViewerSubtitle = snapshot.ViewerSubtitle;
        PlaceholderText = snapshot.PlaceholderText;
        ViewportImageSource = CreateBitmapSource(snapshot.ViewportImage);
        StatusText = snapshot.StatusText;
        PatientText = snapshot.PatientText;
        StudyText = snapshot.StudyText;
        ToolText = snapshot.ToolText;
        WindowText = snapshot.WindowText;
        SliceText = snapshot.SliceText;
        ViewText = snapshot.ViewText;
        NotesText = snapshot.NotesText;

        PreviousSliceCommand.NotifyCanExecuteChanged();
        NextSliceCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        SoftTissuePresetCommand.NotifyCanExecuteChanged();
        LungPresetCommand.NotifyCanExecuteChanged();
        BonePresetCommand.NotifyCanExecuteChanged();
        IncreaseWindowCommand.NotifyCanExecuteChanged();
        DecreaseWindowCommand.NotifyCanExecuteChanged();
        IncreaseLevelCommand.NotifyCanExecuteChanged();
        DecreaseLevelCommand.NotifyCanExecuteChanged();
    }

    private static BitmapSource? CreateBitmapSource(ViewportImageData? image)
    {
        if (image is null || image.Width <= 0 || image.Height <= 0 || image.Pixels.Length == 0)
        {
            return null;
        }

        // 这里使用 Gray8 直接生成 BitmapSource，先保持链路简单，后续再引入更复杂的渲染控件。
        var bitmap = BitmapSource.Create(
            image.Width,
            image.Height,
            96,
            96,
            System.Windows.Media.PixelFormats.Gray8,
            null,
            image.Pixels,
            image.Stride);

        bitmap.Freeze();
        return bitmap;
    }
}