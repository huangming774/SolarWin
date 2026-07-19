using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SolarWin.Controls;

public sealed partial class PageStateOverlay : UserControl
{
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(PageStateOverlay),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty IsEmptyProperty =
        DependencyProperty.Register(nameof(IsEmpty), typeof(bool), typeof(PageStateOverlay),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty HasErrorProperty =
        DependencyProperty.Register(nameof(HasError), typeof(bool), typeof(PageStateOverlay),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty LoadingMessageProperty =
        DependencyProperty.Register(nameof(LoadingMessage), typeof(string), typeof(PageStateOverlay),
            new PropertyMetadata("加载中…", OnTextChanged));

    public static readonly DependencyProperty EmptyMessageProperty =
        DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(PageStateOverlay),
            new PropertyMetadata("暂无内容", OnTextChanged));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(PageStateOverlay),
            new PropertyMetadata("出错了", OnTextChanged));

    public PageStateOverlay()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public bool IsEmpty
    {
        get => (bool)GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public string LoadingMessage
    {
        get => (string)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageStateOverlay overlay)
        {
            overlay.UpdateVisualState();
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageStateOverlay overlay)
        {
            overlay.LoadingText.Text = overlay.LoadingMessage;
            overlay.EmptyText.Text = overlay.EmptyMessage;
            overlay.ErrorText.Text = string.IsNullOrWhiteSpace(overlay.ErrorMessage) ? "出错了" : overlay.ErrorMessage;
        }
    }

    private void UpdateVisualState()
    {
        // Priority: loading > error > empty
        var showLoading = IsLoading;
        var showError = !IsLoading && HasError;
        var showEmpty = !IsLoading && !HasError && IsEmpty;

        LoadingPanel.Visibility = showLoading ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = showError ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = showEmpty ? Visibility.Visible : Visibility.Collapsed;

        Visibility = showLoading || showError || showEmpty ? Visibility.Visible : Visibility.Collapsed;
        IsHitTestVisible = showLoading || showError || showEmpty;

        LoadingText.Text = LoadingMessage;
        EmptyText.Text = EmptyMessage;
        ErrorText.Text = string.IsNullOrWhiteSpace(ErrorMessage) ? "出错了" : ErrorMessage;
    }
}
