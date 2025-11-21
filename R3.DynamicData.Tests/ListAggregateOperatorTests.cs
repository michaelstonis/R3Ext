// Port of DynamicData to R3.

using R3.DynamicData.List;
using Xunit;

namespace R3.DynamicData.Tests;

public class ListAggregateOperatorTests
{
    private class Person
    {
        public int Age { get; set; }
    }

    [Fact]
    public void Max_Min_IntSelector()
    {
        var list = new SourceList<Person>();
        var maxValues = new List<int>();
        var minValues = new List<int>();

        list.Connect().Max(p => p.Age).Subscribe(v => maxValues.Add(v));
        list.Connect().Min(p => p.Age).Subscribe(v => minValues.Add(v));

        list.Add(new Person { Age = 10 }); // max=10, min=10
        list.Add(new Person { Age = 5 });  // max=10, min=5
        list.Add(new Person { Age = 20 }); // max=20, min=5
        list.RemoveAt(0);                  // removed 10 -> max still 20, min=5
        list.RemoveAt(1);                  // removed 20 -> remaining {5} -> max=5, min=5
        list.Clear();                      // empty -> max=0, min=0 (default)

        Assert.Equal(new[] { 10, 10, 20, 20, 5, 0 }, maxValues);
        Assert.Equal(new[] { 10, 5, 5, 5, 5, 0 }, minValues);
    }

    [Fact]
    public void Average_And_StdDev()
    {
        var list = new SourceList<Person>();
        var avgValues = new List<double>();
        var stdValues = new List<double>();

        list.Connect().Avg(p => p.Age).Subscribe(v => avgValues.Add(v));
        list.Connect().StdDev(p => p.Age).Subscribe(v => stdValues.Add(v));

        list.Add(new Person { Age = 10 });      // avg=10, std=0
        list.Add(new Person { Age = 20 });      // avg=15, std=sqrt(25)=5
        list.Add(new Person { Age = 30 });      // avg=20, std=sqrt(66.666...)≈8.1649658
        list.RemoveAt(1);                       // remove 20 -> remaining 10,30 avg=20 std=10
        list.ReplaceAt(0, new Person { Age = 5 }); // list 5,30 avg=17.5 std≈12.5
        list.Clear();                           // avg=0 std=0

        Assert.Equal(6, avgValues.Count);
        Assert.Equal(6, stdValues.Count);

        Assert.Equal(10.0, avgValues[0]);
        Assert.Equal(15.0, avgValues[1]);
        Assert.Equal(20.0, avgValues[2]);
        Assert.Equal(20.0, avgValues[3]);
        Assert.Equal(17.5, avgValues[4]);
        Assert.Equal(0.0, avgValues[5]);

        Assert.Equal(0.0, stdValues[0]);
        Assert.Equal(5.0, stdValues[1]);
        Assert.True(Math.Abs(stdValues[2] - Math.Sqrt(66.66666666666667)) < 1e-6);
        Assert.Equal(10.0, stdValues[3]);
        Assert.Equal(12.5, stdValues[4]);
        Assert.Equal(0.0, stdValues[5]);
    }
}
