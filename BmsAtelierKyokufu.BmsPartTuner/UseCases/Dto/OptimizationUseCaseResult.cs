namespace BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;

public class OptimizationUseCaseResult<T>
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public T? Data { get; set; }

    public static OptimizationUseCaseResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static OptimizationUseCaseResult<T> Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
