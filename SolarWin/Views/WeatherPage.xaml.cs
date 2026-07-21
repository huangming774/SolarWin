using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using SolarWin.Models;
using SolarWin.ViewModels;
using Windows.UI;

namespace SolarWin.Views;

public sealed partial class WeatherPage : Page
{
    public WeatherViewModel ViewModel { get; }

    public WeatherPage()
    {
        ViewModel = App.Services.GetRequiredService<WeatherViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        CitySearchBox.ItemsSource = ViewModel.CitySuggestions;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyCinematicBackground();
        ApplyGlassToCards();
        EnsureCardShadows();
    }

    private void Page_OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyGlassToCards();
        ApplyCinematicBackground();
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WeatherViewModel.GradientTop)
            or nameof(WeatherViewModel.GradientBottom)
            or null)
        {
            ApplyCinematicBackground();
        }
    }

    /// <summary>
    /// Navy-blue gradient + golden solar halo / bloom (diffused light behind glass).
    /// </summary>
    private void ApplyCinematicBackground()
    {
        if (WeatherGradientHost is not null)
        {
            WeatherGradientHost.Fill = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0.85, 1),
                GradientStops =
                {
                    new GradientStop { Color = ViewModel.GradientTop, Offset = 0 },
                    new GradientStop { Color = ViewModel.GradientBottom, Offset = 0.55 },
                    new GradientStop
                    {
                        Color = Color.FromArgb(
                            255,
                            (byte)Math.Clamp(ViewModel.GradientBottom.R + 20, 0, 255),
                            (byte)Math.Clamp(ViewModel.GradientBottom.G + 10, 0, 255),
                            (byte)Math.Clamp(ViewModel.GradientBottom.B - 10, 0, 255)),
                        Offset = 1,
                    },
                },
            };
        }

        if (SunHaloOuter is not null)
        {
            SunHaloOuter.Fill = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0x90, 0xFF, 0xD0, 0x6A), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0x55, 0xFF, 0xB0, 0x40), Offset = 0.35 },
                    new GradientStop { Color = Color.FromArgb(0x18, 0xFF, 0xC8, 0x70), Offset = 0.7 },
                    new GradientStop { Color = Color.FromArgb(0x00, 0x20, 0x40, 0x80), Offset = 1 },
                },
            };
        }

        if (SunCore is not null)
        {
            SunCore.Fill = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.45, 0.45),
                RadiusX = 0.55,
                RadiusY = 0.55,
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0xFF, 0xFF, 0xF2, 0xC8), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0xE6, 0xFF, 0xD0, 0x50), Offset = 0.25 },
                    new GradientStop { Color = Color.FromArgb(0x88, 0xFF, 0xA8, 0x30), Offset = 0.55 },
                    new GradientStop { Color = Color.FromArgb(0x00, 0xFF, 0x90, 0x20), Offset = 1 },
                },
            };
        }

        if (SunBloom is not null)
        {
            SunBloom.Fill = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0.5, 0),
                EndPoint = new Windows.Foundation.Point(0.5, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0x55, 0xFF, 0xC0, 0x60), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0x18, 0x80, 0xA0, 0xD0), Offset = 0.55 },
                    new GradientStop { Color = Color.FromArgb(0x00, 0x10, 0x20, 0x40), Offset = 1 },
                },
            };
        }
    }

    /// <summary>
    /// Frosted glass acrylic: high blur feel via translucent tint + white edge.
    /// (WinUI AcrylicBrush; TintLuminosityOpacity deepens the liquid-glass look.)
    /// </summary>
    private void ApplyGlassToCards()
    {
        var glass = CreateGlassAcrylicBrush();
        var glassStrong = CreateGlassAcrylicBrush(stronger: true);

        foreach (var card in GlassCards())
        {
            if (card is null)
            {
                continue;
            }

            card.Background = card == MainCard || card == SearchCard ? glassStrong : glass;
        }
    }

    private IEnumerable<Border?> GlassCards()
    {
        yield return SearchCard;
        yield return MainCard;
        yield return HourlyCard;
        yield return DailyCard;
        yield return AqiCard;
        yield return PrecipCard;
        yield return UvCard;
        yield return SunsetCard;
    }

    private Brush CreateGlassAcrylicBrush(bool stronger = false)
    {
        try
        {
            // Frosted glass: cool translucent white over navy, soft luminosity.
            var tint = stronger
                ? Color.FromArgb(0x55, 0xE8, 0xF0, 0xFF)
                : Color.FromArgb(0x40, 0xD8, 0xE6, 0xFF);
            var fallback = stronger
                ? Color.FromArgb(0x88, 0x28, 0x3C, 0x68)
                : Color.FromArgb(0x70, 0x20, 0x34, 0x5C);

            return new AcrylicBrush
            {
                TintColor = tint,
                TintOpacity = stronger ? 0.42 : 0.32,
                TintLuminosityOpacity = 0.55,
                FallbackColor = fallback,
            };
        }
        catch
        {
            return new SolidColorBrush(Color.FromArgb(0x88, 0x24, 0x38, 0x60));
        }
    }

    private void EnsureCardShadows()
    {
        try
        {
            var shadow = new ThemeShadow();
            if (WeatherGradientHost is not null)
            {
                shadow.Receivers.Add(WeatherGradientHost);
            }

            void Elevate(Border? card, float z)
            {
                if (card is null)
                {
                    return;
                }

                card.Translation = new System.Numerics.Vector3(0, 0, z);
                card.Shadow = shadow;
            }

            Elevate(MainCard, 40);
            Elevate(SearchCard, 36);
            Elevate(HourlyCard, 28);
            Elevate(DailyCard, 28);
            Elevate(AqiCard, 24);
            Elevate(PrecipCard, 24);
            Elevate(UvCard, 24);
            Elevate(SunsetCard, 24);
        }
        catch
        {
            // ThemeShadow not critical if host tree rejects receivers.
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!ViewModel.HasData && ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void CitySearchBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        if (ViewModel.SuggestCitiesCommand.CanExecute(sender.Text))
        {
            ViewModel.SuggestCitiesCommand.Execute(sender.Text);
        }
    }

    private void CitySearchBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is GeoResult city)
        {
            sender.Text = city.DisplayName;
        }
    }

    private void CitySearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is GeoResult chosen)
        {
            if (ViewModel.SelectCityCommand.CanExecute(chosen))
            {
                ViewModel.SelectCityCommand.Execute(chosen);
            }

            return;
        }

        if (ViewModel.CitySuggestions.Count > 0)
        {
            var first = ViewModel.CitySuggestions[0];
            if (ViewModel.SelectCityCommand.CanExecute(first))
            {
                ViewModel.SelectCityCommand.Execute(first);
            }
        }
    }
}
