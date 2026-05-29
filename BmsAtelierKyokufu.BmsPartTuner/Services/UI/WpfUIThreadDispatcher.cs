using System.Diagnostics.CodeAnalysis;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.UI;

[ExcludeFromCodeCoverage]
public class WpfUIThreadDispatcher(Dispatcher dispatcher) : IUIThreadDispatcher
{
    private readonly Dispatcher _dispatcher = dispatcher;

    public Task InvokeAsync(Action action)
    {
        return _dispatcher.InvokeAsync(action).Task;
    }
}
