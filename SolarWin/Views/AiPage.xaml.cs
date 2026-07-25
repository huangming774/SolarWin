using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;
using Windows.System;

namespace SolarWin.Views;

public sealed partial class AiPage : Page
{
    private readonly NotifyCollectionChangedEventHandler _messagesChangedHandler;

    public AiViewModel ViewModel { get; }

    public AiPage()
    {
        ViewModel = App.Services.GetRequiredService<AiViewModel>();
        InitializeComponent();
        // AiViewModel is a singleton — subscribing from the constructor without a
        // matching unsubscribe rooted every visited page's visual tree forever.
        _messagesChangedHandler = (_, _) =>
        {
            Bindings.Update();
            ScrollToBottom();
        };
        Loaded += OnLoaded;
    }

    /// <summary>Empty-state hint when there are no messages.</summary>
    public Visibility EmptyHintVisibility =>
        ViewModel.Messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncApiKeyBox();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.LoadSettingsFromStore();
        SyncApiKeyBox();
        ViewModel.Messages.CollectionChanged += _messagesChangedHandler;
        Bindings.Update();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Messages.CollectionChanged -= _messagesChangedHandler;
    }

    private void SyncApiKeyBox()
    {
        try
        {
            if (ApiKeyBox is not null && ApiKeyBox.Password != ViewModel.ApiKey)
            {
                ApiKeyBox.Password = ViewModel.ApiKey ?? string.Empty;
            }
        }
        catch
        {
            // PasswordBox may not be ready
        }
    }

    private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            ViewModel.ApiKey = box.Password ?? string.Empty;
        }
    }

    private void DraftBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)
            && ViewModel.SendCommand.CanExecute(null))
        {
            ViewModel.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ScrollToBottom()
    {
        try
        {
            if (MessageList.Items.Count == 0)
            {
                return;
            }

            MessageList.ScrollIntoView(MessageList.Items[^1]);
        }
        catch
        {
            // ignore
        }
    }
}
