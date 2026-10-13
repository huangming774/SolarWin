using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class WeatherPage : Page
{
    public WeatherViewModel ViewModel { get; }

    public WeatherPage()
    {
        ViewModel = App.Services.GetRequiredService<WeatherViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => ViewModel.CancelPendingRequests();
        SizeChanged += (_, args) => UpdateLayoutColumns(args.NewSize.Width);
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        UpdateLayoutColumns(ActualWidth);
        await ViewModel.RefreshIfStaleAsync();
    }

    private void UpdateLayoutColumns(double width)
    {
        var compact = width < 900;
        DetailsColumn.Width = new GridLength(compact ? 0 : 2, GridUnitType.Star);
        Grid.SetColumn(DetailsPanel, compact ? 0 : 1);
        Grid.SetRow(DetailsPanel, compact ? 1 : 0);
    }

    private void CitySearchBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.SuggestCitiesCommand.Execute(sender.Text);
    }

    private void CitySearchBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is GeoResult city) sender.Text = city.DisplayName;
    }

    private async void CitySearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is GeoResult city)
            await ViewModel.SelectCityCommand.ExecuteAsync(city);
        else
            await ViewModel.SearchAndSelectAsync(args.QueryText);
    }
}
