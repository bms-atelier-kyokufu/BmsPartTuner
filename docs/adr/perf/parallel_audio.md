---
adr-id: OPT-01
target-class: ParallelAudioComparisonEngine
status: open
---

# $O(N^2)$回避のための並列比較アルゴリズムと枝刈り

## 並列オーディオ比較エンジン
* 対象クラス: ParallelAudioComparisonEngine



**数学的証明 (Mathematical Proof)**

**【Sort & Sweep による探索空間削減】**
比較対象を事前にRMS（二乗平均平方根）で昇順ソートする。音響的に類似した波形はRMSも近くなる性質を利用し、$R_i \approx R_j$ となる近傍のファイルペアのみを詳細比較する。これにより計算量を $O(N \log N) + O(N \times K)$（$K \ll N$）に削減。

**【三角不等式による枝刈り (Triangle Inequality Pruning)】**
ピアソン相関係数 $r \in [-1, 1]$ は、距離計量 $d = \sqrt{2 \max(0, 1 - r)}$ に変換可能。この距離空間において三角不等式 $d(x, y) \le d(x, p) + d(p, y)$ が成立する。
グループ内から選んだピボット $p$ に対して各要素の距離 $d(x, p)$ を事前計算しておくことで、$|d(x, p) - d(y, p)| > d_{\text{threshold}}$ の場合、直ちに $x$ と $y$ は非類似として比較をスキップ（枝刈り）できる。

**【Union-Find による推移関係の統合】**
波形AとBが一致し、BとCが一致した場合、AとCも一致するとみなす（推移律）。
この関係を素集合データ構造（Union-Find）を用いて管理し、経路圧縮（Path Compression）により検索を $O(\alpha(n))$ に最適化している。


**設計判断 (Why this algorithm?)**
N個の音声ファイル群に対して総当り比較を行うと $O(N^2)$ の計算量となり、大量のファイルでパフォーマンスが破綻します。これを回避するため、ソートによる探索空間の削減（Sort & Sweep）、ピボットを用いた三角不等式による枝刈り（Triangle Inequality Pruning）、および推移的な一致関係の統合（Union-Find）を組み合わせた高度な並列比較エンジンを採用しています。

**非採用としたアプローチ (Rejected Alternatives / Anti-patterns)**
- **単純な二重ループ（$O(N^2)$）**: 計算量が爆発するため廃止しました。
- **ロック（`lock` ステートメント）による同期**: 並列処理時のコンテキストスイッチがボトルネックとなるため廃止しました。代わりに `Interlocked.CompareExchange` を用いたCAS（Compare-And-Swap）によるロックフリー（Lock-Free）アルゴリズムを採用し、複数のスレッドから同時にUnion-Findツリーを更新できるようにしています。



