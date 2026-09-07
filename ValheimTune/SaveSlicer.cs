using System;
using System.Collections.Generic;

namespace ValheimTune
{
    public static class SaveSlicer
    {
        // Calls work(item) for each item; after each item, if clock() - sliceStart >= budgetMs, yields true (caller yields a frame).
        public static IEnumerable<bool> Run<T>(IReadOnlyList<T> items, Action<T> work, Func<double> clockMs, double budgetMs)
        {
            double sliceStart = clockMs();
            for (int i = 0; i < items.Count; i++)
            {
                work(items[i]);
                if (i == items.Count - 1) yield break;
                if (clockMs() - sliceStart >= budgetMs)
                {
                    yield return true;
                    sliceStart = clockMs();
                }
            }
        }
    }
}
