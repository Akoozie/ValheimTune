using System.Collections.Generic;
using System.Linq;
using ValheimTune;
using Xunit;

public class SaveSlicerTests
{
    [Fact]
    public void ProcessesEveryItemAndYieldsWhenBudgetIsSpent()
    {
        var items = Enumerable.Range(0, 10).ToList();
        var seen = new List<int>();
        double fakeClock = 0;
        int yields = SaveSlicer.Run(items,
            work: i => { seen.Add(i); fakeClock += 2; },   // each item costs 2 ms
            clockMs: () => fakeClock,
            budgetMs: 5).Count();

        Assert.Equal(items, seen);
        Assert.Equal(3, yields);   // 10 items x 2 ms: budget crossed after the 3rd, 6th and 9th item; never after the last
    }

    [Fact]
    public void EmptyListYieldsNothing()
    {
        Assert.Empty(SaveSlicer.Run(new List<int>(), _ => { }, () => 0, 5));
    }
}
