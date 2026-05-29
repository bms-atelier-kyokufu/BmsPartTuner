---
adr-id: M-05
target-class: CachedAudioSource
status: accepted
---

# 累積和(Prefix Sum)と SimHash256 による超高速スクリーニング

## 音声データの最適化キャッシュとカスケード分類

- 対象クラス: CachedAudioSource, FastWaveCompare

**数学的証明 (Mathematical Proof)**

**【累積和 (Prefix Sum)】**

$$
P[k] = \sum_{i=0}^{k-1} x_i, \quad P_{sq}[k] = \sum_{i=0}^{k-1} x_i^2
$$

任意の区間 $[i, j]$ の和や二乗和を $P[j+1] - P[i]$ のように $O(1)$ で取得できる。

**【SimHash256 と POPCNT による超高速スクリーニング】**
波形を 256bit (`ulong[4]`) のシグネチャ (SimHash) に圧縮します。波形の周波数ビンや隣接微分などの特徴から、ハッシュの各ビットを $O(N)$ で決定してロード時に保存します。

比較時には、2つの波形の SimHash シグネチャ $S_A, S_B$ のハミング距離 $H$ をハードウェア命令（`BitOperations.PopCount`）を用いて計算します。

$$
H(S_A, S_B) = \sum_{i=0}^{3} \text{PopCount}(S_A[i] \oplus S_B[i])
$$

**設計判断 (Why this algorithm?)**
「本当に似ている波形だけをフル比較（SIMD）に回す」ための強力な事前足切り（カスケード分類器）です。`ulong[4]` に対する XOR と POPCNT のみで済むため、数クロック・分岐なし（ループ展開済）で計算が完了します。ハミング距離が一定の閾値（例: 64）を超えるペアを瞬時に弾くことで、計算空間を劇的に削減します。かつての実装（単なる閾値超過フラグのLSH）から、波形の特徴をより精緻に表現できる SimHash へと進化しました。
