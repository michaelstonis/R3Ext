namespace R3Ext.Tests;

public class ShuffleTests
{
    [Fact]
    public void ShuffleIsDeterministicWithSeed()
    {
        int[] data1 = Enumerable.Range(0, 20).ToArray();
        int[] data2 = Enumerable.Range(0, 20).ToArray();

        Random r1 = new(12345);
        Random r2 = new(12345);

        data1.Shuffle(r1);
        data2.Shuffle(r2);

        Assert.Equal(data1, data2); // same seed yields same permutation

        Array.Sort(data1);
        Array.Sort(data2);
        Assert.Equal(Enumerable.Range(0, 20).ToArray(), data1);
        Assert.Equal(Enumerable.Range(0, 20).ToArray(), data2);
    }
}
