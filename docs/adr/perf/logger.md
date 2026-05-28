---
adr-id: OPT-03
target-class: PerformanceDebugLogger
status: open
---

# 非同期バッチロギングと構造化ログのアーキテクチャ

## パフォーマンス計測とデバッグ用の非同期構造化ロガー

- 対象クラス: PerformanceDebugLogger

**設計判断 (Why this algorithm?)**

- **非同期バッチログ書き込み (Channel)**:
  大量のログ出力（特にパフォーマンス計測）がI/Oブロッキングを引き起こし、アプリ全体の処理落ちを招くのを防ぐため、`System.Threading.Channels`を用いた非同期キューによるバッチ書き込みを採用しました。
- **構造化ロギングの採用**:
  インデント等のテキスト装飾に頼るのではなく、`nameof()`を用いたタグ付けとLogLevelごとの自動カラーリングにより、コンソール上での可読性を担保しています。
