namespace BmsAtelierKyokufu.BmsPartTuner.UI.Services;

[ExcludeFromCodeCoverage]
public class WpfUIThreadDispatcher(Dispatcher dispatcher) : IUIThreadDispatcher
{
    private readonly Dispatcher _dispatcher = dispatcher;

    public Task InvokeAsync(Action action)
    {
        return _dispatcher.InvokeAsync(action).Task;
    }
}
