---
adr-id: OPT-02
target-class: StorageTypeDetector
status: open
---

# Win32 APIによる高速なSSD判定と非同期I/O最適化

## SSD判定ユーティリティ

- 対象クラス: StorageTypeDetector

**設計判断 (Why this algorithm?)**

- **Win32 APIによるSSD判定**:
  ドライブがSSDかHDDかを判定する際、WMI (Windows Management Instrumentation) を使用すると非常に遅く（数秒かかることもある）、UIスレッドをブロックする危険があります。そのため、`DeviceIoControl` を直接呼び出し、`STORAGE_PROPERTY_QUERY` でシークペナルティの有無 (`IncursSeekPenalty`) を問い合わせる高速な手法を採用しました。
- **管理者権限不要のハンドル取得**:
  `CreateFile` で `dwDesiredAccess = 0` (アクセス権なし) を指定することで、管理者権限なしでもデバイスのメタデータ（プロパティ）を安全に取得できる設計としています。
