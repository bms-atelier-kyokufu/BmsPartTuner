# BmsPartTuner ソフトウェアアーキテクチャ

<div style="display: flex; justify-content: center; margin:1em">
       <img src="img/Document-Hierarchy_JP.svg" alt="ドキュメント階層" width="40%" >
</div>

## アーキテクチャ概要

このドキュメントでは、BmsPartTuner のハイレベルなアーキテクチャについて説明します。本アプリケーションは WPF (.NET) を使用して構築されており、MVVM (Model-View-ViewModel) パターンを採用することで、保守性とテスト容易性を確保しています。

## アーキテクチャパターン

- MVVM: UIロジックとビジネスロジックを分離するために Model-View-ViewModel パターンを採用しています。CommunityToolkit.Mvvm を使用して、効率的なMVVM実装を行っています。
- 依存性の注入 (DI): Microsoft.Extensions.DependencyInjection を使用して依存関係を管理し、サービスとViewModel間の疎結合を促進しています。

- サービス指向: ビジネスロジックは可能な限りステートレスなサービス内にカプセル化し、ユニットテストを容易にしています。

## プロジェクト構造

ソリューションは以下の主要なプロジェクトで構成されています。

- BmsAtelierKyokufu.BmsPartTuner: UI、ロジック、リソースを含むメインのWPFアプリケーション。
- BmsAtelierKyokufu.BmsPartTuner.Tests: ユニットテストおよび統合テスト (xUnit)。

## システム構造図
![ARCHITECTURE図解](svg/ARCHITECTURE.svg)
<!-- https://www.mermaidchart.com/ -->
<!--
```mermaid
flowchart TB
 subgraph Controls["Custom Controls"]
        SCC["SlideConfirmationControl"]
        SFC["SmartFilterChips"]
        TC["ToastControl"]
 end
 subgraph View_Layer["View / UI"]
        MainWindow["MainWindow.xaml"]
        SettingsView["SettingsView.xaml"]
        Controls
 end
 subgraph Sub_ViewModels["Feature ViewModels"]
        FOVM["FileOperationsViewModel"]
        FLVM["FileListViewModel"]
        OVM["OptimizationViewModel"]
        SVM["SettingsViewModel"]
        NVM["NotificationViewModel"]
 end
 subgraph ViewModel_Layer["ViewModel"]
        MainVM["MainViewModel"]
        Sub_ViewModels
 end
 subgraph Service_Layer["Application Services"]
        FLFS["FileListFilterService"]
        SS["SettingsService"]
        RCS["ResultCardService"]
        LLS["LicenseLoaderService"]
        DDS["DragDropService"]
        TNS["ToastNotificationService"]
        TS["ThemeService"]
 end
 subgraph Audio_Engine["Audio Processing"]
        PACE["ParallelAudioComparisonEngine"]
        FWC["FastWaveCompare"]
        INDS["InstrumentNameDetectionService"]
 end
 subgraph Bms_Core["BMS Management"]
        BM["BmsManager"]
        BFR["BmsFileRewriter"]
        SE["SimulationEngine"]
 end
 subgraph Core_Layer["Domain Logic / Engine"]
        BOS["BmsOptimizationService"]
        Audio_Engine
        Bms_Core
 end
    MainWindow --\> MainVM
    SettingsView --\> SVM
    SCC -.-\> OVM
    SFC -.-\> FLVM
    MainVM --\> FOVM & FLVM & OVM & SVM & NVM
    FLVM --\> FLFS
    OVM --\> BOS
    SVM --\> SS & TS & LLS & LLS
    BOS --\> PACE & BM & BFR & SE
    PACE --\> FWC
    FLFS --\> INDS

    %% クラス定義：ここで青統一スタイルを適用
    classDef blueNode fill:#FFFFFF,stroke:#1565C0,stroke-width:1px,color:#0D47A1;
    classDef blueCluster fill:#E3F2FD,stroke:#64B5F6,stroke-width:2px,color:#0D47A1,rx:10,ry:10;

    style Controls fill:#F2F7FF,stroke:#8CBCFF,color:#001D36,stroke-dasharray: 5 5
    style Sub_ViewModels fill:#F2F7FF,stroke:#8CBCFF,color:#001D36,stroke-dasharray: 5 5
    style Audio_Engine fill:#F2F7FF,stroke:#8CBCFF,color:#001D36,stroke-dasharray: 5 5
    style Bms_Core fill:#F2F7FF,stroke:#8CBCFF,color:#001D36,stroke-dasharray: 5 5
    
    %% 全ノードとクラスターに適用
    class SCC,SFC,TC,MainWindow,SettingsView,FOVM,FLVM,OVM,SVM,NVM,MainVM,FLFS,SS,RCS,LLS,DDS,TNS,TS,PACE,FWC,INDS,BM,BFR,SE,BOS blueNode;
    class Controls,View_Layer,Sub_ViewModels,ViewModel_Layer,Service_Layer,Audio_Engine,Bms_Core,Core_Layer blueCluster;
-->


## ディレクトリ構成と責務

メインプロジェクト (BmsAtelierKyokufu.BmsPartTuner) は以下のように構成されています。

### 1\. コアロジック (/Core)

UIレイヤーから独立した、純粋なドメインロジックが含まれます。

* **Bms/**: BMSファイルの解析、変更、書き換えロジック (BmsFileRewriter, BmsManager)。  
* **Optimization/**: ファイル分割と結合シミュレーションのためのコアアルゴリズム (SimulationEngine)。  
* **Validation/**: ユーザー入力およびBMSファイルの整合性検証ロジック (BmsValidators)。  
* **Helpers/**: ドメイン固有のヘルパーアルゴリズム (例: AudioFileGroupingStrategy, RadixConvert)。

### 2\. 音声処理 (/Audio)

低レベルの音声操作と波形分析を担当します。

* **FastWaveCompare**: 音声波形データを比較するための最適化されたアルゴリズム。  
* **WaveValidation**: 音声ファイル形式の検証ロジック。  
* **AudioCacheManager**: パフォーマンス向上のための音声データキャッシュ機構。

### 3\. アプリケーションサービス (/Services)

UI (ViewModel) と Core/Audio ロジック間の相互作用を調整します。

* **BmsOptimizationService**: BMS最適化プロセスを調整するファサードサービス。  
* **AudioPreviewService**: NAudio を使用した音声再生とプレビューロジック。  
* **InputValidationService**: アプリケーション全体のユーザー入力を検証します。  
* **SettingsService**: 永続的なアプリケーション設定を管理します。

### **4\. UIレイヤー (/Views, /Controls, /ViewModels, /Themes)**

* **ViewModels/**: プレゼンテーションロジックと状態を保持します。  
* **Controls/**: 再利用可能なユーザーコントロール (例: SmartFilterChips, SlideConfirmationControl) や特定のビューコンポーネント。  
* **Themes/**: XAMLリソース、スタイル、コントロールテンプレート (デザイントークン、ダーク/ライトテーマ)。

### 5\. インフラストラクチャ (/Infrastructure)

WPF固有のインフラストラクチャとヘルパー。

* **Behaviors/**: UIインタラクションのための添付ビヘイビア (例: DragDropBehavior, NumericInputBehavior)。  
* **UI/**: UI固有のヘルパークラス。

## **データフロー**

1. **ユーザーインタラクション**: ユーザーが View (Window/UserControl) を操作します。  
2. **ViewModel**: View は ViewModel にバインドされています。ViewModel はコマンドを処理し、状態を更新します。  
3. **サービスレイヤー**: ViewModel はサービス (IBmsOptimizationService など) を呼び出して操作を実行します。  
4. **Core/Audio**: サービスは複雑な計算やファイル操作を Core および Audio コンポーネントに委譲します。  
5. **Model**: データは Model (DTO) を使用して受け渡されます。

## 安全性への配慮

ユーザーの大切なデータを守るため、以下の安全機構を実装している。

1. **アトミックなファイル保存**:  
   * BmsFileRewriter は直接上書きを行わない。  
   * Target.bms → Target.tmp に書き出し → Target.bms をバックアップ/削除 → Target.tmp を Target.bms にリネーム、という手順を踏む。  
2. **物理削除のUXフリクション**:  
   * 音源ファイルを削除するオプションが有効な場合、確認用スライドUI (SlideConfirmationControl) の操作方向を **「左から右」から「右から左」へ強制的に変更**する。  
   * 慣れによる誤操作を防ぎ、ユーザーに慎重な判断を促すメンタルモデル設計を採用。

## 技術スタック

* **Runtime**: .NET 10.0 (WPF)  
* **Audio IO**: NAudio (Wasapi / WaveStream)  
* **MVVM**: CommunityToolkit.Mvvm  
* **Markdown**: Markdig.Wpf (ライセンス表示用)  
* **UI Framework**: Material Design 3 (Orignal Custom Implementation based on XAML)  
* **Parallelism**: Task Parallel Library (TPL) / PLINQ
