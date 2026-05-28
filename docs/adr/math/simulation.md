---
adr-id: M-04
target-class: SimulationEngine
status: open
---

# アニーリング手法等を用いたスコア計算の数理モデル

## 高速並列シミュレーションエンジン

- 対象クラス: SimulationEngine

**数学的証明 (Mathematical Proof)**

**【Union-Find による推移律の管理とカウント】**
波形AとBがしきい値 $\theta$ 以上で一致する場合、同一の代表要素（ルート）に統合する。

$$
\text{if } r(A,B) \ge \theta \implies \text{Union}(A, B)
$$

最終的に、すべての要素について `FindRoot(x) == x` となる $x$ の数を数えることで、$O(\alpha(n))$ の計算量で正確なユニークファイル数を導出できる。

**設計判断 (Why this algorithm?)**
複数しきい値での一括シミュレーションにおいて、全てのペア比較を直列実行すると膨大な時間がかかります。そのため、しきい値レベルおよびグループレベルでの2段階並列化（`Parallel.ForEach`）を採用し、計算資源を最大限活用しつつ、Union-Findを用いたロックフリーな結果集計を行う設計としました。

**非採用としたアプローチ (Rejected Alternatives / Anti-patterns)**

- **`lock` ブロックによる同期**: 複数スレッドからの同時アクセスが頻発するため、ロック競合がボトルネックになります。そのため、`Interlocked.CompareExchange` によるCAS（Compare-And-Swap）操作や `ConcurrentBag` を採用し、ロックフリーに動作させています。
- **最大並列度（ProcessorCount）の全割当**: UIスレッドや他のバックグラウンド処理のハングを防ぐため、あえて `ProcessorCount - 1` を最大並列度とし、システムリソースに余裕を持たせています。
