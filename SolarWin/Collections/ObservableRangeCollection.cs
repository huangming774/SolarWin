using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SolarWin.Collections;

/// <summary>Replaces all items while raising one Reset notification.</summary>
public sealed class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public void ReplaceWith(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Items.Clear();
        foreach (var value in values) Items.Add(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
