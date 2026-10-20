using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

internal static class NoteVisual
{
    public static string Marker(NoteState state) => state switch
    { NoteState.Complete => "✓", NoteState.Pending => "!", NoteState.InProgress => "•", NoteState.Locked => "–", NoteState.Stale => "↻", _ => "?" };
    public static Brush Ink(NoteState state, bool theme = false)
    {
        if (theme) return (Brush)Application.Current.FindResource(state == NoteState.Pending ? "Warning" :
            state is NoteState.Unknown or NoteState.Stale or NoteState.Locked ? "Muted" : "AccentText");
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(state switch
        { NoteState.Pending => "#FFD17C", NoteState.Complete => "#B9E995", NoteState.InProgress => "#9DD9CB", _ => "#AEB7B2" }));
    }
    public static string Tooltip(NoteLine note) => $"{note.Name} · {note.Value}\n{NoteBoard.Hint(note, DateTimeOffset.UtcNow)}\n来源：{note.Source}";
}

public sealed class NotesView : UserControl
{
    private readonly TextBlock _summary = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _footer = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 2) };
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly Dictionary<string, Button> _filters = new();
    private IReadOnlyList<NoteLine> _notes = [];
    private string _filter = "全部";
    private long _minute;
    private string _theme = "";
    public NotesView()
    {
        var root = new StackPanel();
        _summary.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
        _footer.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        root.Children.Add(_summary);
        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        foreach (var label in new[] { "全部", "待办", "日常", "每周", "经营", "活动" })
        {
            var button = new Button { Content = label, FontSize = 11, Padding = new Thickness(5, 3, 5, 3), Margin = new Thickness(0, 0, 4, 3), BorderThickness = new Thickness(0) };
            button.Click += (_, _) => { _filter = label; Draw(); _scroll?.ScrollToTop(); };
            _filters[label] = button; filters.Children.Add(button);
        }
        root.Children.Add(filters);
        _scroll = new ScrollViewer { Content = _rows, MaxHeight = 250, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _scroll.SetResourceReference(StyleProperty, "WidgetScrollViewer");
        root.Children.Add(_scroll); root.Children.Add(_footer); Content = root;
    }
    public void SetNotes(IReadOnlyList<NoteLine> notes, string footer)
    {
        _footer.Text = footer;
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var theme = App.Current.Settings.Theme + App.Current.Settings.Accent;
        if (_notes.SequenceEqual(notes) && _minute == minute && _theme == theme) return;
        _notes = notes; _minute = minute; _theme = theme; Draw();
    }
    private void Draw()
    {
        var offset = _scroll.VerticalOffset;
        _summary.Text = $"待办 {_notes.Count(n => n.State == NoteState.Pending)} · 进行中 {_notes.Count(n => n.State == NoteState.InProgress)} · 已完成 {_notes.Count(n => n.State == NoteState.Complete)} · 待确认 {_notes.Count(n => n.State is NoteState.Unknown or NoteState.Stale)}";
        foreach (var (label, button) in _filters)
        {
            button.SetResourceReference(BackgroundProperty, label == _filter ? "Accent" : "Panel");
            if (label == _filter) button.Foreground = new SolidColorBrush(Color.FromRgb(23, 29, 17));
            else button.SetResourceReference(ForegroundProperty, "Muted");
        }
        _rows.Children.Clear();
        var selected = _notes.Where(n => _filter == "全部" || (_filter == "待办" ? n.State == NoteState.Pending : n.Group == _filter));
        foreach (var group in selected.GroupBy(n => n.Group).OrderBy(g => g.Key switch { "日常" => 0, "每周" => 1, "经营" => 2, _ => 3 }))
        {
            var heading = new TextBlock { Text = group.Key, FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 3) };
            heading.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _rows.Children.Add(heading);
            foreach (var note in group.OrderBy(n => n.State == NoteState.Pending ? 0 : 1))
            {
                var block = new StackPanel { Margin = new Thickness(0, 6, 3, 7), ToolTip = NoteVisual.Tooltip(note) };
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock { Text = note.Name, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 9, 0) };
                name.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
                var value = new TextBlock { Text = NoteVisual.Marker(note.State) + " " + note.Value, MaxWidth = 128, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = NoteVisual.Ink(note.State, true), TextAlignment = TextAlignment.Right };
                Grid.SetColumn(value, 1); row.Children.Add(name); row.Children.Add(value); block.Children.Add(row);
                if (note.Current is >= 0 && note.Target is > 0 && note.State is not (NoteState.Unknown or NoteState.Stale or NoteState.Locked))
                {
                    var progress = new ProgressBar { Minimum = 0, Maximum = note.Target.Value, Value = Math.Min(note.Current.Value, note.Target.Value), Height = 3, Margin = new Thickness(0, 5, 0, 1), BorderThickness = new Thickness(0) };
                    progress.SetResourceReference(ForegroundProperty, "Accent"); progress.SetResourceReference(BackgroundProperty, "Stroke"); block.Children.Add(progress);
                }
                var hint = NoteBoard.Hint(note, DateTimeOffset.UtcNow);
                if (hint.Length > 0)
                {
                    var text = new TextBlock { Text = hint, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
                    text.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); block.Children.Add(text);
                }
                _rows.Children.Add(block);
            }
        }
        if (_rows.Children.Count == 0)
        {
            var empty = new TextBlock { Text = _filter == "待办" ? "已同步项目中暂无明确待办。未知项目仍需确认。" : "本次接口没有返回此类信息。", FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _rows.Children.Add(empty);
        }
        _scroll.ScrollToVerticalOffset(offset);
    }
    internal void FilterForCheck(string filter) { _filter = filter; Draw(); }
    internal ScrollViewer ScrollForCheck => _scroll;
}
