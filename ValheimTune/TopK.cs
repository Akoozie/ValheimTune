using System;

namespace ValheimTune
{
    // Pure: no game types. Bounded-heap partial sort: reorders the first min(k, n) elements of
    // `items` in place to the k lowest-ranked items in ascending rank order. O(n log k), no
    // allocation once the static scratch buffers have grown to the largest n seen. Not thread-safe,
    // main thread only.
    public static class TopK
    {
        private static double[] s_rank;
        private static int[] s_heap;
        private static bool[] s_taken;

        private static class Buf<T>
        {
            public static T[] Items;
        }

        public static void Select<T>(System.Collections.Generic.List<T> items, Func<T, double> rank, int k)
        {
            int n = items.Count;
            if (n == 0 || k <= 0) return;
            if (k > n) k = n;

            if (s_rank == null || s_rank.Length < n)
            {
                s_rank = new double[n];
                s_heap = new int[n];
                s_taken = new bool[n];
            }
            if (Buf<T>.Items == null || Buf<T>.Items.Length < n) Buf<T>.Items = new T[n];

            var rankArr = s_rank;
            var heap = s_heap;
            var taken = s_taken;
            var buf = Buf<T>.Items;

            for (int i = 0; i < n; i++)
            {
                buf[i] = items[i];
                rankArr[i] = rank(items[i]);
                taken[i] = false;
            }

            // Max-heap of indices sized k; root is the worst (largest rank) of the current best k.
            int heapCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (heapCount < k)
                {
                    heap[heapCount] = i;
                    heapCount++;
                    SiftUp(heap, rankArr, heapCount - 1);
                }
                else if (rankArr[i] < rankArr[heap[0]])
                {
                    heap[0] = i;
                    SiftDown(heap, rankArr, 0, heapCount);
                }
            }

            // Pop worst-first into positions k-1..0 so the final order is ascending by rank.
            int count = heapCount;
            for (int pos = count - 1; pos >= 0; pos--)
            {
                int idx = heap[0];
                taken[idx] = true;
                items[pos] = buf[idx];
                heapCount--;
                heap[0] = heap[heapCount];
                SiftDown(heap, rankArr, 0, heapCount);
            }

            int w = count;
            for (int i = 0; i < n; i++)
                if (!taken[i]) items[w++] = buf[i];
        }

        private static void SiftUp(int[] heap, double[] rankArr, int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (rankArr[heap[parent]] >= rankArr[heap[i]]) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private static void SiftDown(int[] heap, double[] rankArr, int i, int count)
        {
            while (true)
            {
                int left = 2 * i + 1, right = 2 * i + 2, largest = i;
                if (left < count && rankArr[heap[left]] > rankArr[heap[largest]]) largest = left;
                if (right < count && rankArr[heap[right]] > rankArr[heap[largest]]) largest = right;
                if (largest == i) break;
                (heap[i], heap[largest]) = (heap[largest], heap[i]);
                i = largest;
            }
        }
    }
}
