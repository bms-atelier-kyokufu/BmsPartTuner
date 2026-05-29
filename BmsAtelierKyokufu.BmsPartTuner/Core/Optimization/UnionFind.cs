namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

/// <summary>
/// スレッドセーフな Union-Find (Disjoint-Set) アルゴリズムの実装。
/// </summary>
internal class UnionFind(int size)
{
    private readonly int[] _parent = new int[size];

    /// <summary>
    /// 代表値（ルート）を見つける（経路圧縮付き、アロケーションフリーの2パス実装）。
    /// </summary>
    public int Find(int i)
    {
        int root = i;
        while (true)
        {
            int p = _parent[root];
            if (p == 0 || p == root)
                break;
            root = p;
        }

        // 経路圧縮：2パス目で親を直接ルートに設定（メモリ確保なし）
        int current = i;
        while (current != root)
        {
            int p = _parent[current];
            if (p == 0 || p == current)
                break;
            
            _parent[current] = root;
            current = p;
        }

        return root;
    }

    /// <summary>
    /// 2つの要素が属する集合を統合（CASスピンロックによるスレッドセーフなマージ）。
    /// </summary>
    public void Union(int i, int j)
    {
        while (true)
        {
            int rootI = Find(i);
            int rootJ = Find(j);

            if (rootI == rootJ)
                return;

            // より小さい値を代表値（ルート）にする
            int minRoot = Math.Min(rootI, rootJ);
            int maxRoot = Math.Max(rootI, rootJ);

            int currentParent = _parent[maxRoot];
            if (currentParent == minRoot)
                return; // すでに別スレッドが統合済み

            // 自分がルート（0 または 自分自身）以外を指している場合は、親が変わっているので再試行
            if (currentParent != 0 && currentParent != maxRoot)
                continue;

            // CASで統合。成功したらループを抜ける。
            if (System.Threading.Interlocked.CompareExchange(ref _parent[maxRoot], minRoot, currentParent) == currentParent)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 自分自身を代表値としてマークします。すでにマークされている場合は false を返します。
    /// </summary>
    public bool TryMarkSelf(int i)
    {
        return System.Threading.Interlocked.CompareExchange(ref _parent[i], i, 0) == 0;
    }

    /// <summary>
    /// j を i の子として直接リンクします（j が未マークの場合のみ）。
    /// </summary>
    public bool TryLink(int child, int newParent)
    {
        return System.Threading.Interlocked.CompareExchange(ref _parent[child], newParent, 0) == 0;
    }
    
    /// <summary>
    /// 指定された要素がマークされているか（親が設定されているか）を確認します。
    /// </summary>
    public bool IsMapped(int i)
    {
        return _parent[i] != 0;
    }

}
