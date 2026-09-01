using System.Windows;
using GameCompare.App.Services;
using GameCompare.Core;
using GameCompare.Core.Abstractions;
using GameCompare.Core.Services;
using GameCompare.Persistence;
using GameCompare.Playnite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameCompare.App;

public partial class App : Application
{
    private ServiceProvider? serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            services.AddWpfBlazorWebView();
            services.AddLogging(builder => builder.AddDebug());
            services.AddDbContextFactory<GameCompareDbContext>(options =>
                options.UseSqlite($"Data Source={AppDataPaths.DatabasePath}"));

            services.AddSingleton<IGameRepository, GameRepository>();
            services.AddSingleton<IPlayniteImporter, PlayniteImporter>();
            services.AddSingleton<IRankingAlgorithm, EloRankingAlgorithm>();
            services.AddSingleton<IPairSelectionStrategy, BalancedPairSelectionStrategy>();
            services.AddSingleton<GameSyncService>();
            services.AddSingleton<GameComparisonService>();
            services.AddSingleton<ObsidianNoteFileService>();
            services.AddSingleton<AppSettingsService>();
            services.AddSingleton<CoverImageService>();
            services.AddSingleton<FolderPickerService>();
            services.AddSingleton<ObsidianUriLauncher>();
            services.AddSingleton<ObsidianNotesService>();
            services.AddSingleton<MainWindow>();

            serviceProvider = services.BuildServiceProvider();
            await serviceProvider.GetRequiredService<IGameRepository>().InitializeAsync();

            var window = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"GameCompare 启动失败：\n\n{exception.Message}",
                "GameCompare",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
