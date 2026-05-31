# BMS Part Tuner ドキュメント

[![BMS Part Tuner Logo](./img/BmpPartTunerLogo_dark.svg)](https://github.com/bms-atelier-kyokufu/BmsPartTuner/)

**BMS Part Tuner** のドキュメントサイトへようこそ。
このツールは、数時間かかるはずの最適化処理を、純粋な計算機科学（Computer Science）の力によってわずか数秒で終わらせる「極限のサボり技術（最適化アルゴリズム）」の結晶です。

読者の目的や背景知識に合わせて、2つの主要なドキュメントルートを用意しています。

GitHubレポジトリはこちら: [![GitHub](https://img.shields.io/badge/-GitHub-181717?style=flat-square&logo=github) bms-atelier-kyokufu/BmsPartTuner](https://github.com/bms-atelier-kyokufu/BmsPartTuner)

> [!NOTE]
> **謝辞**
> 本システムにおける複雑なアルゴリズムの導出、ならびにこれらの高度な数理モデルの構築および実装の大部分は、**Google AI Gemini 3.0 Pro** および **Gemini 3.5 Flash** の膨大な計算力と論理的推論能力を多大に駆使することによって実現されました。開発者個人の力だけでは到底到達し得なかった領域であり、この場を借りて深い敬意と謝意を表します。

---

## ストーリーで読む「やさしい解説書」

**対象**: 一般開発者、BMS制作者、アルゴリズムの裏側に興味がある方

難しい数式を完全に排除し、「いかに賢く計算をサボり、PCの限界を引き出しているか」を図や身近な例え話（動画編集のタイムライン、アンケートなど）を使って直感的に解説します。AIとの壁打ちから生まれた泥臭い開発秘話や、知的パズルのような面白さを味わえる、読み物としてのドキュメントです。

**[やさしい解説書 を読む](casual_guide.md)**

---

## 理論と実装の「技術解説書」

**対象**: アルゴリズムエンジニア、コンピュータサイエンス専門家、AI（LLMエージェント）

大学レベルの応用数学・信号処理理論の枠組みに則り、システムのコアロジックを体系化した本格的な技術ドキュメントです。離散数学、グラフ理論、計算量理論に基づく厳密な証明や、実際の実装コードとの対応関係を網羅しています。

※このドキュメント群は人間だけでなく**LLMエージェントが本システムの深いコンテキストを理解するための知識ベース（AI向けコンテキスト）** としても最適化されています。

**[技術解説書インデックス へ進む](teach/index.md)**

---

## 開発・品質保証プロセス

本プロジェクトの意思決定記録は以下を参照してください。

- **[ADR (Architecture Decision Records)](https://github.com/bms-atelier-kyokufu/BmsPartTuner/tree/main/docs/adr)**: アーキテクチャの設計決定ログ

