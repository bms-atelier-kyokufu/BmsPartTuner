# 第5章 周波数領域における時間アライメント

本章では、2つの音響信号間に存在する微小な時間的ズレを、サブサンプル精度かつ高速に検出・補正するための数理を解説する。
時間領域における素朴な全探索が抱える計算量爆発の課題を明確にし、信号を周波数領域へ射影する「畳み込み定理」と「FFT」を用い、計算量を $O(N \log N)$ へと劇的に削減する過程を数学的に証明する。

> [!NOTE]
> **本章における主要な略称・用語**
>
> - **FFT**: 高速フーリエ変換 (Fast Fourier Transform)
> - **DFT**: 離散フーリエ変換 (Discrete Fourier Transform)
> - **IDFT**: 逆離散フーリエ変換 (Inverse Discrete Fourier Transform)

## 5.1 時間アライメント問題の数理的定式化

2つの同一音源に由来する信号であっても、エンコード時のパディングや切り出し位置の違いにより、時間的なズレ（ラグ）が生じることがある。このズレを特定する問題を定式化する。

### 定義 5.1 (離散相互相関関数)

長さ $N$ の実数値離散時間信号 $\mathbf{x}, \mathbf{y} \in \mathbb{R}^N$ において、信号 $\mathbf{y}$ を時間 $\tau \in \mathbb{Z}$ だけシフトさせたときの相互相関関数 $R_{xy}(\tau)$ を次のように定義する。

$$R_{xy}(\tau) = \sum_{n=0}^{N-1} x[n] y[n + \tau] \quad \cdots (5.1)$$

ただし、範囲外のインデックスに対してはゼロパディング（$x[n]=0, y[n]=0$）されているものとする。
時間アライメント（ズレの補正）の目的は、この相互相関関数が最大となる最適なシフト量 $\tau_{\text{opt}}$ を見つける最適化問題である。

$$\tau_{\text{opt}} = \arg\max_{\tau} R_{xy}(\tau) \quad \cdots (5.2)$$

## 5.2 課題：時間領域探索における計算量爆発のジレンマ (Why)

前章のピアソン相関係数探索（式4.6）において、局所的な微小ズレ $\tau \in [-\tau_{\text{max}}, \tau_{\text{max}}]$ をSIMD命令で探索する手法を解説した。しかし、これには致命的な限界が存在する。

**時間領域探索の破綻：**
もし、2つの信号間に数秒単位（数万サンプル）の巨大なズレが存在する場合や、ズレの範囲が全く未知である場合、探索範囲は $\tau \in [-N, N]$ となる。
このとき、式(5.1)をすべての $\tau$ について計算する時間領域アルゴリズムは、二重ループ構造となり、**時間計算量** $O(N^2)$ を要求する。
例えば $N = 44100$（1秒の音声）の場合、約20億回の積和演算が必要となり、数万のファイル群を比較する本システムにおいては、処理時間が年単位に膨れ上がり完全に破綻する。

この計算量 $O(N^2)$ の壁を突破し、どのようなズレであっても実用時間内にアライメントを完了させるためには、演算の空間（ドメイン）を時間から周波数へと写像する数学的変形が不可避となる。

## 5.3 定理の提示と畳み込み定理による証明 (Proof)

計算量を削減するため、信号を複素数平面上の周波数領域へと射影する。

### 定義 5.2 (離散フーリエ変換: DFT)

信号 $\mathbf{x}$ のDFT $X[k] \in \mathbb{C}$ を次のように定義する。

$$X[k] = \mathcal{F}\{x\}[k] = \sum_{n=0}^{N-1} x[n] e^{-j \frac{2\pi}{N} k n} \quad \cdots (5.3)$$

ここで $j$ は虚数単位（$j^2 = -1$）である。同様に $\mathbf{y}$ のDFTを $Y[k]$ とする。

> [!IMPORTANT]
> **定理 5.1 (相互相関の周波数領域定理 / ウィーナー・ヒンチン定理の離散版)**
> 時間領域における相互相関関数 $R_{xy}(\tau)$ のフーリエ変換は、周波数領域における $X[k]$ の複素共役 $X^*[k]$ と $Y[k]$ の要素ごとの積（Hadamard積）に等しい。
>
> $$\mathcal{F}\{R_{xy}\}[k] = X^*[k] \cdot Y[k] \quad \cdots (5.4)$$

> [!NOTE]
> **証明**
>
> 相互相関関数 $R_{xy}(\tau)$ のフーリエ変換を展開する。
>
> <!-- prettier-ignore -->
> $$ \begin{aligned} \mathcal{F}\{R_{xy}\}[k] &= \sum_{\tau=0}^{N-1} R_{xy}(\tau) e^{-j \frac{2\pi}{N} k \tau} \\ &= \sum_{\tau=0}^{N-1} \left( \sum_{n=0}^{N-1} x[n] y[n + \tau] \right) e^{-j \frac{2\pi}{N} k \tau} \end{aligned} $$
>
> 和の順序を交換（フビニの定理の離散適用）し、$m = n + \tau$ と変数変換を行う。このとき $\tau = m - n$ である。
>
> <!-- prettier-ignore -->
> $$ \begin{aligned} &= \sum_{n=0}^{N-1} x[n] \sum_{m=0}^{N-1} y[m] e^{-j \frac{2\pi}{N} k (m - n)} \\ &= \left( \sum_{n=0}^{N-1} x[n] e^{j \frac{2\pi}{N} k n} \right) \left( \sum_{m=0}^{N-1} y[m] e^{-j \frac{2\pi}{N} k m} \right) \end{aligned} $$
>
> 第一項は $x[n]$ のDFTの虚数部の符号が反転しているため、$X[k]$ の複素共役 $X^*[k]$ となる。第二項は $Y[k]$ そのものである。
> したがって、
>
> <!-- prettier-ignore -->
> $$ \mathcal{F}\{R_{xy}\}[k] = X^{*}[k] Y[k] $$
>
> が示された。$\blacksquare$

### 逆変換による最適シフト $\tau_{\text{opt}}$ の導出

定理5.1より、求める相互相関関数 $R_{xy}(\tau)$ は、周波数領域の積を逆離散フーリエ変換（IDFT）することで一挙に得られる。

$$R_{xy}(\tau) = \mathcal{F}^{-1} \{ X^*[k] Y[k] \}(\tau) \quad \cdots (5.5)$$

## 5.4 計算量削減の定量的評価と疑似コード

### 5.4.1 計算量の劇的な削減（$O(N^2) \to O(N \log N)$）

式(5.5)の計算過程において、クーリー・テューキー型アルゴリズム等のFFTを適用した場合の計算量を評価する。

1. $X[k] = \text{FFT}(x)$ : $O(N \log N)$
2. $Y[k] = \text{FFT}(y)$ : $O(N \log N)$
3. 複素共役積 $X^*[k] Y[k]$ : $O(N)$
4. $R_{xy}(\tau) = \text{IFFT}(X^* Y)$ : $O(N \log N)$

結果として、全体の時間計算量は
$$O(N \log N)$$
となる。
$N = 65536 \ (= 2^{16})$ の場合、
$$N^2 \approx 4.2 \times 10^9$$ に対して、
$$N \log_2 N \approx 1.0 \times 10^6$$
となり、**数千倍の物理的演算削減**が達成される。これが、時間領域での力技探索を棄て、FFTアライメントを採用する決定的な数学的理由である。

### 5.4.2 実装への射影：疑似コード

`FftAlignmentEngine.cs` におけるアライメント算出ロジックは、この定理を直接具現化している。

```csharp
// 【疑似コード】FFTを用いた O(N log N) 時間アライメント
int N = MathHelper.NextPowerOfTwo(signalX.Length + signalY.Length - 1); // 巡回畳み込み回避のゼロパディング

// 1. O(N log N): 両信号を周波数領域へ写像
Complex[] freqX = FFT.Forward(PadWithZeros(signalX, N));
Complex[] freqY = FFT.Forward(PadWithZeros(signalY, N));

// 2. O(N): 周波数領域での複素共役積 (相互相関スペクトル)
Complex[] crossSpectrum = new Complex[N];
for (int k = 0; k < N; k++) {
    // freqX[k]の複素共役(Conjugate)と freqY[k]の積
    crossSpectrum[k] = Complex.Conjugate(freqX[k]) * freqY[k];
}

// 3. O(N log N): 逆フーリエ変換で時間領域の相互相関関数へ戻す
double[] crossCorrelation = FFT.Inverse(crossSpectrum);

// 4. O(N): 最大相関を与えるインデックス(遅延 tau)を特定
int optimalTau = FindPeakIndex(crossCorrelation);
```

## 5.5 誤差限界と境界条件（巡回畳み込みの回避）

### ゼロパディングの数学的必要性

FFTを用いた相互相関計算（式5.5）は、本来「巡回相互相関」を計算するものである。すなわち、信号が周期 $N$ で無限に繰り返していると暗黙に仮定される。

もし長さ $L$ の信号をそのままサイズ $L$ のFFTにかけると、シフトして右端からはみ出した信号が左端から回り込んでくる「エイリアシング」現象が発生し、正しいズレ $\tau$ が検出できなくなる。

これを防ぐための境界条件として、FFTのサイズ $N$ は必ず以下の不等式を満たすように選択されなければならない。

$$N \ge \text{Length}(x) + \text{Length}(y) - 1 \quad \cdots (5.6)$$

`FftAlignmentEngine.cs` において、入力を必ずこのサイズ $N$（通常は更に高速化のため $2$ の冪乗）にまで $0$ で埋める「ゼロパディング」という前処理が行われているのは、この数学的境界条件を保証し、計算誤差を論理的にゼロにするためである。

## 5.6 参考文献 (References)

- Bracewell, R. N. (1986). _The Fourier Transform and Its Applications_. McGraw-Hill.
- Cooley, J. W., & Tukey, J. W. (1965). "An algorithm for the machine calculation of complex Fourier series". _Mathematics of Computation_, 19(90), 297-301.

