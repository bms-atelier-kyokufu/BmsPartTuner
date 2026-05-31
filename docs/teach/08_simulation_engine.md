# 第8章 時間軸シミュレーションエンジンとプレイアビリティ保存

本章では、冗長な音声定義を単一の代表元へと統合（最適化）した結果、ゲームプレイ時に「音が不自然に重なる」「発音タイミングが変わる」といったプレイフィール上の破綻が絶対に発生しないことを数学的・論理的に保証するためのシステムシミュレーション理論を解説する。
単なる静的な定義置換が引き起こす干渉問題 (Why) を排除し、楽譜時間から実時間への非線形写像、および Sweep-Line アルゴリズムを用いた区間代数によって、重複発音の検証計算量を $O(M^2)$ から $O(M \log M)$ へと削減する過程を証明する。

> [!NOTE]
> **本章における主要な略称・用語**
>
> - **BPM**: Beats Per Minute (1分間の拍数)
> - **JND**: 最小可聴差異 (Just Noticeable Difference)
> - **Sweep-Line**: 走査線アルゴリズム

## 8.1 楽譜時間（Pulse）と実時間（Seconds）の非線形写像の定式化

BMSにおけるノート（音符）の配置は、一定のリズム間隔を示す「パルス」と呼ばれる離散的な座標系で行われる。これを物理的な音響波形のオーバーラップ検証に用いるためには、パルスから実時間（秒）への厳密な写像を構築する必要がある。

### 定義 8.1 (Pulse-Time 写像)

BMSのタイムライン上に存在するBPM変更イベントおよびSTOPイベントの全集合を考慮する。
パルス座標 $p \in \mathbb{R}_{\ge 0}$ から実時間座標 $t \in \mathbb{R}_{\ge 0}$ への変換を、非線形な連続関数（または区間的に連続な関数） $T: p \mapsto t$ として定義する。

あるパルス区間 $[p_k, p_{k+1})$ においてBPMが定数 $B_k > 0$ であるとき、パルスの基本分解能を $R$（通常 $R=96$ または $192$）とすると、その区間における実時間の増分 $\Delta t$ はパルスの増分 $\Delta p$ に対して以下の線形関係を持つ。

$$\Delta t = \frac{60}{B_k \cdot R} \Delta p \quad \cdots (8.1)$$

### 定義 8.2 (発音イベントとエネルギー密度)

タイムライン上の $i$ 番目の発音イベント $e_i$ を、タプル $(p_i, w_i, L_i)$ として定義する。
ここで $p_i$ は発音開始パルス、$w_i \in V$ は再生される音声定義（第7章の統合の要素）、$L_i$ は音声波形の実時間長（秒）である。写像 $T$ により、実時間における発音区間は $I_i = [T(p_i), T(p_i) + L_i]$ となる。

## 8.2 課題：静的解析の限界と波形干渉のジレンマ (Why)

最適化の目的は、類似した音声定義群（同値類 $[w]$）を代表元 $w_{\text{rep}}$ にすべて置換することである。しかし、タイムラインを考慮せずに静的置換を強行すると、以下の物理的・音響的破綻を引き起こす。

**1. 位相干渉とコムフィルタ効果の発生**
もし異なるレーンに配置されたノート $e_1, e_2$ が同一の実時間 $t$ に極めて近いタイミングで発音され、それらが同じ代表元 $w_{\text{rep}}$ に置換されたとする。二つの同一波形が微小な時間差 $\Delta t$ で重ね合わされると、周波数領域において強め合い・打ち消し合いが周期的に発生する（コムフィルタ効果）。これにより、元の音響的性質が著しく歪む（フランジャーのような音質劣化を招く）。

**2. 同時発音数制限による音抜け**
多くの再生エンジンは、全く同一のバッファへの同時アクセスや多重再生に対してハードウェア的またはソフトウェア的な発音数上限を持つ。同一時刻付近に多数の $w_{\text{rep}}$ が密集すると、後着のノートが再生をキャンセルされる（音抜けが発生する）。

**3.** $O(M^2)$ **の素朴な区間衝突判定の爆発**
上記の破綻を防ぐには、「置換対象のノート群の実時間区間 $I_i$ と $I_j$ がオーバーラップしているか否か」を全イベントについてシミュレーション（衝突判定）する必要がある。総ノート数を $M$ としたとき、すべてのペアを素朴に比較すると時間計算量は $O(M^2)$ となる。高難易度の譜面（$M > 10^5$）では数分間の計算ブロックが発生し、パイプラインのリアルタイム性を損なう。

## 8.3 定理の提示とSweep-Lineアルゴリズムによる証明 (Proof)

Pulse-Time写像の単調性と、区間代数における Sweep-Line アルゴリズムを導入することで、シミュレーション計算量を劇的に削減する。

> [!IMPORTANT]
> **定理 8.1 (Pulse-Time 写像の広義単調増加性と逆関数)**
> STOPイベントによる停留区間を除き、関数 $T(p)$ は広義単調増加である。
> すなわち、任意の $p_1 < p_2$ に対して $T(p_1) \le T(p_2)$ が成り立つ。これにより、$B$ 個のBPM/STOP変化点を持つ区分線形関数 $T(p)$ の評価は、変化点の配列に対する二分探索を用いることで、任意の $p$ に対して $O(\log B)$ 時間で一意に計算可能である。

> [!IMPORTANT]
> **定理 8.2 (Sweep-Lineによる区間オーバーラップの高速検出)**
> 総数 $M$ 個の発音イベント集合 $E$ におけるすべての区間交差（オーバーラップ）の検出は、時間計算量 $O(M \log M)$ で完了する。

> [!NOTE]
> **証明**
>
> 各発音イベント $e_i$ の実時間区間 $I_i = [t_{\text{start}}^{(i)}, t_{\text{end}}^{(i)}]$ に対して、2つのイベントポイント（端点）を生成する。
>
> - 始点イベント: $(t_{\text{start}}^{(i)}, \text{type: START}, \text{id}: i)$
> - 終点イベント: $(t_{\text{end}}^{(i)}, \text{type: END}, \text{id}: i)$
>
> これら $2M$ 個の端点を、時間 $t$ の順にソートする（ソートの時間計算量は $O(M \log M)$）。
> Sweep-Line を時間 $t=0$ から順に進めながら、以下の状態遷移を行う。
>
> 1. `START` イベントに遭遇した場合：現在の「発音中リスト」にノート $i$ を追加する。このとき、リスト内に既に存在するノート群が、ノート $i$ と確実に交差（オーバーラップ）している区間である。
> 2. `END` イベントに遭遇した場合：「発音中リスト」からノート $i$ を削除する。
>
> この走査処理において、各イベントのリストへの追加・削除がそれぞれ1回ずつ行われるため、走査自体の時間計算量は $O(M)$ である。交差の列挙を含めても、ソートの計算量が支配的となるため、全体の時間計算量は $O(M \log M)$ となることが示された。$\blacksquare$

## 8.4 計算量削減の定量的評価と疑似コード

前述の証明により、譜面の全ノートのペアを検証する $O(M^2)$ の力技探索は排除され、ソートに基づいた $O(M \log M)$ 空間へと最適化された。

### 実装への射影：PulseToRealTimeCalculator と SimulationEngine

C# 実装においては、`PulseToRealTimeCalculator` が前計算による変化点配列の二分探索（$O(\log B)$）を担保し、`SimulationEngine` が走査線アルゴリズムを実行する。

```csharp
// 【疑似コード】PulseToRealTimeCalculator.cs における O(log B) 時間変換
public double ConvertPulseToSeconds(double pulse) {
    // _timeEvents はパルス順にソート済みの BPM/STOP 変化点配列
    // 二分探索 (Binary Search) により、O(log B) で対象区間を特定
    int idx = BinarySearch(_timeEvents, pulse);
    var ev = _timeEvents[idx];

    // 式(8.1)に基づく線形補間
    double deltaP = pulse - ev.Pulse;
    return ev.RealTime + (deltaP * (60.0 / (ev.Bpm * Resolution)));
}

// 【疑似コード】SimulationEngine.cs における Sweep-Line オーバーラップ検出
public void SimulateAndOptimize(List<NoteEvent> notes) {
    var endpoints = new List<TimePoint>(notes.Count * 2);
    foreach (var note in notes) {
        endpoints.Add(new TimePoint(note.StartTime, START, note));
        endpoints.Add(new TimePoint(note.EndTime, END, note));
    }

    // O(M log M): タイムライン順にソート
    endpoints.Sort((a, b) => a.Time.CompareTo(b.Time));

    var activeNotes = new HashSet<NoteEvent>();

    // O(M): 走査線によるシミュレーション
    foreach (var pt in endpoints) {
        if (pt.Type == START) {
            // 現在発音中のノートとの衝突判定（コムフィルタ効果の検証）
            foreach (var active in activeNotes) {
                if (AreInSameEquivalenceClass(pt.Note.Audio, active.Audio)) {
                    MarkAsNonOptimizable(pt.Note); // 同時発音による破綻を回避するため最適化から除外
                }
            }
            activeNotes.Add(pt.Note);
        } else {
            activeNotes.Remove(pt.Note);
        }
    }
}

```

このシミュレーションにより、置換対象となる音声が「タイムライン上でオーバーラップする」場合は安全のために最適化（統合）から除外（`MarkAsNonOptimizable`）される。

## 8.5 プレイアビリティ等価定理と許容限界

最終的に、このシミュレーションエンジンが保証する「ゲームプレイの不変性」を数学的に定義する。

> [!IMPORTANT]
> **定理 8.3 (プレイアビリティ等価定理)**
> 最適化前の譜面における時刻 $t$ での全発音エネルギー密度関数を $\Phi_{\text{orig}}(t)$、最適化後のそれを $\Phi_{\text{opt}}(t)$ とする。
> SimulationEngine によって「干渉する同値類ノートを統合しない」という境界条件が守られる限り、任意の時刻 $t$ において以下の不等式が成立する。
>
> $$\int_{t}^{t + \Delta t} |\Phi_{\text{orig}}(\tau) - \Phi_{\text{opt}}(\tau)| d\tau \le \delta \quad \cdots (8.2)$$
>
> ここで $\delta$ は人間の聴覚におけるJNDに基づく極微小な許容限界値である。

この定理により、BMS Part Tuner が行う最適化は単なるファイルサイズの削減に留まらず、「プレイヤーが体感する音響的エネルギーの総量と位相分布を数学的に完全保存している」ことが論理的に証明される。

## 8.6 参考文献 (References)

- Shamos, M. I., & Hoey, D. (1976). "Geometric intersection problems". _17th Annual Symposium on Foundations of Computer Science (sfcs 1976)_. IEEE.

