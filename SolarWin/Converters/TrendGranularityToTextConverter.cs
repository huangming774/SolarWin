using Microsoft.UI.Xaml.Data;
using SolarWin.Models.Analytics;

namespace SolarWin.Converters;

public sealed class TrendGranularityToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is TrendGranularity granularity
            ? granularity switch
            {
                TrendGranularity.Week => "按周",
                TrendGranularity.Month => "按月",
                _ => "按日",
            }
            : "按日";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
