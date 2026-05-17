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
        ImportFolderCommand = new RelayCommand(async () => await ImportFolderAsync());
        PreviousSliceCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.MoveSlice(-1)), () => SeriesItems.Count > 0);
        NextSliceCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.MoveSlice(1)), () => SeriesItems.Count > 0);
        PanToolCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.Pan)));
        WindowLevelToolCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.WindowLevel)));
        LengthToolCommand = new RelayCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.MeasureLength)));
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
    }

    private static BitmapSource? CreateBitmapSource(ViewportImageData? image)
    {
        if (image is null || image.Width <= 0 || image.Height <= 0 || image.Pixels.Length == 0)
        {
            return null;
        }

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