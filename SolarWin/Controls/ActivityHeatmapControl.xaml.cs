using System.Collections.Specialized;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SolarWin.Converters;
using SolarWin.Models.Analytics;

namespace SolarWin.Controls;

public sealed partial class ActivityHeatmapControl : UserControl
{
    private readonly HeatLevelToBrushConverter _brushConverter = new();
    private INotifyCollectionChanged? _observableItems;
    private bool _isLoaded;

    public ActivityHeatmapControl()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => BuildGrid();
        Loaded += (_, _) =>
        {
            _isLoaded = true;
            AttachCollection(ItemsSource);
        };
        Unloaded += (_, _) =>
        {
            _isLoaded = false;
            DetachCollection();
        };
    }

    public IEnumerable<ActivityHeatmapCell>? ItemsSource
    {
        get => (IEnumerable<ActivityHeatmapCell>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable<ActivityHeatmapCell>),
        typeof(ActivityHeatmapControl),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public ICommand? CellClickCommand
    {
        get => (ICommand?)GetValue(CellClickCommandProperty);
        set => SetValue(CellClickCommandProperty, value);
    }

    public static readonly DependencyProperty CellClickCommandProperty = DependencyProperty.Register(
        nameof(CellClickCommand),
        typeof(ICommand),
        typeof(ActivityHeatmapControl),
        new PropertyMetadata(null));

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ActivityHeatmapControl)d;
        control.DetachCollection();
        // Only subscribe while the control is in the visual tree. A subscription made
        // while detached would survive to the next Unloaded that never fires, letting a
        // long-lived view-model collection root this control (and its ~200 buttons).
        if (control._isLoaded)
        {
            control.AttachCollection(e.NewValue as IEnumerable<ActivityHeatmapCell>);
        }

        control.BuildGrid();
    }

    private void AttachCollection(IEnumerable<ActivityHeatmapCell>? items)
    {
        DetachCollection();
        if (items is INotifyCollectionChanged observable)
        {
            _observableItems = observable;
            observable.CollectionChanged += Items_CollectionChanged;
        }
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => BuildGrid();

    private void DetachCollection()
    {
        if (_observableItems is not null)
        {
            _observableItems.CollectionChanged -= Items_CollectionChanged;
            _observableItems = null;
        }
    }

    private void BuildGrid()
    {
        if (HeatmapGrid is null) return;
        HeatmapGrid.Children.Clear();
        HeatmapGrid.RowDefinitions.Clear();
        HeatmapGrid.ColumnDefinitions.Clear();
        HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
        for (var hour = 0; hour < 24; hour++)
        {
            HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(33) });
        }
        HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var day = 0; day < 7; day++)
        {
            HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
        }

        for (var hour = 0; hour < 24; hour++)
        {
            var label = new TextBlock
            {
                Text = hour.ToString("00"),
                FontSize = 11,
                Opacity = 0.58,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5),
            };
            Grid.SetColumn(label, hour + 1);
            HeatmapGrid.Children.Add(label);
        }

        var cells = ItemsSource?.ToDictionary(static x => (x.DayOfWeekIndex, x.Hour)) ?? [];
        var hasData = false;
        for (var day = 0; day < 7; day++)
        {
            var dayLabel = new TextBlock
            {
                Text = DayLabels[day],
                FontSize = 12,
                Opacity = 0.72,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(dayLabel, day + 1);
            HeatmapGrid.Children.Add(dayLabel);

            for (var hour = 0; hour < 24; hour++)
            {
                var cell = cells.TryGetValue((day, hour), out var existing)
                    ? existing
                    : new ActivityHeatmapCell { DayOfWeekIndex = day, DayLabel = DayLabels[day], Hour = hour };
                hasData |= cell.MessageCount > 0;
                var button = new Button
                {
                    Width = 28,
                    Height = 28,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    BorderThickness = new Thickness(0),
                    Background = (Microsoft.UI.Xaml.Media.Brush)_brushConverter.Convert(
                        cell.Intensity,
                        typeof(Microsoft.UI.Xaml.Media.Brush),
                        ActualTheme,
                        string.Empty),
                    Command = CellClickCommand,
                    CommandParameter = cell,
                };
                ToolTipService.SetToolTip(button, cell.TooltipText);
                AutomationProperties.SetName(button, cell.TooltipText);
                Grid.SetRow(button, day + 1);
                Grid.SetColumn(button, hour + 1);
                HeatmapGrid.Children.Add(button);
            }
        }

        EmptyOverlay.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
    }

    private static readonly string[] DayLabels =
        ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
}
