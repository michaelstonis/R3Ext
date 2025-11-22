// Port of DynamicData to R3.
// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.

using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// Extensions for creating observable caches.
/// </summary>
public static class ObservableCacheExtensions
{
    /// <summary>
    /// Converts a SourceCache to a read-only IObservableCache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source cache.</param>
    /// <returns>A read-only observable cache.</returns>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new ObservableCacheAdapter<TObject, TKey>(source);
    }

    private sealed class ObservableCacheAdapter<TObject, TKey> : IObservableCache<TObject, TKey>
        where TKey : notnull
    {
        private readonly ISourceCache<TObject, TKey> _source;

        public ObservableCacheAdapter(ISourceCache<TObject, TKey> source)
        {
            _source = source;
        }

        public int Count => _source.Count;

        public IReadOnlyList<TObject> Items => _source.Items.ToList();

        public IReadOnlyList<TKey> Keys => _source.Keys.ToList();

        public IReadOnlyDictionary<TKey, TObject> KeyValues
        {
            get
            {
                var dict = new Dictionary<TKey, TObject>();
                foreach (var kvp in _source.KeyValues)
                {
                    dict[kvp.Value] = kvp.Key;
                }

                return dict;
            }
        }

        public Observable<int> CountChanged => _source.CountChanged;

        public Kernel.Optional<TObject> Lookup(TKey key) => _source.Lookup(key);

        public Observable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        {
            if (predicate == null)
            {
                return _source.Connect();
            }

            return _source.Connect(predicate);
        }

        public Observable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null)
        {
            // Preview doesn't include initial state, so skip the first emission if there's data
            var connected = Connect(predicate, suppressEmptyChangeSets: true);
            return connected.Skip(1);
        }

        public Observable<Change<TObject, TKey>> Watch(TKey key) => _source.Watch(key);

        public void Dispose()
        {
            // Don't dispose the source cache - we're just wrapping it
        }
    }
}
