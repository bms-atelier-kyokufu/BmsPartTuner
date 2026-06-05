namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

public record TutorialStep(string Title, string Description);

/// <summary>
/// 初回起動時チュートリアルの状態と進行を管理するViewModel。
/// </summary>
public partial class TutorialViewModel : ObservableObject
{
    /// <summary>
    /// チュートリアルが完了またはスキップされたときに発火します。
    /// </summary>
    public event EventHandler? TutorialCompleted;

    public ObservableCollection<TutorialStep> Steps { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStepData))]
    [NotifyPropertyChangedFor(nameof(DisplayStepIndex))]
    [NotifyPropertyChangedFor(nameof(IsFirstStep))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(NextButtonText))]
    public partial int CurrentStepIndex { get; set; } = 0;


    public TutorialViewModel()
    {
        Steps =
        [
            new TutorialStep(
                Title: "BMS Part Tuner へようこそ",
                Description: "BMS Part Tunerは、重複した音源定義を自動で最適化し、\nBMSデータを軽量化するための専用ツールです。\n手作業ならば数時間かかる工程を、アルゴリズムの力で数秒で終わらせます。"
            ),
            new TutorialStep(
                Title: "かんたん3ステップで最適化",
                Description: "1. BMS・BMSONファイルを読み込む\n2. 『処理を開始』ボタンをクリック\n3. アプリが自動で最適化し、軽量化されたファイルを出力します！"
            ),
            new TutorialStep(
                Title: "細かなチューニングも可能",
                Description: "アルゴリズムによるマッチ許容度の自動調整や、\n未使用ファイルの物理削除など、\nニーズに合わせた高度な最適化にも対応しています。\nさっそく始めましょう！"
            )
        ];
    }

    public TutorialStep CurrentStepData => Steps[CurrentStepIndex];

    public int TotalSteps => Steps.Count;

    public int DisplayStepIndex => CurrentStepIndex + 1;

    public bool IsFirstStep => CurrentStepIndex == 0;

    public bool IsLastStep => CurrentStepIndex == Steps.Count - 1;

    public string NextButtonText => IsLastStep ? "はじめる" : "次へ";

    [RelayCommand]
    private void Next()
    {
        if (!IsLastStep)
        {
            CurrentStepIndex++;
        }
        else
        {
            Complete();
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (!IsFirstStep)
        {
            CurrentStepIndex--;
        }
    }

    [RelayCommand]
    private void Skip()
    {
        Complete();
    }

    private void Complete()
    {
        TutorialCompleted?.Invoke(this, EventArgs.Empty);
    }
}
