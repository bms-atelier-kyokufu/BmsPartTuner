---
adr-id: OPT-05
target-class: PreNormalizedSoundData
status: accepted
---

# Anemic Domain ModelとFlyweightパターンによるメモリ最適化

## キャッシュ音声データモデル

- 対象クラス: PreNormalizedSoundData, PointerSoundData

**設計判断 (Why this algorithm?)**

- **メモリ最適化と純粋なデータモデル (`PreNormalizedSoundData`)**:
  BMS最適化処理では数百MBの音声データをオンメモリで保持するため、モデル内にI/Oを持たせず、処理されたデータ（無音トリミング後の波形など）のみを保持する純粋なデータモデル（Anemic Domain Model寄りの設計）を採用しました。オブジェクトの生成は全て `AudioProcessingService` に委譲し、SRPを徹底しています。
- **Flyweightパターンによるメモリ最適化 (`PointerSoundData`)**:
  bmsonファイルでは、数千個のスライス音声が定義されますが、これらは全て1つの巨大なベースWAVの異なる範囲を指しているだけです。各スライスが自身の `float[]` 配列をヒープに抱えると、同じ波形データを重複してメモリに乗せることになり、`OutOfMemoryException` を誘発します。そのため、ポインタ（ベースデータの参照、開始位置、長さ）のみを保持し、実際の波形はベースデータの配列から `ReadOnlySpan<float>` として切り出して返す構造を採用しました。
- **LSP違反の解消とインターフェース分離 (ISP)**:
  当初は `ICachedSoundData` に計算系のメソッド（`GetChannelSum` など）を持たせていましたが、Flyweightパターンの `PreNormalizedSoundData` 等でサポートできず `NotSupportedException` を投げる実装となり、リスコフの置換原則 (LSP) に違反していました。これを解消するため、計算責務を `IAudioStatisticalData` インターフェースに分離し、実装クラスが安全に振る舞える設計に改善しました。
