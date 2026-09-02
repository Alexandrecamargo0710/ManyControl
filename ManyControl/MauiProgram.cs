using ManyControl.Data;
using ManyControl.Services;
using ManyControl.ViewModels;
using ManyControl.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManyControl;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "manycontrol-sync.db");

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(dbPath);
        builder.Services.AddDbContextFactory<FinanceDbContext>((serviceProvider, options) =>
        {
            var path = serviceProvider.GetRequiredService<string>();
            options.UseSqlite($"Data Source={path}");
        });
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<FinanceService>();
        builder.Services.AddSingleton<GoogleDriveService>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<UpdateService>();
        builder.Services.AddSingleton<ChangelogService>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ExtratoViewModel>();
        builder.Services.AddTransient<ExtratoPage>();
        builder.Services.AddTransient<ConfiguracoesViewModel>();
        builder.Services.AddTransient<ConfiguracoesPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        databaseService.InitializeAsync().GetAwaiter().GetResult();

        var financeService = scope.ServiceProvider.GetRequiredService<FinanceService>();
        financeService.LimparDespesasRecorrentesFuturasAsync().GetAwaiter().GetResult();
        financeService.ProcessarDespesasRecorrentesAsync(DateTime.Today).GetAwaiter().GetResult();

        return app;
    }
}
