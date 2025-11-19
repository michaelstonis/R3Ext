using R3;

namespace R3Ext.Tests;

public class NullAndFilterTests
{
    [Fact]
    public async Task WhereIsNotNull_Class_FiltersNulls()
    {
        string?[] data = { "a", null, "b", };
        Observable<string> obs = Observable.ToObservable(data).WhereIsNotNull();
        string[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { "a", "b", }, arr);
    }

    [Fact]
    public async Task WhereIsNotNull_Struct_FiltersNullables()
    {
        int?[] data = { 1, null, 3, };
        Observable<int> obs = Observable.ToObservable(data).WhereIsNotNull();
        int[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 1, 3, }, arr);
    }

    [Fact]
    public void Filter_NullPattern_Throws()
    {
        Observable<string> src = CreationExtensions.FromArray("a", "bb");
        Assert.Throws<ArgumentNullException>(() => src.Filter(null!));
    }

    [Fact]
    public async Task Filter_Regex_Matches()
    {
        Observable<string> src = CreationExtensions.FromArray("a", "bb", "ccc");
        string[] arr = await src.Filter("^b+").ToArrayAsync();
        Assert.Equal(new[] { "bb", }, arr);
    }
}
