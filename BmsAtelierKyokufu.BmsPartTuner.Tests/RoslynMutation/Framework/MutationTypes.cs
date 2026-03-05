namespace BmsAtelierKyokufu.BmsPartTuner.Tests.MutationFramework;

/// <summary>
/// 変異の種類を表す列挙型。
/// </summary>
public enum MutationType
{
    EqualToNotEqual,
    NotEqualToEqual,
    LessThanToLessOrEqual,
    LessThanToGreaterThan,
    GreaterThanToGreaterOrEqual,
    GreaterThanToLessThan,
    LessOrEqualToLessThan,
    LessOrEqualToGreaterOrEqual,
    GreaterOrEqualToGreaterThan,
    GreaterOrEqualToLessOrEqual,
    AndToOr,
    OrToAnd,
    AddToSubtract,
    SubtractToAdd,
    MultiplyToDivide,
    DivideToMultiply,
    TrueToFalse,
    FalseToTrue,
    NumericIncrement,
    NumericDecrement,
    NumericToZero,
    FirstToLast,
    LastToFirst,
    FirstOrDefaultToLastOrDefault,
    LastOrDefaultToFirstOrDefault,
    AnyToAll,
    AllToAny,
    TakeToSkip,
    SkipToTake,
    OrderByToOrderByDescending,
    OrderByDescendingToOrderBy,
    MinToMax,
    MaxToMin,
    SingleToFirst,
    FirstToSingle,
    SumToCount,
    CountToSum,
    WhereConditionNegation,
    PreIncrementToPreDecrement,
    PreDecrementToPreIncrement,
    PostIncrementToPostDecrement,
    PostDecrementToPostIncrement,
    BitwiseAndToOr,
    BitwiseOrToAnd,
    LeftShiftToRightShift,
    RightShiftToLeftShift,
    StringLiteralToEmpty,
    StringLiteralToNull,
    NullCoalescingRemoveDefault,
    ConditionalExpressionNegate,
    IfConditionNegate,
    FirstOrDefaultToFirst,
    LastOrDefaultToLast,
    SingleOrDefaultToSingle,
    SingleToFirstOrDefault
}

/// <summary>
/// 変異情報を格納するレコード。
/// </summary>
public record MutationInfo(
    string FilePath,
    MutationType Type,
    int Line,
    int Column,
    string OriginalCode,
    string MutatedCode);

/// <summary>
/// 変異テスト結果を格納するレコード。
/// </summary>
public record MutationTestResult(
    MutationInfo Mutation,
    bool IsKilled,
    string? ErrorMessage = null);
