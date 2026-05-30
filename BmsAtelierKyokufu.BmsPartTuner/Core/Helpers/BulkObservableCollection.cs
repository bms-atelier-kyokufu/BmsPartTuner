using System.Collections.Specialized;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// 複数アイテムの一括追加（AddRange）や一括置き換え（ReplaceAll）時に、変更通知（CollectionChanged）の発生を
/// 1回のみに抑制できる、パフォーマンス最適化された ObservableCollection です。
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _isNotificationSuspended;

    /// <summary>
    /// 新しいインスタンスを初期化します。
    /// </summary>
    public BulkObservableCollection() : base() { }

    /// <summary>
    /// 指定されたコレクションの要素をコピーして、新しいインスタンスを初期化します。
    /// </summary>
    public BulkObservableCollection(IEnumerable<T> collection) : base(collection) { }

    /// <summary>
    /// 変更通知を一時的に停止します。
    /// </summary>
    public void SuspendNotifications()
    {
        _isNotificationSuspended = true;
    }

    /// <summary>
    /// 変更通知を再開し、コレクションのリセットイベントを発生させます。
    /// </summary>
    public void ResumeNotifications()
    {
        _isNotificationSuspended = false;
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// 変更通知の発生を抑止した状態で、指定したコレクションの要素を一括で追加します。
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        SuspendNotifications();
        try
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            ResumeNotifications();
        }
    }

    /// <summary>
    /// 変更通知の発生を抑止した状態で、現在のコレクションをクリアし、指定したコレクションの要素を一括で追加します。
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        SuspendNotifications();
        try
        {
            ClearItems();
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            ResumeNotifications();
        }
    }

    /// <inheritdoc />
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_isNotificationSuspended)
        {
            base.OnCollectionChanged(e);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_isNotificationSuspended)
        {
            base.OnPropertyChanged(e);
        }
    }
}
