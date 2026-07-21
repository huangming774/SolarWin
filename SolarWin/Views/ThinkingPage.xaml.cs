using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;
using Windows.System;

namespace SolarWin.Views;

public sealed partial class ThinkingPage : Page
{
    public ThinkingViewModel ViewModel { get; }

    public ThinkingPage()
    {
        ViewModel = App.Services.GetRequiredService<ThinkingViewModel>();
        InitializeComponent();
        ViewModel.Messages.CollectionChanged += (_, _) => ScrollToBottom();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.AgentNames.Count == 0 && ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void DraftBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+Enter to send
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
