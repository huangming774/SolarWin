using System.Collections.Specialized;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SolarWin.Models.Analytics;

namespace SolarWin.Controls;

public sealed partial class WordCloudControl : UserControl
{
    private readonly DispatcherTimer _resizeDebounce;
    private INotifyCollectionChanged? _observableItems;
    private double _lastRequestedWidth;
    private double _lastRequestedHeight;
    private bool _isLoaded;

    public WordCloudControl()
    {
        InitializeComponent();
        _resizeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        Loaded += Control_OnLoaded;
        Unloaded += Control_OnUnloaded;
    }

    public event EventHandler<WordCloudSizeChangedEventArgs>? RelayoutRequested;

    public IEnumerable<WordCloudItem>? ItemsSource
    {
        get => (IEnumerable<WordCloudItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable<WordCloudItem>),
        typeof(WordCloudControl),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public ICommand? WordClickCommand
    {
        get => (ICommand?)GetValue(WordClickCommandProperty);
        set => SetValue(WordClickCommandProperty, value);
    }

    public static readonly DependencyProperty WordClickCommandProperty = DependencyProperty.Register(
        nameof(WordClickCommand),
        typeof(ICommand),
        typeof(WordCloudControl),
        new PropertyMetadata(null));

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (WordCloudControl)d;
        control.DetachCollection();
        control.AttachCollection(e.NewValue as IEnumerable<WordCloudItem>);
        control.RenderItems();
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RenderItems();

    private void Root_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isLoaded || e.NewSize.Width < 80 || e.NewSize.Height < 60) return;
        if (Math.Abs(e.NewSize.Width - _lastRequestedWidth) < 8
            && Math.Abs(e.NewSize.Height - _lastRequestedHeight) < 8) return;
        _resizeDebounce.Stop();
        _resizeDebounce.Start();
    }

    private void ResizeDebounce_OnTick(object? sender, object e)
    {
        _resizeDebounce.Stop();
        if (!_isLoaded || ActualWidth < 80 || ActualHeight < 60) return;
        if (Math.Abs(ActualWidth - _lastRequestedWidth) < 8
            && Math.Abs(ActualHeight - _lastRequestedHeight) < 8) return;
        _lastRequestedWidth = ActualWidth;
        _lastRequestedHeight = ActualHeight;
        RelayoutRequested?.Invoke(this, new WordCloudSizeChangedEventArgs(ActualWidth, ActualHeight));
    }

    private void Control_OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _resizeDebounce.Tick -= ResizeDebounce_OnTick;
        _resizeDebounce.Tick += ResizeDebounce_OnTick;
        AttachCollection(ItemsSource);
    }

    private void Control_OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _resizeDebounce.Stop();
        _resizeDebounce.Tick -= ResizeDebounce_OnTick;
        DetachCollection();
    }

    private void RenderItems()
    {
        if (CloudCanvas is null) return;
        CloudCanvas.Children.Clear();
        foreach (var item in ItemsSource ?? [])
        {
            var button = new Button
            {
                Content = item.Word,
                FontSize = item.FontSize,
                FontWeight = item.FontSize >= 34 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Padding = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                RenderTransform = new RotateTransform { Angle = item.Rotation },
                Command = WordClickCommand,
                CommandParameter = item,
            };
            try
            {
                if (Application.Current.Resources.TryGetValue(item.ColorKey, out var resource)
                    && resource is Brush brush)
                {
                    button.Foreground = brush;
                }
                else
                {
                    button.Foreground = ResolveFallbackBrush(item.ColorKey);
                }
            }
            catch
            {
                // default foreground remains theme-aware
            }
            ToolTipService.SetToolTip(button, $"{item.Word}：{item.Count} 次");
            AutomationProperties.SetName(button, $"词语 {item.Word}，出现 {item.Count} 次");
            Canvas.SetLeft(button, item.X);
            Canvas.SetTop(button, item.Y);
            CloudCanvas.Children.Add(button);
        }
    }

    private void DetachCollection()
    {
        if (_observableItems is not null)
        {
            _observableItems.CollectionChanged -= Items_CollectionChanged;
            _observableItems = null;
        }
    }

    private void AttachCollection(IEnumerable<WordCloudItem>? items)
    {
        if (!_isLoaded || _observableItems is not null || items is not INotifyCollectionChanged observable) return;
        _observableItems = observable;
        observable.CollectionChanged += Items_CollectionChanged;
    }

    private Brush ResolveFallbackBrush(string key)
    {
        var dark = ActualTheme == ElementTheme.Dark
                   || (ActualTheme == ElementTheme.Default
                       && Application.Current.RequestedTheme == ApplicationTheme.Dark);
        var color = key switch
        {
            "SystemFillColorSuccessBrush" => dark ? Windows.UI.Color.FromArgb(255, 70, 210, 150) : Windows.UI.Color.FromArgb(255, 16, 130, 95),
            "SystemFillColorCautionBrush" => dark ? Windows.UI.Color.FromArgb(255, 250, 190, 70) : Windows.UI.Color.FromArgb(255, 180, 105, 0),
            "SystemFillColorCriticalBrush" => dark ? Windows.UI.Color.FromArgb(255, 255, 110, 105) : Windows.UI.Color.FromArgb(255, 190, 45, 45),
            "AccentTextFillColorPrimaryBrush" => dark ? Windows.UI.Color.FromArgb(255, 105, 165, 255) : Windows.UI.Color.FromArgb(255, 38, 95, 205),
            _ => dark ? Windows.UI.Color.FromArgb(255, 235, 235, 235) : Windows.UI.Color.FromArgb(255, 45, 45, 45),
        };
        return new SolidColorBrush(color);
    }
}

public sealed class WordCloudSizeChangedEventArgs(double width, double height) : EventArgs
{
    public double Width { get; } = width;
    public double Height { get; } = height;
}
