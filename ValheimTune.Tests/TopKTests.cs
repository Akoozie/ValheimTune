using System;
using System.Collections.Generic;
using System.Linq;
using ValheimTune;
using Xunit;

public class TopKTests
{
    [Fact]
    public void FirstKMatchFullSortRemainderIsComplement()
    {
        var rnd = new Random(42);
        var items = new List<double>();
        for (int i = 0; i < 1000; i++) items.Add(rnd.NextDouble());
        var expectedSorted = items.OrderBy(x => x).ToList();

        TopK.Select(items, x => x, 50);

        Assert.Equal(expectedSorted.Take(50), items.Take(50));
        Assert.Equal(expectedSorted.Skip(50).OrderBy(x => x), items.Skip(50).OrderBy(x => x));
    }

    [Fact]
    public void KGreaterThanNSortsWholeList()
    {
        var items = new List<double> { 5, 3, 8, 1, 9, 2, 7, 4, 6, 0 };
        var expected = items.OrderBy(x => x).ToList();

        TopK.Select(items, x => x, 50);

        Assert.Equal(expected, items);
    }

    [Fact]
    public void ZeroKAndEmptyListAreNoOps()
    {
        var items = new List<double> { 3, 1, 2 };
        var original = new List<double>(items);
        TopK.Select(items, x => x, 0);
        Assert.Equal(original, items);

        var empty = new List<double>();
        TopK.Select(empty, x => x, 5);
        Assert.Empty(empty);
    }

    [Fact]
    public void BuffersRegrowAndReuseAcrossCallSizes()
    {
        foreach (int size in new[] { 1000, 5000, 20 })
        {
            var rnd = new Random(size);
            var items = Enumerable.Range(0, size).Select(_ => (double)rnd.Next(0, 100000)).ToList();
            int k = Math.Min(10, size);
            var expected = items.OrderBy(x => x).Take(k).ToList();

            TopK.Select(items, x => x, 10);

            Assert.Equal(size, items.Count);
            Assert.Equal(expected, items.Take(k));
        }
    }

    [Fact]
    public void TiedRanksKeepAllItemsNoException()
    {
        var items = Enumerable.Repeat(5.0, 20).ToList();
        TopK.Select(items, x => x, 3);
        Assert.Equal(20, items.Count);
        Assert.All(items, x => Assert.Equal(5.0, x));
    }
}
