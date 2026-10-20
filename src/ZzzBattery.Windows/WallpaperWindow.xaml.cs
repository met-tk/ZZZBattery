using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class WallpaperWindow : Window
{
    private readonly WallpaperService _service = App.Current.Wallpapers;
    private readonly CancellationTokenSource _closed = new();
    private List<WallpaperOption> _options = [];
    private string? _element;
    private string? _profession;
    private WallpaperOption? _selection;
    private BitmapSource? _preview;
    private bool _ready;
    private bool _filtering;
    private int _previewVersion;
    public int VisibleCount => WallpaperList.Items.Count;

    public WallpaperWindow()
    {
        InitializeComponent();
        Width = Math.Min(980, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(790, SystemParameters.WorkArea.Height - 40);
        PreviewCard.ShowPreview();
        _ready = true;
        SetCatalog(_service.Catalog.Current);
        Loaded += async (_, _) => await RefreshCatalog(false);
        Closed += (_, _) => { _closed.Cancel(); _previewVersion++; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void SetCatalog(WallpaperCatalogResult result)
    {
        var selectedId = _selection?.Wallpaper.Id ?? _service.Resolve(App.Current.Settings.Wallpaper).Id;
        _options = result.Catalog.Wallpapers.Select(w => new WallpaperOption(w)).ToList();
        _selection = _options.FirstOrDefault(w => w.Wallpaper.Id == selectedId) ?? _options.First();
        if (!result.Catalog.Elements.Any(f => f.Id == _element)) _element = null;
        if (!result.Catalog.Professions.Any(f => f.Id == _profession)) _profession = null;
        BuildFilters(); FilterList();
        WallpaperList.SelectedItem = _selection;
        _ = UpdatePreview(_selection);
        CatalogStatus.Text = result.Error ?? (result.FetchedAt is { } time
            ? $"{(result.Offline ? "本地缓存" : "已同步米游社")} · {time.ToLocalTime():MM-dd HH:mm} · 每 6 小时检查更新"
            : "内置壁纸列表 · 联网后自动更新；已下载图片可离线使用");
    }

    private async Task RefreshCatalog(bool force)
    {
        RefreshCatalogButton.IsEnabled = false;
        CatalogStatus.Text = "正在同步米游社壁纸…";
        try
        {
            // Opening the picker checks for newly released skins, with one-minute request coalescing.
            var current = _service.Catalog.Current;
            var result = await _service.Catalog.RefreshAsync(force || current.FetchedAt is null ||
                DateTimeOffset.UtcNow - current.FetchedAt > TimeSpan.FromMinutes(5), _closed.Token);
            if (_closed.IsCancellationRequested) return;
            SetCatalog(result);
            App.Current.UpdateWallpaper();
        }
        catch (OperationCanceledException) { }
        finally { RefreshCatalogButton.IsEnabled = true; }
    }

    private void BuildFilters()
    {
        void Populate(WrapPanel panel, IReadOnlyList<WallpaperFilter> filters, bool element)
        {
            panel.Children.Clear();
            foreach (var filter in filters)
            {
                var selected = (element ? _element : _profession) == filter.Id;
                var icon = new Image { Width = 46, Height = 46 };
                var button = new Button
                {
                    Content = icon, Width = 49, Height = 49, MinHeight = 0, Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 12, 4), Background = Brushes.Transparent,
                    BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(255, 222, 0)) : Brushes.Transparent,
                    ToolTip = FilterName(filter.Id, element)
                };
                System.Windows.Automation.AutomationProperties.SetName(button, button.ToolTip.ToString());
                button.Click += (_, _) =>
                {
                    if (element) _element = _element == filter.Id ? null : filter.Id;
                    else _profession = _profession == filter.Id ? null : filter.Id;
                    BuildFilters(); FilterList();
                };
                panel.Children.Add(button);
                _ = LoadFilterIcon(icon, selected ? filter.SelectedIconUrl : filter.IconUrl);
            }
        }
        Populate(ElementFilters, _service.Catalog.Current.Catalog.Elements, true);
        Populate(ProfessionFilters, _service.Catalog.Current.Catalog.Professions, false);
    }

    private static string FilterName(string id, bool element) => element ? id switch
    {
        "200" => "物理", "201" => "火", "202" => "冰", "203" => "电", "205" => "以太", _ => "属性 " + id
    } : id switch
    {
        "1" => "强攻", "2" => "击破", "3" => "异常", "4" => "支援", "5" => "防护", "6" => "命破", _ => "特性 " + id
    };
    private async Task LoadFilterIcon(Image target, string url)
    {
        try { target.Source = await _service.GetImageAsync(url, 96, _closed.Token); }
        catch (OperationCanceledException) { }
    }

    private void FilterList()
    {
        if (!_ready) return;
        _filtering = true;
        var visible = _options.Where(o => o.Wallpaper.Matches(_element, _profession, SearchBox.Text)).ToList();
        WallpaperList.ItemsSource = visible;
        WallpaperList.SelectedItem = visible.Contains(_selection!) ? _selection : null;
        CatalogCount.Text = $"{visible.Count} / {_options.Count} 款";
        EmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _filtering = false;
    }
    private void ThumbnailLoaded(object sender, RoutedEventArgs e) => LoadVisibleThumbnails();
    private void GalleryScrolled(object sender, ScrollChangedEventArgs e) => LoadVisibleThumbnails();
    private void GallerySized(object sender, SizeChangedEventArgs e) => LoadVisibleThumbnails();
    internal void LoadVisibleThumbnails()
    {
        if (!_ready || !WallpaperList.IsLoaded) return;
        foreach (WallpaperOption option in WallpaperList.Items)
        {
            if (option.Thumbnail is not null || option.Loading || option.ImageStatus.Length > 0) continue;
            if (WallpaperList.ItemContainerGenerator.ContainerFromItem(option) is not FrameworkElement container || !container.IsLoaded) continue;
            var y = container.TranslatePoint(new Point(0, 0), WallpaperList).Y;
            if (y + container.ActualHeight >= -140 && y <= WallpaperList.ActualHeight + 140) _ = LoadThumbnail(option);
        }
    }
    private async Task LoadThumbnail(WallpaperOption option)
    {
        option.Loading = true;
        try
        {
            option.Thumbnail = await _service.GetImageAsync(option.Wallpaper.ImageUrl, 360, _closed.Token);
            option.ImageStatus = option.Thumbnail is null ? "未下载 · 联网后重试" : "";
        }
        catch (OperationCanceledException) { }
        finally { option.Loading = false; }
    }
    private async Task UpdatePreview(WallpaperOption option)
    {
        var version = ++_previewVersion;
        _preview = null; ApplyButton.IsEnabled = false;
        SelectedName.Text = option.Name;
        PreviewStatus.Text = "正在加载完整壁纸…";
        PreviewCard.SetTheme(option.Wallpaper, option.Thumbnail);
        try
        {
            var image = await _service.GetImageAsync(option.Wallpaper.ImageUrl, 720, _closed.Token);
            if (version != _previewVersion || _closed.IsCancellationRequested) return;
            _preview = image;
            PreviewCard.SetTheme(option.Wallpaper, image);
            ApplyButton.IsEnabled = image is not null;
            PreviewStatus.Text = image is null ? "图片暂时无法加载，请联网后点击“更新壁纸”重试。" : "官方角色立绘与主题色 · 应用后自动保存";
        }
        catch (OperationCanceledException) { }
    }
    private void WallpaperSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_filtering || WallpaperList.SelectedItem is not WallpaperOption selected) return;
        _selection = selected; _ = UpdatePreview(selected);
    }
    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (_selection is null || _preview is null) return;
        App.Current.Settings.Wallpaper = _selection.Wallpaper;
        App.Current.Settings.CharacterStyle = true;
        App.Current.Settings.Mini = false;
        var saved = App.Current.SaveSettings();
        App.Current.ShowWidget();
        PreviewStatus.Text = saved ? "已应用并保存，下次启动将继续使用此壁纸。" : "已应用，但数据目录无法写入，选择尚未保存。";
    }
    private async void RefreshCatalogClick(object sender, RoutedEventArgs e) => await RefreshCatalog(true);
    private void SearchChanged(object sender, TextChangedEventArgs e) => FilterList();
    private void ResetFiltersClick(object sender, RoutedEventArgs e) { _element = _profession = null; SearchBox.Clear(); BuildFilters(); FilterList(); }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    // Used by the explicit isolated UI smoke check; follows the same selection and apply handlers.
    internal async Task SelectForCheck(string id)
    {
        _selection = _options.Single(o => o.Wallpaper.Id == id); WallpaperList.SelectedItem = _selection;
        await UpdatePreview(_selection);
        if (_preview is null) throw new InvalidOperationException("Wallpaper preview failed to load.");
        ApplyClick(this, new RoutedEventArgs());
    }
    internal void FilterForCheck(string? element, string? profession, string search)
    { _element = element; _profession = profession; SearchBox.Text = search; BuildFilters(); FilterList(); }
}

public sealed class WallpaperOption(WidgetWallpaper wallpaper) : INotifyPropertyChanged
{
    public WidgetWallpaper Wallpaper { get; } = wallpaper;
    public string Name => Wallpaper.Name;
    public Brush Background { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString(wallpaper.Color));
    private BitmapSource? _thumbnail;
    private string _imageStatus = "";
    public bool Loading { get; set; }
    public string Placeholder => _thumbnail is null && _imageStatus.Length == 0 ? "正在加载…" : "";
    public BitmapSource? Thumbnail { get => _thumbnail; set { _thumbnail = value; PropertyChanged?.Invoke(this, new(nameof(Thumbnail))); PropertyChanged?.Invoke(this, new(nameof(Placeholder))); } }
    public string ImageStatus { get => _imageStatus; set { _imageStatus = value; PropertyChanged?.Invoke(this, new(nameof(ImageStatus))); PropertyChanged?.Invoke(this, new(nameof(Placeholder))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
