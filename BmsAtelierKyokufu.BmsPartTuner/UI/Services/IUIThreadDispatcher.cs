namespace BmsAtelierKyokufu.BmsPartTuner.UI.Services;

public interface IUIThreadDispatcher
{
    Task InvokeAsync(Action action);
}
