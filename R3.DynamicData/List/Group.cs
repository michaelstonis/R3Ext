// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

public sealed class Group<TKey, T>
{
    public Group(TKey key)
    {
        Key = key;
        Items = new ChangeAwareList<T>();
    }

    public TKey Key { get; }

    public ChangeAwareList<T> Items { get; }
}
