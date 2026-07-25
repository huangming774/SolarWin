using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SolarWin.Helpers;

/// <summary>
/// ObservableCollection that can apply multi-item mutations with a single Reset notification,
/// avoiding N× CollectionChanged/layout passes when binding to ListView.
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    private int _suppress;

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppress == 0)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_suppress == 0)
        {
            base.OnPropertyChanged(e);
        }
    }

    /// <summary>Replace entire contents with one Reset notification.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        CheckReentrancy();

        _suppress++;
        try
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppress--;
        }

        RaiseReset();
    }

    /// <summary>Append many items with one Reset notification.</summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var list = items as IList<T> ?? items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        _suppress++;
        try
        {
            foreach (var item in list)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppress--;
        }

        RaiseReset();
    }

    /// <summary>Insert many items at <paramref name="index"/> with one Reset notification.</summary>
    public void InsertRange(int index, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (index < 0 || index > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var list = items as IList<T> ?? items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        _suppress++;
        try
        {
            for (var i = 0; i < list.Count; i++)
            {
                Items.Insert(index + i, list[i]);
            }
        }
        finally
        {
            _suppress--;
        }

        RaiseReset();
    }

    /// <summary>Remove <paramref name="count"/> items starting at <paramref name="index"/> with one Reset.</summary>
    public void RemoveRange(int index, int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index + count > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        CheckReentrancy();
        _suppress++;
        try
        {
            for (var i = 0; i < count; i++)
            {
                Items.RemoveAt(index);
            }
        }
        finally
        {
            _suppress--;
        }

        RaiseReset();
    }

    private void RaiseReset()
    {
        base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        base.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
