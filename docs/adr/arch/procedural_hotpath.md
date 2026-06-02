---
adr-id: ARCH-03
title: 超ホットパスにおける手続き的構造とAggressiveInliningの採用
date: 2026-06-03
status: accepted
---

# ARCH-03: 超ホットパスにおける手続き的構造とAggressiveInliningの採用

## コンテキスト
`FastWaveCompare.IsMatch` は最適化フェーズのインナーループで極端に高い頻度で呼び出される超ホットパスです。
ARCH-01 で導入された Pipeline パターンは関心の分離や拡張性に優れますが、インターフェースを介した仮想関数呼び出し（Virtual Dispatch）やコンテキストオブジェクトの生成（アロケーション）に伴うGCオーバーヘッドが発生し、本パスでは許容できない遅延の原因となります。

## 決定事項
`FastWaveCompare.IsMatch` には Pipeline パターンを適用せず、手続き的な早期リターン構造を維持します。
各判定ステップは `[MethodImpl(MethodImplOptions.AggressiveInlining)]` を付与したプライベートメソッドに分割し、コンパイル時に単一メソッドへとインライン展開させます。

## 影響範囲・結果
- **メリット**:
  - インスタンス生成および仮想関数呼び出しのオーバーヘッドがゼロとなり、処理速度が最大化されます。
  - プライベートメソッド分割により、実質的にパイプラインに準じたコードの見通しが確保されます。
- **デメリット**:
  - 動的なステップの追加や順序変更はコンパイル時にコードを直接修正する必要があります。

## ADRアンカー
- `FastWaveCompare.IsMatch`
