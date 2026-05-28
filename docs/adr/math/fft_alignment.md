---
adr-id: M-03
target-class: FftAlignmentEngine
status: open
---

# FFT（高速フーリエ変換）を用いた位相アライメントの数理モデル

## サブミリ秒アライメントのズレ量推定エンジン
* 対象クラス: FftAlignmentEngine



**数学的証明 (Mathematical Proof)**
畳み込み定理（Convolution Theorem）により、2つの信号 $x(t)$ と $y(t)$ の相互相関関数 $R_{xy}(\tau)$ は、各々のフーリエ変換 $X(f)$ と $Y(f)$ のクロススペクトルの逆フーリエ変換に等しい。

$$
R_{xy}(\tau) = \mathcal{F}^{-1} \{ X(f) \cdot Y^*(f) \}
$$
（※ $Y^*(f)$ は $Y(f)$ の複素共役）

この $R_{xy}(\tau)$ が最大となるインデックス $\tau$ （時間ズレ）を探索する。
実装上では、探索範囲を `FftLength / 2` に制限することで、循環畳み込み（Circular Convolution）によるエイリアシングの影響を排除している。


**設計判断 (Why this algorithm?)**
サブミリ秒精度の波形アライメント（位相ズレ推定）を高速に行うため、時間領域での単純なスライディング相互相関計算ではなく、FFTを用いた周波数領域での相互相関計算（畳み込み定理）を採用しています。これにより、計算量を $O(N^2)$ から $O(N \log N)$ へと劇的に削減しています。

**非採用としたアプローチ (Rejected Alternatives / Anti-patterns)**
- **時間領域での相互相関**: サンプルごとにループを回す計算は計算量が多すぎ、リアルタイム処理が困難だったため廃止しました。
- **毎回のアロケーション**: `Complex32[]` やハニング窓の配列を呼び出しごとに `new` すると、GCの負担が極めて大きくなります。そのため、`ThreadLocal<T>` や `ConcurrentDictionary` による事前割り当て・キャッシュ再利用の仕組みを採用しました。



