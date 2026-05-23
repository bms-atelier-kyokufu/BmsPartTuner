using BmsAtelierKyokufu.BmsPartTuner.Services.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Services.Audio.AudioPlayer;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Services.Common;
using BmsAtelierKyokufu.BmsPartTuner.Services.UI;
using BmsAtelierKyokufu.BmsPartTuner.ViewModels;
using BmsAtelierKyokufu.BmsPartTuner.Views.Windows;
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
            // Settings Services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<LicenseLoaderService>();
            services.AddSingleton<UpdateService>();

            // Core Services (Phase 5: ISP 適用)
            services.AddSingleton<IInputValidationService, InputValidationService>();
            services.AddSingleton<IBmsOptimizationService, BmsOptimizationService>();
            services.AddSingleton<IAudioPlayerFactory, NAudioPlayerFactory>();
            services.AddSingleton<IUIThreadDispatcher>(provider =>
                new WpfUIThreadDispatcher(Application.Current.Dispatcher));
            services.AddSingleton(provider =>
                new AudioPreviewService(
                    provider.GetRequiredService<IUIThreadDispatcher>(),
                    provider.GetRequiredService<IAudioPlayerFactory>()));
            services.AddSingleton<InstrumentNameDetectionService>();

            // UI Services (Initializeパターン)
            services.AddSingleton<IUiElementService<ToastViewModel>, ToastNotificationService>();
            services.AddSingleton<IUiElementService<ResultCardData>, ResultCardService>();
            services.AddSingleton<IDragDropService>(provider =>
                new DragDropService(AppConstants.Files.SupportedBmsExtensions));
            services.AddSingleton<FileListFilterService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Windows
            services.AddTransient<MainWindow>();

            return services;
        }
    }
}
