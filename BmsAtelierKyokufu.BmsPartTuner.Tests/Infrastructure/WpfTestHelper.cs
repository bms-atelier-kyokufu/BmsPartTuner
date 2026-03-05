using System.Windows;
using System.Windows.Threading;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Infrastructure
{
    /// <summary>
    /// 最小限のWPFテストヘルパー。
    /// STAスレッド上で実行中のDispatcher/Applicationを使用して非同期コードを実行します。
    /// 外部テストパッケージを使わずにViewModelテストを信頼性高く実行できます。
    /// </summary>
    public static class WpfTestHelper
    {
        public static Task RunStaAsync(Func<Task> testBody)
        {
            if (testBody == null) throw new ArgumentNullException(nameof(testBody));

            var tcs = new TaskCompletionSource<object?>();

            var thread = new Thread(() =>
            {
                try
                {
                    // このSTAスレッド上にWPF app/dispatcherが存在することを確保
                    if (Application.Current == null)
                    {
                        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    }

                    // SynchronizationContextを設定
                    var syncContext = new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher);
                    SynchronizationContext.SetSynchronizationContext(syncContext);

                    // テスト本体を実行するタスクを開始
                    var testTask = Task.Run(async () =>
                    {
                        try
                        {
                            await testBody();
                            tcs.TrySetResult(null);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    });

                    // Dispatcherループを実行（テスト完了まで）
                    var frame = new DispatcherFrame();
                    tcs.Task.ContinueWith(_ =>
                    {
                        Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false);
                    });
                    Dispatcher.PushFrame(frame);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return tcs.Task;
        }
    }
}
