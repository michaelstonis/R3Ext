// Port of DynamicData to R3 - Tests.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public sealed class NewOperatorsTests : IDisposable
{
    private readonly SourceList<int> _source;
    private readonly List<IChangeSet<int>> _results;
    private IDisposable? _subscription;

    public NewOperatorsTests()
    {
        _source = new SourceList<int>();
        _results = new List<IChangeSet<int>>();
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void Clone_CopiesChangesToTargetList()
    {
        var target = new List<int>();
        _subscription = _source.Connect().Clone(target).Subscribe(_results.Add);

        _source.Add(1);
        _source.Add(2);
        _source.Add(3);

        Assert.Equal(new[] { 1, 2, 3 }, target);
        Assert.Equal(3, _results.Count);
    }

    [Fact]
    public void Cast_ConvertsItemTypes()
    {
        var stringSource = new SourceList<string>();
        var castResults = new List<IChangeSet<object>>();

        using var subscription = stringSource.Connect()
            .Cast<string, object>()
            .Subscribe(castResults.Add);

        stringSource.Add("one");
        stringSource.Add("two");

        Assert.Equal(2, castResults.Count);
        Assert.Equal("one", castResults[0].First().Item);
        Assert.Equal("two", castResults[1].First().Item);

        stringSource.Dispose();
    }

    [Fact]
    public void DeferUntilLoaded_WaitsForData()
    {
        _subscription = _source.Connect().DeferUntilLoaded().Subscribe(_results.Add);

        Assert.Empty(_results);

        _source.Add(1);

        Assert.Single(_results);
        Assert.Single(_results[0]);
    }

    [Fact]
    public void StartWithEmpty_PrependsEmptyChangeset()
    {
        _subscription = _source.Connect().StartWithEmpty().Subscribe(_results.Add);

        Assert.Single(_results);
        Assert.Empty(_results[0]);

        _source.Add(1);

        Assert.Equal(2, _results.Count);
    }

    [Fact]
    public void WhereReasonsAre_FiltersCorrectly()
    {
        _subscription = _source.Connect()
            .WhereReasonsAre(ListChangeReason.Add)
            .Subscribe(_results.Add);

        _source.Add(1);
        _source.Add(2);
        _source.RemoveAt(0);

        Assert.Equal(2, _results.Count);
        Assert.True(_results.All(cs => cs.All(c => c.Reason == ListChangeReason.Add)));
    }

    [Fact]
    public void NotEmpty_SuppressesEmptyChangesets()
    {
        _subscription = _source.Connect()
            .WhereReasonsAre(ListChangeReason.Add, ListChangeReason.Remove)
            .NotEmpty()
            .Subscribe(_results.Add);

        _source.Add(1);
        _source.Add(2);

        Assert.Equal(2, _results.Count);
        Assert.True(_results.All(cs => cs.Count > 0));
    }

    [Fact]
    public void QueryWhenChanged_ExposesCurrentState()
    {
        var queries = new List<IReadOnlyList<int>>();

        _subscription = _source.Connect()
            .QueryWhenChanged()
            .Subscribe(queries.Add);

        _source.Add(1);
        _source.Add(2);
        _source.Add(3);

        Assert.Equal(3, queries.Count);
        Assert.Equal(new[] { 1, 2, 3 }, queries[2]);
    }

    [Fact]
    public void ToCollection_ReturnsReadOnlyList()
    {
        var collections = new List<IReadOnlyCollection<int>>();

        _subscription = _source.Connect()
            .ToCollection()
            .Subscribe(collections.Add);

        _source.Add(1);
        _source.Add(2);

        Assert.Equal(2, collections.Count);
        Assert.Equal(new[] { 1, 2 }, collections[1]);
    }

    [Fact]
    public void And_IntersectsMultipleSources()
    {
        var source2 = new SourceList<int>();

        _subscription = _source.Connect()
            .And(source2.Connect())
            .Subscribe(_results.Add);

        _source.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 2, 3, 4 });

        var allItems = _results.SelectMany(cs => cs.Select(c => c.Item)).ToHashSet();
        Assert.Equal(new[] { 2, 3 }, allItems.OrderBy(x => x));

        source2.Dispose();
    }

    [Fact]
    public void Or_UnionsMultipleSources()
    {
        var source2 = new SourceList<int>();

        _subscription = _source.Connect()
            .Or(source2.Connect())
            .Subscribe(_results.Add);

        _source.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 3, 4, 5 });

        var allItems = _results.SelectMany(cs => cs.Select(c => c.Item)).ToHashSet();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, allItems.OrderBy(x => x));

        source2.Dispose();
    }

    [Fact]
    public void BufferIf_PausesAndResumes()
    {
        var pauseSignal = new Subject<bool>();

        _subscription = _source.Connect()
            .BufferIf(pauseSignal)
            .Subscribe(_results.Add);

        _source.Add(1);
        Assert.Single(_results);

        // Pause
        pauseSignal.OnNext(true);
        _source.Add(2);
        _source.Add(3);

        Assert.Single(_results);

        // Resume
        pauseSignal.OnNext(false);

        Assert.Equal(2, _results.Count);
        Assert.Equal(2, _results[1].Count);

        pauseSignal.Dispose();
    }

    [Fact]
    public void Switch_SwitchesObservables()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var switcher = new Subject<Observable<IChangeSet<int>>>();

        _subscription = switcher.Switch().Subscribe(_results.Add);

        // Switch to source1
        switcher.OnNext(source1.Connect());
        source1.Add(1);

        Assert.Single(_results);
        Assert.Equal(1, _results[0].First().Item);

        // Switch to source2
        switcher.OnNext(source2.Connect());
        source2.Add(2);

        Assert.Equal(2, _results.Count);
        Assert.Equal(2, _results[1].First().Item);

        // source1 changes should be ignored now
        source1.Add(3);
        Assert.Equal(2, _results.Count);

        source1.Dispose();
        source2.Dispose();
        switcher.Dispose();
    }

    [Fact]
    public void ToObservableChangeSet_ConvertsObservable()
    {
        var subject = new Subject<int>();

        _subscription = subject.ToObservableChangeSet().Subscribe(_results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        Assert.Equal(3, _results.Count);
        Assert.Equal(1, _results[0].First().Item);
        Assert.Equal(2, _results[1].First().Item);
        Assert.Equal(3, _results[2].First().Item);

        subject.Dispose();
    }

    [Fact]
    public void ToObservableChangeSet_WithSizeLimit()
    {
        var subject = new Subject<int>();

        _subscription = subject.ToObservableChangeSet(limitSizeTo: 3).Subscribe(_results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4); // Should trigger removal of first item

        Assert.Equal(5, _results.Count);

        var allChanges = _results.SelectMany(cs => cs).ToList();
        var adds = allChanges.Where(c => c.Reason == ListChangeReason.Add).Select(c => c.Item).ToList();
        var removes = allChanges.Where(c => c.Reason == ListChangeReason.Remove).Select(c => c.Item).ToList();

        Assert.Equal(new[] { 1, 2, 3, 4 }, adds);
        Assert.Single(removes);
        Assert.Equal(1, removes[0]);

        subject.Dispose();
    }

    [Fact]
    public void ToObservableChangeSet_FromEnumerable()
    {
        var subject = new Subject<IEnumerable<int>>();

        _subscription = subject.ToObservableChangeSet().Subscribe(_results.Add);

        subject.OnNext(new[] { 1, 2 });
        subject.OnNext(new[] { 3, 4, 5 });

        Assert.Equal(2, _results.Count);
        Assert.Equal(2, _results[0].Count);
        Assert.Equal(3, _results[1].Count);

        subject.Dispose();
    }
}
