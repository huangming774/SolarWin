using SolarWin.Models.Analytics;

namespace SolarWin.Services;

public interface IWordCloudLayoutService
{
    IReadOnlyList<WordCloudItem> Layout(
        IReadOnlyList<WordFrequencyItem> words,
        double width,
        double height,
        CancellationToken cancellationToken = default);
}
