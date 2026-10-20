using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class CharacterCard : UserControl
{
    public event EventHandler? LaunchRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? DailySignRequested;
    public event EventHandler? CommunityRequested;
    public event EventHandler? WallpaperRequested;
    public event EventHandler? DetailsRequested;
    private NoteLine[] _rows = [];
    private int _page;
    private long _hintMinute;
    public CharacterCard() { InitializeComponent(); }
    public void SetDailySign(string text, bool enabled, string tooltip)
    {
        DailySignButton.Content = text;
        DailySignButton.IsEnabled = enabled && text != "已签到";
        DailySignButton.ToolTip = tooltip;
        AutomationProperties.SetName(DailySignButton, text);
        AutomationProperties.SetHelpText(DailySignButton, tooltip);
    }
    public void SetTheme(WidgetWallpaper wallpaper, ImageSource? image)
    {
        var color = (Color)ColorConverter.ConvertFromString(wallpaper.Color);
        CardRoot.Background = new SolidColorBrush(color);
        BottomWash.Fill = new LinearGradientBrush(Color.FromArgb(0, color.R, color.G, color.B), color, 90);
        Artwork.Source = image;
    }
    public void SetData(string current, string maximum, string recovery, IReadOnlyList<NoteLine>? notes, bool canRefresh)
    {
        Energy.Text = current; Maximum.Text = "/" + maximum;
        Energy.FontSize = current.Length > 3 ? 22 : 29;
        Recovery.Text = recovery; Recovery.ToolTip = recovery;
        RefreshButton.IsEnabled = canRefresh;
        var vitality = notes?.FirstOrDefault(n => n.Id == "vitality");
        Vitality.Text = vitality?.Value.Replace(" ", "") ?? "—/—";
        Vitality.ToolTip = vitality is null ? "接口未提供今日活跃数据" : NoteVisual.Tooltip(vitality);
        void Status(string id, string title, Border holder, TextBlock mark)
        {
            var note = notes?.FirstOrDefault(n => n.Id == id);
            holder.ToolTip = note is null ? title + " · 暂无数据" : NoteVisual.Tooltip(note);
            mark.Text = NoteVisual.Marker(note?.State ?? NoteState.Unknown);
            mark.Foreground = NoteVisual.Ink(note?.State ?? NoteState.Unknown);
            holder.Opacity = note?.State is null or NoteState.Unknown or NoteState.Stale ? .65 : 1;
        }
        Status("cafe", "咖啡", CoffeeStatus, CoffeeMark);
        Status("video", "录像店经营", VideoStatus, VideoMark);
        Status("card", NoteParser.ScratchCardName, ScratchStatus, ScratchMark);
        var rows = notes?.Where(n => n.Id is not ("vitality" or "cafe" or "card"))
            .OrderBy(n => n.State == NoteState.Pending ? 0 : n.State is NoteState.Unknown or NoteState.Stale ? 2 : 1)
            .ThenBy(n => n.Group == "活动" ? 0 : n.Group == "每周" ? 1 : 2).ToArray() ?? [];
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        if (!_rows.SequenceEqual(rows) || minute != _hintMinute)
        {
            _hintMinute = minute;
            _rows = rows; _page = Math.Min(_page, Math.Max(0, (_rows.Length - 1) / 3));
            RenderRows();
        }
        if (_rows.Length == 0) RenderRows();
        AllNotes.Content = notes is null ? "连接后查看便笺" : $"全部 {notes.Count} 项 · 待办 {notes.Count(n => n.State == NoteState.Pending)} ›";
        AllNotes.IsEnabled = notes is not null;
    }
    private void RenderRows()
    {
        SummaryRows.Children.Clear();
        foreach (var note in _rows.Skip(_page * 3).Take(3))
        {
            var row = new Grid { Height = 27, ToolTip = NoteVisual.Tooltip(note) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var mark = new TextBlock { Text = NoteVisual.Marker(note.State), Foreground = NoteVisual.Ink(note.State), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = note.Name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            var value = new TextBlock { Text = note.Value, FontSize = 12, MaxWidth = 127, Foreground = NoteVisual.Ink(note.State), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 1); Grid.SetColumn(value, 2);
            row.Children.Add(mark); row.Children.Add(name); row.Children.Add(value); SummaryRows.Children.Add(row);
        }
        if (_rows.Length == 0) SummaryRows.Children.Add(new TextBlock { Text = "同步后显示日常、周纪和活动进度", Foreground = Brushes.Silver, FontSize = 12, Margin = new Thickness(3, 23, 0, 0) });
        var pages = Math.Max(1, (int)Math.Ceiling(_rows.Length / 3d));
        PageNumber.Text = $"{_page + 1} / {pages}";
        PreviousPage.IsEnabled = _page > 0; NextPage.IsEnabled = _page < pages - 1;
    }
    public void ShowPreview() => SetData("77", "240", "明天 10:12 回满", DemoData.Create(DateTimeOffset.UtcNow).Notes, false);
    private void PreviousClick(object sender, RoutedEventArgs e) { if (_page > 0) _page--; RenderRows(); }
    private void NextClick(object sender, RoutedEventArgs e) { if ((_page + 1) * 3 < _rows.Length) _page++; RenderRows(); }
    private void DetailsClick(object sender, RoutedEventArgs e) => DetailsRequested?.Invoke(this, EventArgs.Empty);
    private void LaunchClick(object sender, RoutedEventArgs e) => LaunchRequested?.Invoke(this, EventArgs.Empty);
    private void RefreshClick(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
    private void SignClick(object sender, RoutedEventArgs e) => DailySignRequested?.Invoke(this, EventArgs.Empty);
    private void CommunityClick(object sender, RoutedEventArgs e) => CommunityRequested?.Invoke(this, EventArgs.Empty);
    private void WallpaperClick(object sender, RoutedEventArgs e) => WallpaperRequested?.Invoke(this, EventArgs.Empty);
}
