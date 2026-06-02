namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Virtual;

/// <summary>
/// 音声比較における置換テーブル（Union-Find）と非マッチテーブル（Anti-Set）のスレッドセーフな状態管理を提供します。
/// </summary>
internal class ThreadSafeReplaceTable(int[] replaceTable, long[] fileSizes)
{
    private readonly int[] _replaceTable = replaceTable ?? throw new ArgumentNullException(nameof(replaceTable));
    private readonly long[] _fileSizes = fileSizes ?? throw new ArgumentNullException(nameof(fileSizes));
    private readonly long[] _antiSet = new long[((MaxBmsDefNum * MaxBmsDefNum) / 64) + 1];
    private const int MaxBmsDefNum = 3844;

    /// <summary>
    /// 自分自身を置換テーブルにマークします。
    /// </summary>
    public void MarkSelf(int fileNum)
    {
        if (_replaceTable[fileNum] == 0)
        {
            System.Threading.Interlocked.CompareExchange(ref _replaceTable[fileNum], fileNum, 0);
        }
    }

    /// <summary>
    /// 副作用なしでルートを検索します。Anti-Setの参照など、並列競合を避けるために使用します。
    /// </summary>
    public int FindRead(int fileNum)
    {
        int current = fileNum;
        int parent = _replaceTable[current];
        if (parent == 0 || parent == current) return current;

#if DEBUG
        int depth = 0;
#endif
        do
        {
            current = parent;
            parent = _replaceTable[current];
#if DEBUG
            if (depth++ > MaxBmsDefNum)
            {
                throw new InvalidOperationException("Union-Find cycle detected in FindRead.");
            }
#endif
        } while (parent != 0 && parent != current);

        return current;
    }

    /// <summary>
    /// Union-Findのルート検索を行います。アロケーションフリーの2パス経路圧縮を行います。
    /// </summary>
    public int FindRoot(int fileNum)
    {
        int root = fileNum;
        int p = _replaceTable[root];
        if (p != 0 && p != root)
        {
#if DEBUG
            int depth = 0;
#endif
            do
            {
                root = p;
                p = _replaceTable[root];
#if DEBUG
                if (depth++ > MaxBmsDefNum)
                {
                    throw new InvalidOperationException("Union-Find cycle detected in FindRoot.");
                }
#endif
            } while (p != 0 && p != root);
        }

        int current = fileNum;
        if (current != root)
        {
#if DEBUG
            int depth = 0;
#endif
            do
            {
                int next = _replaceTable[current];
                if (next == 0 || next == current) break;
                _replaceTable[current] = root;
                current = next;
#if DEBUG
                if (depth++ > MaxBmsDefNum)
                {
                    throw new InvalidOperationException("Union-Find cycle detected in FindRoot compression.");
                }
#endif
            } while (current != root);
        }
        return root;
    }

    /// <summary>
    /// 置換テーブルを更新します（Union-Find 方式）。CASスピンロックでスレッドセーフに更新します。
    /// </summary>
    public void UpdateReplaceTable(int fileNum1, int fileNum2)
    {
        while (true)
        {
            int root1 = FindRoot(fileNum1);
            int root2 = FindRoot(fileNum2);

            if (root1 == root2) return;

            long size1 = root1 >= 0 && root1 < _fileSizes.Length ? _fileSizes[root1] : 0;
            long size2 = root2 >= 0 && root2 < _fileSizes.Length ? _fileSizes[root2] : 0;

            int newRoot, newChild;
            if (size1 > size2)
            {
                newRoot = root1;
                newChild = root2;
            }
            else if (size2 > size1)
            {
                newRoot = root2;
                newChild = root1;
            }
            else
            {
                newRoot = Math.Min(root1, root2);
                newChild = Math.Max(root1, root2);
            }

            int currentParent = _replaceTable[newChild];
            if (currentParent == newRoot) return;
            if (currentParent != 0 && currentParent != newChild) continue;

            if (System.Threading.Interlocked.CompareExchange(ref _replaceTable[newChild], newRoot, currentParent) == currentParent)
            {
                break;
            }
        }
    }

    public bool IsKnownMismatch(int rootA, int rootB)
    {
        if (rootA == rootB) return false;
        if (rootA < 0 || rootA >= MaxBmsDefNum || rootB < 0 || rootB >= MaxBmsDefNum) return false;
        int min = Math.Min(rootA, rootB);
        int max = Math.Max(rootA, rootB);
        long bitIndex = ((long)min * MaxBmsDefNum) + max;
        int arrayIndex = (int)(bitIndex / 64);
        int bitOffset = (int)(bitIndex % 64);
        return (System.Threading.Interlocked.Read(ref _antiSet[arrayIndex]) & (1L << bitOffset)) != 0;
    }

    public void MarkAsMismatch(int rootA, int rootB)
    {
        if (rootA == rootB) return;
        if (rootA < 0 || rootA >= MaxBmsDefNum || rootB < 0 || rootB >= MaxBmsDefNum) return;
        int min = Math.Min(rootA, rootB);
        int max = Math.Max(rootA, rootB);
        long bitIndex = ((long)min * MaxBmsDefNum) + max;
        int arrayIndex = (int)(bitIndex / 64);
        int bitOffset = (int)(bitIndex % 64);
        System.Threading.Interlocked.Or(ref _antiSet[arrayIndex], 1L << bitOffset);
    }


    public bool IsMapped(int fileNum)
    {
        int val = _replaceTable[fileNum];
        return val != 0 && val != fileNum;
    }
}
