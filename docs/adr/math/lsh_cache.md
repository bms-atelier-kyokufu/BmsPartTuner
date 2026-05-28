---
adr-id: M-05
target-class: CachedAudioSource
status: open
---

# 累積和(Prefix Sum)とLocality Sensitive Hashing (LSH)の数理モデル

## 音声データの最適化キャッシュ
* 対象クラス: CachedAudioSource



**数学的証明 (Mathematical Proof)**

**【累積和 (Prefix Sum)】**
$$
P[k] = \sum_{i=0}^{k-1} x_i, \quad P_{sq}[k] = \sum_{i=0}^{k-1} x_i^2
$$
任意の区間 $[i, j]$ の和や二乗和を $P[j+1] - P[i]$ のように $O(1)$ で取得できる。

**【Locality Sensitive Hashing (LSH)】**
64サンプルごとに1つの `ulong` (64-bit) 変数にビットフラグを格納する。
- `signLsh`: $x_i \ge 0$ の場合にビットを立てる。
- `signLshMask`: $|x_i| \ge \theta$ ($\theta$ は -45dB の閾値) の場合にビットを立てる。

$$
B_k = \bigvee_{j=0}^{63} \left( (x_{64k+j} \ge 0) \ll j \right)
$$
これにより、位相の極性や無音区間をビット演算（AND/XOR）で極めて高速に判定できる。


**設計判断 (Why this algorithm?)**
SIMDによる波形比較を高速化するため、ロード時に音声データの最適化表現（LSHや累積和など）を事前計算します。特に、Locality Sensitive Hashing (LSH) を用いて、波形の符号や閾値超過（無音判定）をビット列として表現することで、計算時のコストを大幅に削減しています。



