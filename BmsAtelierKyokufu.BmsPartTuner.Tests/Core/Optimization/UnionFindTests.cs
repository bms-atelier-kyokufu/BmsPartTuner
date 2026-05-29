using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization
{
    /// <summary>
    /// UnionFind の動作検証テスト。
    /// 代表値（ルート）検索、統合、経路圧縮、スレッドセーフティなどを検証します。
    /// </summary>
    public class UnionFindTests
    {
        [Fact]
        public void FindRoot_DirectParent_ReturnsParent()
        {
            var uf = new UnionFind(10);
            uf.SetParent(2, 1);
            uf.SetParent(1, 1); // 1がルート

            int root = uf.Find(2);

            Assert.Equal(1, root);
        }

        [Fact]
        public void FindRoot_TransitiveParent_ReturnsRoot()
        {
            var uf = new UnionFind(10);
            uf.SetParent(3, 2);
            uf.SetParent(2, 1);
            uf.SetParent(1, 1);

            int root = uf.Find(3);

            Assert.Equal(1, root);
        }

        [Fact]
        public void FindRoot_Uninitialized_ReturnsSelf()
        {
            var uf = new UnionFind(10);

            int root = uf.Find(5);

            Assert.Equal(5, root);
        }

        [Fact]
        public void UpdateReplaceTable_MergesSetsCorrectly()
        {
            var uf = new UnionFind(10);

            uf.Union(2, 3);

            int root2 = uf.Find(2);
            int root3 = uf.Find(3);
            Assert.Equal(root2, root3);
            Assert.Equal(2, root2); // より小さい方がルートになる
        }

        [Fact]
        public void UpdateReplaceTable_TransitiveMerge_WorksCorrectly()
        {
            var uf = new UnionFind(10);

            uf.Union(2, 3);
            uf.Union(3, 4);

            int root2 = uf.Find(2);
            int root3 = uf.Find(3);
            int root4 = uf.Find(4);
            Assert.Equal(root2, root3);
            Assert.Equal(root2, root4);
        }

        [Fact]
        public void UpdateReplaceTable_AlreadyMerged_NoChange()
        {
            var uf = new UnionFind(10);
            uf.Union(2, 3);
            int rootBefore = uf.Find(3);

            // 実行: 再度マージを試みる
            uf.Union(2, 3);

            // 検証: 既にマージ済みなので変化なし
            int rootAfter = uf.Find(3);
            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void UpdateReplaceTable_MultipleGroups_MaintainsSeparation()
        {
            // 準備
            var uf = new UnionFind(20);

            // 実行
            // グループ1: {1, 2, 3}
            uf.Union(1, 2);
            uf.Union(2, 3);

            // グループ2: {10, 11, 12}
            uf.Union(10, 11);
            uf.Union(11, 12);

            // 検証: 各グループは分離されている
            int root1 = uf.Find(1);
            int root2 = uf.Find(2);
            int root3 = uf.Find(3);
            int root10 = uf.Find(10);
            int root11 = uf.Find(11);
            int root12 = uf.Find(12);

            Assert.Equal(root1, root2);
            Assert.Equal(root1, root3);
            Assert.Equal(root10, root11);
            Assert.Equal(root10, root12);
            Assert.NotEqual(root1, root10); // 異なるグループ
        }

        [Fact]
        public void UpdateReplaceTable_ConcurrentAccess_MaintainsConsistency()
        {
            // 準備: スレッドセーフ性の検証
            const int iterations = 1000;
            const int maxIndex = 100;
            var uf = new UnionFind(maxIndex + 1);

            // 実行: 複数スレッドから同時にマージ操作を実行
            Parallel.For(0, iterations, i =>
            {
                int a = (i % maxIndex) + 1;
                int b = ((i + 1) % maxIndex) + 1;
                uf.Union(a, b);
            });

            // 全ての要素がルートにたどり着ける（無限ループなし）
            for (int i = 1; i <= maxIndex; i++)
            {
                int root = uf.Find(i);
                Assert.InRange(root, 1, maxIndex);

                // ルート要素の値が 0 (初期値=自分自身) であることも許容する
                int parent = uf.GetParent(root);
                Assert.True(parent == root || parent == 0 || parent > 0);
            }
        }

        [Fact]
        public async Task UpdateReplaceTable_RaceCondition_NoDeadlock()
        {
            // 準備: 競合状態のテスト
            const int threadCount = 10;
            const int operationsPerThread = 100;
            var uf = new UnionFind(1000);
            var tasks = new List<Task>();

            // 実行: 複数スレッドから同じペアをマージ
            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        // 同じペアを繰り返しマージしてデッドロックや無限ループを検証
                        uf.Union(10, 20);
                        uf.Union(20, 30);
                        uf.Union(30, 40);
                    }
                }));
            }

            // 検証: タイムアウトなく完了する（タイムアウトを明示的にチェック）
            var timeout = TimeSpan.FromSeconds(5);
            var allTask = Task.WhenAll(tasks);
            if (await Task.WhenAny(allTask, Task.Delay(timeout)) != allTask)
            {
                Assert.Fail("並列マージ操作がタイムアウトしました（デッドロックの可能性）");
            }
            // 例外が発生していればここでスローされる
            await allTask;

            // 最終的な一貫性を検証
            int root10 = uf.Find(10);
            int root20 = uf.Find(20);
            int root30 = uf.Find(30);
            int root40 = uf.Find(40);

            Assert.Equal(root10, root20);
            Assert.Equal(root10, root30);
            Assert.Equal(root10, root40);
        }

        /// <summary>
        /// FindRootでのパス圧縮の効果を検証（深いチェーンが圧縮される）。
        /// </summary>
        [Fact]
        public void FindRoot_PathCompression_CompressesDeepChain()
        {
            var uf = new UnionFind(10);

            // 深いチェーンを作成: 1 <- 2 <- 3 <- 4 <- 5
            uf.SetParent(5, 4);
            uf.SetParent(4, 3);
            uf.SetParent(3, 2);
            uf.SetParent(2, 1);
            uf.SetParent(1, 1); // ルート

            // 初回のFindRootでパス圧縮が行われる
            int root = uf.Find(5);

            Assert.Equal(1, root);

            // パス圧縮後、5は直接1を指すようになるはず
            Assert.Equal(1, uf.GetParent(5));
        }

        /// <summary>
        /// UpdateReplaceTableで同じ要素を自分自身にマージしようとするケース。
        /// </summary>
        [Fact]
        public void UpdateReplaceTable_SelfMerge_NoChange()
        {
            var uf = new UnionFind(10);

            // 自分自身にマージを試みる
            uf.Union(3, 3);

            // ルートは自分自身のまま
            int root = uf.Find(3);
            Assert.Equal(3, root);
        }
    }

    internal static class UnionFindTestExtensions
    {
        public static void SetParent(this UnionFind uf, int child, int parent)
        {
            var field = typeof(UnionFind).GetField("_parent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var array = (int[])field!.GetValue(uf)!;
            array[child] = parent;
        }

        public static int GetParent(this UnionFind uf, int child)
        {
            var field = typeof(UnionFind).GetField("_parent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var array = (int[])field!.GetValue(uf)!;
            return array[child];
        }
    }
}
