using System.Windows.Threading;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.UI;

public class WpfUIThreadDispatcher(Dispatcher dispatcher) : IUIThreadDispatcher
{
    private readonly Dispatcher _dispatcher = dispatcher;

    public async Task InvokeAsync(Action action)
    {
        await _dispatcher.InvokeAsync(action);
    }
}
