using System.Collections.Specialized;
using SolarWin.Collections;

namespace SolarWin.Tests;

public sealed class ObservableRangeCollectionTests
{
    [Fact]
    public void ReplaceWith_RaisesOneResetInsteadOfOneEventPerItem()
    {
        var collection = new ObservableRangeCollection<int> { 9 };
        var notifications = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, args) => notifications.Add(args);

        collection.ReplaceWith(Enumerable.Range(0, 168));

        Assert.Equal(168, collection.Count);
        var reset = Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Reset, reset.Action);
    }
}
