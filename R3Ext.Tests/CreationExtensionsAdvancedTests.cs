using System;
using R3;

namespace R3Ext.Tests;

public class CreationExtensionsAdvancedTests
{
    [Fact]
    public async Task FromArray_NullValues_EmitsNulls()
    {
        Observable<string?> obs = CreationExtensions.FromArray<string?>("a", null, "b", null);
        string?[] arr = await obs.ToArrayAsync();
        Assert.Equal(4, arr.Length);
        Assert.Equal("a", arr[0]);
        Assert.Null(arr[1]);
        Assert.Equal("b", arr[2]);
        Assert.Null(arr[3]);
    }

    [Fact]
    public async Task FromArray_SingleItem_EmitsAndCompletes()
    {
        Observable<int> obs = CreationExtensions.FromArray(42);
        int[] arr = await obs.ToArrayAsync();
        Assert.Single(arr);
        Assert.Equal(42, arr[0]);
    }

    [Fact]
    public async Task FromArray_EmptyArray_CompletesImmediately()
    {
        Observable<int> obs = CreationExtensions.FromArray<int>();
        int[] arr = await obs.ToArrayAsync();
        Assert.Empty(arr);
    }

    [Fact]
    public async Task FromArray_LargeArray_EmitsAllItems()
    {
        var items = Enumerable.Range(0, 1000).ToArray();
        Observable<int> obs = CreationExtensions.FromArray(items);
        int[] arr = await obs.ToArrayAsync();
        Assert.Equal(1000, arr.Length);
        Assert.Equal(items, arr);
    }

    [Fact]
    public async Task FromArray_ComplexTypes_EmitsCorrectly()
    {
        var person1 = new Person("Alice", 30);
        var person2 = new Person("Bob", 25);
        Observable<Person> obs = CreationExtensions.FromArray(person1, person2);
        Person[] arr = await obs.ToArrayAsync();
        Assert.Equal(2, arr.Length);
        Assert.Equal(person1, arr[0]);
        Assert.Equal(person2, arr[1]);
    }

    [Fact]
    public async Task Using_DisposesResourceOnSuccess()
    {
        var resource = new TestResource();
        Observable<int> obs = CreationExtensions.Using(
            () => resource,
            r =>
            {
                Assert.False(r.Disposed);
                return Observable.Return(42);
            });

        int[] arr = await obs.ToArrayAsync();
        Assert.Single(arr);
        Assert.Equal(42, arr[0]);
        Assert.True(resource.Disposed);
    }

    [Fact]
    public async Task Using_DisposesResourceOnError()
    {
        var resource = new TestResource();
        Observable<int> obs = CreationExtensions.Using(
            () => resource,
            r => Observable.Range(1, 3).Select(x => x == 2 ? throw new Exception("error") : x));

        await Assert.ThrowsAsync<Exception>(async () => await obs.ToArrayAsync());
        Assert.True(resource.Disposed);
    }

    [Fact]
    public async Task Using_DisposesResourceOnDisposal()
    {
        var resource = new TestResource();
        var subject = new Subject<int>();
        Observable<int> obs = CreationExtensions.Using(
            () => resource,
            r => subject);

        IDisposable subscription = obs.Subscribe(_ => { });
        Assert.False(resource.Disposed);

        subscription.Dispose();
        Assert.True(resource.Disposed);
    }

    [Fact]
    public void Using_NullResourceFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreationExtensions.Using<TestResource, int>(
                null!,
                r => Observable.Return(1)));
    }

    [Fact]
    public void Using_NullObservableFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreationExtensions.Using<TestResource, int>(
                () => new TestResource(),
                null!));
    }

    [Fact]
    public async Task Using_ResourceFactoryThrows_PropagatesException()
    {
        Observable<int> obs = CreationExtensions.Using<TestResource, int>(
            () => throw new InvalidOperationException("factory error"),
            r => Observable.Return(1));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await obs.ToArrayAsync());
    }

    [Fact]
    public async Task Using_ObservableFactoryThrows_PropagatesException()
    {
        var resource = new TestResource();
        Observable<int> obs = CreationExtensions.Using<TestResource, int>(
            () => resource,
            r => throw new InvalidOperationException("observable error"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await obs.ToArrayAsync());

        // Note: Resource is NOT disposed if factory throws before returning observable
        // This is consistent with R3's Observable.Create behavior
        Assert.False(resource.Disposed);
    }

    [Fact]
    public async Task Start_Action_NullAction_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            Observable<Unit> obs = CreationExtensions.Start(null!);
            await obs.ToArrayAsync();
        });
    }

    [Fact]
    public async Task Start_Action_ExecutesAction()
    {
        int count = 0;
        Observable<Unit> obs = CreationExtensions.Start(() => { count++; });
        Unit[] arr = await obs.ToArrayAsync();
        Assert.Equal(1, count);
        Assert.Single(arr);
        Assert.Equal(Unit.Default, arr[0]);
    }

    [Fact]
    public async Task Start_Action_WithConfigureAwaitFalse()
    {
        bool executed = false;
        Observable<Unit> obs = CreationExtensions.Start(() => { executed = true; }, configureAwait: false);
        await obs.ToArrayAsync();
        Assert.True(executed);
    }

    [Fact]
    public async Task Start_Action_ThrowsException_PropagatesError()
    {
        Observable<Unit> obs = CreationExtensions.Start(() => throw new InvalidOperationException("test error"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await obs.ToArrayAsync());
    }

    [Fact]
    public async Task Start_Func_NullFunc_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            Observable<int> obs = CreationExtensions.Start<int>(null!);
            await obs.ToArrayAsync();
        });
    }

    [Fact]
    public async Task Start_Func_ReturnsValue()
    {
        Observable<int> obs = CreationExtensions.Start(() => 123);
        int[] arr = await obs.ToArrayAsync();
        Assert.Single(arr);
        Assert.Equal(123, arr[0]);
    }

    [Fact]
    public async Task Start_Func_WithConfigureAwaitFalse()
    {
        Observable<string> obs = CreationExtensions.Start(() => "result", configureAwait: false);
        string[] arr = await obs.ToArrayAsync();
        Assert.Single(arr);
        Assert.Equal("result", arr[0]);
    }

    [Fact]
    public async Task Start_Func_ThrowsException_PropagatesError()
    {
        Observable<int> obs = CreationExtensions.Start<int>(() => throw new InvalidOperationException("func error"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await obs.ToArrayAsync());
    }

    [Fact]
    public async Task Start_Func_ReturnsNull_EmitsNull()
    {
        Observable<string?> obs = CreationExtensions.Start<string?>(() => null);
        string?[] arr = await obs.ToArrayAsync();
        Assert.Single(arr);
        Assert.Null(arr[0]);
    }

    [Fact]
    public async Task Start_Func_ComplexComputation()
    {
        Observable<int> obs = CreationExtensions.Start(() =>
        {
            int sum = 0;
            for (int i = 0; i < 100; i++)
            {
                sum += i;
            }

            return sum;
        });

        int[] arr = await obs.ToArrayAsync();
        Assert.Single(arr);
        Assert.Equal(4950, arr[0]);
    }

    [Fact]
    public async Task Using_MultipleSubscribers_CreatesNewResourcePerSubscriber()
    {
        int resourceCount = 0;
        Observable<int> obs = CreationExtensions.Using(
            () =>
            {
                resourceCount++;
                return new TestResource();
            },
            r => Observable.Return(1));

        await obs.ToArrayAsync();
        await obs.ToArrayAsync();
        await obs.ToArrayAsync();

        Assert.Equal(3, resourceCount);
    }

    [Fact]
    public async Task Using_ResourceUsedInObservable()
    {
        Observable<string> obs = CreationExtensions.Using(
            () => new TestResource { Value = 42 },
            r => Observable.Return(r.Value.ToString()));

        string[] arr = await obs.ToArrayAsync();
        Assert.Single(arr);
        Assert.Equal("42", arr[0]);
    }

    private record Person(string Name, int Age);

    private class TestResource : IDisposable
    {
        public bool Disposed { get; private set; }

        public int Value { get; set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
