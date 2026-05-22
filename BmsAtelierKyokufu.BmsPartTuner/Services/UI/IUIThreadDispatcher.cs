namespace BmsAtelierKyokufu.BmsPartTuner.Services.UI;

public interface IUIThreadDispatcher
{
    Task InvokeAsync(Action action);
}
