namespace BmsAtelierKyokufu.BmsPartTuner.Services;

public interface IUIThreadDispatcher
{
    Task InvokeAsync(Action action);
}
