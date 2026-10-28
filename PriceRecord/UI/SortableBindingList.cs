using System.ComponentModel;

namespace PriceRecord.UI;

/// <summary>BindingList that supports DataGridView column-header sorting.</summary>
public sealed class SortableBindingList<T> : BindingList<T>
{
    private PropertyDescriptor? _sortProperty;
    private ListSortDirection _sortDirection;
    private bool _isSorted;

    protected override bool SupportsSortingCore => true;
    protected override bool IsSortedCore => _isSorted;
    protected override PropertyDescriptor? SortPropertyCore => _sortProperty;
    protected override ListSortDirection SortDirectionCore => _sortDirection;

    protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
    {
        if (Items is not List<T> items) return;

        var comparer = Comparer<object?>.Create((a, b) =>
        {
            if (a is null && b is null) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            return a is IComparable ca ? ca.CompareTo(b) : string.CompareOrdinal(a.ToString(), b.ToString());
        });

        items.Sort((x, y) =>
        {
            var result = comparer.Compare(prop.GetValue(x), prop.GetValue(y));
            return direction == ListSortDirection.Ascending ? result : -result;
        });

        _sortProperty = prop;
        _sortDirection = direction;
        _isSorted = true;
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }

    protected override void RemoveSortCore()
    {
        _isSorted = false;
        _sortProperty = null;
    }

    /// <summary>Replaces the contents with one Reset notification instead of one per item.</summary>
    public void ResetItems(IEnumerable<T> items)
    {
        RaiseListChangedEvents = false;
        try
        {
            ClearItems();
            foreach (var item in items) Add(item);
        }
        finally
        {
            RaiseListChangedEvents = true;
        }

        if (_isSorted && _sortProperty is not null)
            ApplySortCore(_sortProperty, _sortDirection);
        else
            ResetBindings();
    }
}
