// Port of DynamicData to R3.

namespace R3.DynamicData.List;

/// <summary>
/// Represents a group of items with a common key.
/// </summary>
/// <typeparam name="TKey">The type of the key used to group items.</typeparam>
/// <typeparam name="T">The type of items in the group.</typeparam>
public sealed class Group<TKey, T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Group{TKey, T}"/> class.
    /// </summary>
    /// <param name="key">The key that identifies this group.</param>
    public Group(TKey key)
    {
        Key = key;
        Items = new ChangeAwareList<T>();
    }

    /// <summary>
    /// Gets the key that identifies this group.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the collection of items in this group.
    /// </summary>
    public ChangeAwareList<T> Items { get; }
}
