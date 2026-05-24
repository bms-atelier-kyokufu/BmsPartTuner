namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

/// <summary>
/// スレッドセーフな Union-Find (Disjoint-Set) アルゴリズムの実装。
/// </summary>
internal class UnionFind(int size)
{
    private readonly int[] _parent = new int[size];

    /// <summary>
    /// 代表値（ルート）を見つける（経路圧縮付き、非再帰実装）。
    /// </summary>
    public int Find(int i)
    {
        int current = i;
        var pathNodes = new List<int>();

        while (true)
        {
            int p = _parent[current];
            if (p == 0 || p == current)
                break;

            pathNodes.Add(current);
            current = p;
        }

        int root = current;
        // 経路圧縮：探索したすべてのノードの親を直接ルートに設定
        foreach (var node in pathNodes)
        {
            _parent[node] = root;
        }

        return root;
    }

    /// <summary>
    /// 2つの要素が属する集合を統合（CAS操作によるスレッドセーフなマージ）。
    /// </summary>
    public void Union(int i, int j)
    {
        int rootI = Find(i);
        int rootJ = Find(j);

        if (rootI == rootJ)
            return;

        // より小さい値を代表値（ルート）にする
        int minRoot = Math.Min(rootI, rootJ);
        int maxRoot = Math.Max(rootI, rootJ);

        // CASで統合
        System.Threading.Interlocked.CompareExchange(ref _parent[maxRoot], minRoot, 0);
        System.Threading.Interlocked.CompareExchange(ref _parent[maxRoot], minRoot, maxRoot);
    }

    /// <summary>
    /// 内部の親子関係配列を取得します（テスト・互換用）。
    /// </summary>
    public int[] GetRawTable() => _parent;
}
