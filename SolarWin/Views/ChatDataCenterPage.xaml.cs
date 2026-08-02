using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Controls;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class ChatDataCenterPage : Page
{
    private string? _requestedConversationId;

    public ChatDataCenterViewModel ViewModel { get; }

    public ChatDataCenterPage()
    {
        ViewModel = App.Services.GetRequiredService<ChatDataCenterViewModel>();
        InitializeComponent();
        Loaded += Page_OnLoaded;
        Unloaded += Page_OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _requestedConversationId = e.Parameter switch
        {
            Guid id when id != Guid.Empty => id.ToString("D"),
            string text when Guid.TryParse(text, out var id) && id != Guid.Empty => id.ToString("D"),
            _ => null,
        };
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.Close();
        base.OnNavigatedFrom(e);
    }

    private async void Page_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.InitializeCommand.CanExecute(null))
            {
                await ViewModel.InitializeCommand.ExecuteAsync(null);
            }

            if (_requestedConversationId is { } requested)
            {
                ViewModel.SelectedConversation = ViewModel.Conversations.FirstOrDefault(
                    item => string.Equals(item.Id, requested, StringComparison.OrdinalIgnoreCase));
                if (ViewModel.SelectedConversation is not null
                    && ViewModel.RefreshCommand.CanExecute(null))
                {
                    await ViewModel.RefreshCommand.ExecuteAsync(null);
                }
            }
        }
        catch
        {
            // ViewModel exposes friendly load errors.
        }
    }

    private void Page_OnUnloaded(object sender, RoutedEventArgs e)
        => ViewModel.Close();

    private async void WordCloud_OnRelayoutRequested(object? sender, WordCloudSizeChangedEventArgs e)
    {
        try
        {
            await ViewModel.RelayoutWordCloudAsync(e.Width, e.Height);
        }
        catch (OperationCanceledException)
        {
            // A newer resize or navigation superseded this layout.
        }
        catch
        {
            // Never let a layout failure escape an async UI event handler.
        }
    }
}
