using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class Virtualiser<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Observable<VirtualRequest> _requests;

    public Virtualiser(Observable<IChangeSet<T>> source, Observable<VirtualRequest> requests)
    {
        _source = source;
        _requests = requests;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var allItems = new List<T>();
            var currentRequest = new VirtualRequest(0, int.MaxValue);

            var requestsSub = _requests.Subscribe(request =>
            {
                currentRequest = request;

                // Emit current virtual window
                EmitVirtualWindow(allItems, currentRequest, observer);
            });

            var sourceSub = _source.Subscribe(
                changes =>
                {
                    // Apply changes to full list
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                allItems.Insert(change.CurrentIndex, change.Item);
                                break;

                            case ListChangeReason.AddRange:
                                allItems.InsertRange(change.CurrentIndex, change.Range);
                                break;

                            case ListChangeReason.Remove:
                                allItems.RemoveAt(change.CurrentIndex);
                                break;

                            case ListChangeReason.RemoveRange:
                                for (int i = 0; i < change.Range.Count; i++)
                                {
                                    allItems.RemoveAt(change.CurrentIndex);
                                }

                                break;

                            case ListChangeReason.Replace:
                                allItems[change.CurrentIndex] = change.Item;
                                break;

                            case ListChangeReason.Moved:
                                var movedItem = allItems[change.PreviousIndex];
                                allItems.RemoveAt(change.PreviousIndex);
                                allItems.Insert(change.CurrentIndex, movedItem);
                                break;

                            case ListChangeReason.Clear:
                                allItems.Clear();
                                break;
                        }
                    }

                    // Emit current virtual window
                    EmitVirtualWindow(allItems, currentRequest, observer);
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return R3.Disposable.Create(() =>
            {
                requestsSub.Dispose();
                sourceSub.Dispose();
            });
        });
    }

    private void EmitVirtualWindow(List<T> allItems, VirtualRequest request, Observer<IChangeSet<T>> observer)
    {
        var startIndex = Math.Min(request.StartIndex, allItems.Count);
        var endIndex = Math.Min(startIndex + request.Size, allItems.Count);
        var windowItems = allItems.Skip(startIndex).Take(endIndex - startIndex).ToList();

        var changes = new List<Change<T>>();
        for (int i = 0; i < windowItems.Count; i++)
        {
            changes.Add(new Change<T>(ListChangeReason.Add, windowItems[i], i));
        }

        if (changes.Count > 0)
        {
            observer.OnNext(new ChangeSet<T>(changes));
        }
    }
}
