using BmsAtelierKyokufu.BmsPartTuner.Services.UI;
using BmsAtelierKyokufu.BmsPartTuner.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

namespace BmsAtelierKyokufu.BmsPartTuner.Extensions
{
    /// <summary>
    /// IServiceCollectionの拡張メソッド。DIコンテナの構成を集約します。
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// アプリケーションに必要なすべてのサービスを登録します。
        /// </summary>
        public static IServiceCollection ConfigureAppServices(this IServiceCollection services)
        {
            var baseNamespace = typeof(App).Namespace ?? "BmsAtelierKyokufu.BmsPartTuner";

            // Scrutorによるアセンブリスキャン自動登録
            services.Scan(scan => scan
                .FromAssemblyOf<App>()
                    // Services配下のクラスをSingletonとして登録（自身および実装するインターフェースとして）
                    // InNamespaces は前方一致のため、サブ名前空間（Common, Audio等）も全て含まれます。
                    .AddClasses(classes => classes.InNamespaces($"{baseNamespace}.Services")
                        // 特殊な初期化が必要なクラスは除外
                        .Where(type => type != typeof(DragDropService) && type != typeof(WpfUIThreadDispatcher)))
                    .AsSelfWithInterfaces()
                    .WithSingletonLifetime()
                    // ViewModels配下のクラスをTransientとして登録
                    .AddClasses(classes => classes.InNamespaces($"{baseNamespace}.ViewModels"))
                    .AsSelf()
                    .WithTransientLifetime()
            );

            // スキャンで解決できない、特殊な初期化が必要なサービスの手動登録
            services.AddSingleton<IUIThreadDispatcher>(static _ =>
                new WpfUIThreadDispatcher(Application.Current.Dispatcher));

            services.AddSingleton<IDragDropService>(static _ =>
                new DragDropService(AppConstants.Files.SupportedBmsExtensions));

            // Windows
            services.AddTransient<MainWindow>();

            return services;
        }
    }
}
