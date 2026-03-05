using System.Windows.Threading;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

public class WpfUIThreadDispatcher : IUIThreadDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfUIThreadDispatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task InvokeAsync(Action action)
    {
        await _dispatcher.InvokeAsync(action);
    }
}
