using BmsAtelierKyokufu.BmsPartTuner.UI.Services;
using BmsAtelierKyokufu.BmsPartTuner.UI.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

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
                    // Services配下、および各サブレイヤーのサービス実装クラスをSingletonとして登録（自身および実装するインターフェースとして）
                    .AddClasses(classes => classes.InNamespaces(
                            $"{baseNamespace}.Services",
                            $"{baseNamespace}.UI.Services",
                            $"{baseNamespace}.Core.Interfaces.Common",
                            $"{baseNamespace}.Core.Optimization",
                            $"{baseNamespace}.Infrastructure.Common",
                            $"{baseNamespace}.Infrastructure.Bms",
                            $"{baseNamespace}.Infrastructure.Audio"
                        )
                        // 特殊な初期化が必要なクラスは除外
                        .Where(type => type != typeof(DragDropService) && type != typeof(WpfUIThreadDispatcher))
                    ).AsSelfWithInterfaces()
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
