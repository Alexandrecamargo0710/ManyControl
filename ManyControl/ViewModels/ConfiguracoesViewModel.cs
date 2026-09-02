using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManyControl.Services;

namespace ManyControl.ViewModels;

public partial class ConfiguracoesViewModel : ObservableObject
{
    private readonly SyncService _syncService;
    private readonly UpdateService _updateService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    public partial string LastSyncText { get; set; } = "Nunca sincronizado";

    [ObservableProperty]
    public partial string LastSyncModeText { get; set; } = "Última ação: nenhuma";

    [ObservableProperty]
    public partial string SyncStatusText { get; set; } = "Pronto para sincronizar";

    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial bool IsGoogleDriveConnected { get; set; }

    [ObservableProperty]
    public partial bool IsGoogleDriveDisconnected { get; set; } = true;

    [ObservableProperty]
    public partial string ConnectedEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalSyncPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AppVersionText { get; set; } = $"v{AppInfo.Current.VersionString}";

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = "Clique para verificar novas versões";

    [ObservableProperty]
    public partial bool IsCheckingUpdate { get; set; }

    [ObservableProperty]
    public partial bool IsDownloadingUpdate { get; set; }

    private readonly ChangelogService _changelogService;

    [ObservableProperty]
    public partial double UpdateProgress { get; set; }

    [ObservableProperty]
    public partial string UpdateProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLightTheme { get; set; }

    [ObservableProperty]
    public partial string ThemeText { get; set; } = "Tema Escuro";

    [ObservableProperty]
    public partial VersaoInfo VersaoAtual { get; set; } = new();

    [ObservableProperty]
    public partial List<VersaoInfo> HistoricoVersoes { get; set; } = [];

    [ObservableProperty]
    public partial bool IsHistoricoModalOpen { get; set; }

    public ConfiguracoesViewModel(SyncService syncService, UpdateService updateService, IDialogService dialogService, ChangelogService changelogService)
    {
        _syncService = syncService;
        _updateService = updateService;
        _dialogService = dialogService;
        _changelogService = changelogService;

        var savedTheme = Preferences.Get("app_theme", "Dark");
        IsDarkTheme = savedTheme != "Light";
        IsLightTheme = savedTheme == "Light";
        ThemeText = IsDarkTheme ? "Tema Escuro" : "Tema Claro";

        VersaoAtual = _changelogService.GetVersaoAtual();
        HistoricoVersoes = _changelogService.GetHistorico();
    }

    public string ThemeToggleIcon => IsDarkTheme ? "☀️" : "🌙";
    public string ThemeToggleImage => IsDarkTheme ? "ic_sun_yellow.png" : "ic_moon_blue.png";
    public string ThemeEscuroIcon => IsDarkTheme ? "ic_moon_white.png" : "ic_moon_dark.png";
    public string ThemeClaroIcon => IsLightTheme ? "ic_sun_white.png" : (IsDarkTheme ? "ic_sun_white.png" : "ic_sun_dark.png");
    public Color ThemeEscuroBg => IsDarkTheme ? Color.FromArgb("#2563EB") : (IsLightTheme ? Color.FromArgb("#F1F5F9") : Color.FromArgb("#1F2937"));
    public Color ThemeEscuroText => IsDarkTheme ? Colors.White : (IsLightTheme ? Color.FromArgb("#0F172A") : Colors.White);
    public Color ThemeClaroBg => IsLightTheme ? Color.FromArgb("#0284C7") : (IsDarkTheme ? Color.FromArgb("#1F2937") : Color.FromArgb("#F1F5F9"));
    public Color ThemeClaroText => IsLightTheme ? Colors.White : (IsDarkTheme ? Colors.White : Color.FromArgb("#0F172A"));

    [RelayCommand]
    public void SetTheme(string? theme)
    {
        if (theme == "light")
        {
            if (Application.Current != null) Application.Current.UserAppTheme = AppTheme.Light;
            Preferences.Set("app_theme", "Light");
            IsLightTheme = true;
            IsDarkTheme = false;
            ThemeText = "Tema Claro";
        }
        else
        {
            if (Application.Current != null) Application.Current.UserAppTheme = AppTheme.Dark;
            Preferences.Set("app_theme", "Dark");
            IsLightTheme = false;
            IsDarkTheme = true;
            ThemeText = "Tema Escuro";
        }

        OnPropertyChanged(nameof(ThemeToggleIcon));
        OnPropertyChanged(nameof(ThemeToggleImage));
        OnPropertyChanged(nameof(ThemeEscuroIcon));
        OnPropertyChanged(nameof(ThemeClaroIcon));
        OnPropertyChanged(nameof(ThemeEscuroBg));
        OnPropertyChanged(nameof(ThemeEscuroText));
        OnPropertyChanged(nameof(ThemeClaroBg));
        OnPropertyChanged(nameof(ThemeClaroText));
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        SetTheme(IsDarkTheme ? "light" : "dark");
    }

    [RelayCommand]
    public void AbrirHistorico()
    {
        HistoricoVersoes = _changelogService.GetHistorico();
        IsHistoricoModalOpen = true;
    }

    [RelayCommand]
    public void FecharHistorico()
    {
        IsHistoricoModalOpen = false;
    }

    [RelayCommand]
    public async Task AtualizarStatusAsync()
    {
        LastSyncText = _syncService.GetLastSyncText();
        LastSyncModeText = _syncService.GetLastSyncModeText();
        IsGoogleDriveConnected = _syncService.IsGoogleDriveConnected;
        IsGoogleDriveDisconnected = !IsGoogleDriveConnected;
        LocalSyncPath = _syncService.SyncFilePath;

        if (IsGoogleDriveConnected)
        {
            if (string.IsNullOrWhiteSpace(ConnectedEmail))
            {
                var email = await _syncService.GetConnectedGoogleDriveEmailAsync();
                ConnectedEmail = email ?? "Conta Google Vinculada";
            }
        }
        else
        {
            ConnectedEmail = string.Empty;
        }
    }

    [RelayCommand]
    public async Task ConectarGoogleAsync()
    {
        if (IsSyncing)
        {
            return;
        }

        IsSyncing = true;
        SyncStatusText = "Abrindo login do Google no navegador...";
        try
        {
            await _syncService.ConnectGoogleDriveAsync();
            await AtualizarStatusAsync();

            SyncStatusText = "Sincronizando dados pela primeira vez...";
            var result = await _syncService.SyncAsync();
            await AtualizarStatusAsync();

            SyncStatusText = result.Success ? "Conta conectada e sincronizada com sucesso!" : result.Message;
            await _dialogService.ShowAlertAsync(
                "Sucesso",
                "Conta do Google conectada e dados sincronizados com sucesso!",
                "OK");
        }
        catch (Exception ex)
        {
            SyncStatusText = "Não foi possível conectar ao Google.";
            await _dialogService.ShowAlertAsync("Falha na Conexão", ex.Message, "OK");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    public async Task DesconectarGoogleAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Desconectar Google Drive",
            "Deseja desconectar sua conta do Google Drive deste dispositivo?",
            "Desconectar",
            "Cancelar");

        if (!confirm)
        {
            return;
        }

        _syncService.DisconnectGoogleDrive();
        ConnectedEmail = string.Empty;
        await AtualizarStatusAsync();
        SyncStatusText = "Conta desconectada.";

        await _dialogService.ShowAlertAsync("Desconectado", "Sua conta do Google foi desconectada com sucesso.", "OK");
    }

    [RelayCommand]
    public async Task SincronizarAsync()
    {
        IsSyncing = true;
        SyncStatusText = "Sincronizando com o Google Drive...";
        try
        {
            var result = await _syncService.SyncAsync();
            SyncStatusText = result.Success ? "Sincronização concluída com sucesso!" : result.Message;
            await AtualizarStatusAsync();

            await _dialogService.ShowAlertAsync(
                result.Success ? "Sucesso" : "Atenção",
                result.Message,
                "OK");
        }
        catch (Exception ex)
        {
            SyncStatusText = $"Erro: {ex.Message}";
            await _dialogService.ShowAlertAsync("Erro na Sincronização", ex.Message, "OK");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    public async Task ExportarBackupManualAsync()
    {
        try
        {
            var result = await _syncService.ExportAsync();
            await AtualizarStatusAsync();
            await _dialogService.ShowAlertAsync("Backup Local", $"{result.Message}\n\nArquivo salvo em:\n{LocalSyncPath}", "OK");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Erro", ex.Message, "OK");
        }
    }

    [RelayCommand]
    public async Task ImportarBackupManualAsync()
    {
        try
        {
            var file = await _dialogService.PickFileAsync("Selecione o arquivo de sincronização (.json)");
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            var result = await _syncService.ImportAsync(stream);
            await AtualizarStatusAsync();

            await _dialogService.ShowAlertAsync("Restauração", result.Message, "OK");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Erro", ex.Message, "OK");
        }
    }

    [RelayCommand]
    public async Task VerificarAtualizacoesAsync()
    {
        if (IsCheckingUpdate || IsDownloadingUpdate)
        {
            return;
        }

        IsCheckingUpdate = true;
        UpdateStatusText = "Buscando atualizações no GitHub...";

        try
        {
            var update = await _updateService.CheckForUpdatesAsync();

            if (update is null)
            {
                UpdateStatusText = "Não foi possível conectar ao servidor de atualizações.";
                await _dialogService.ShowAlertAsync("Aviso", "Não foi possível verificar atualizações no momento. Verifique sua conexão.", "OK");
                return;
            }

            if (!update.IsNewer)
            {
                UpdateStatusText = $"O ManyControl já está na versão mais recente ({AppVersionText})!";
                await _dialogService.ShowAlertAsync("Atualizado", $"Você já está utilizando a versão mais recente ({AppVersionText}).", "OK");
                return;
            }

            UpdateStatusText = $"Nova versão {update.TagName} disponível!";

            var confirm = await _dialogService.ShowConfirmationAsync(
                $"Nova Versão ({update.TagName})",
                $"Uma nova versão do ManyControl ({update.TagName}) está disponível para download!\n\nDeseja atualizar agora?",
                "Atualizar Agora",
                "Depois");

            if (!confirm)
            {
                return;
            }

            await BaixarEInstalarAtualizacaoAsync(update);
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Erro ao verificar: {ex.Message}";
            await _dialogService.ShowAlertAsync("Erro", $"Falha ao verificar atualizações: {ex.Message}", "OK");
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    public async Task BaixarEInstalarAtualizacaoAsync(UpdateInfo update)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            await _dialogService.ShowAlertAsync("Aviso", "O pacote de instalação para esta plataforma ainda não está disponível nesta versão.", "OK");
            return;
        }

        IsDownloadingUpdate = true;
        UpdateProgress = 0;
        UpdateProgressText = "Iniciando download...";

        var progress = new Progress<double>(p =>
        {
            UpdateProgress = p;
            UpdateProgressText = $"Baixando atualização... {p * 100:F0}%";
        });

        try
        {
            var downloadedFile = await _updateService.DownloadUpdateAsync(update, progress);

            if (string.IsNullOrWhiteSpace(downloadedFile) || !File.Exists(downloadedFile))
            {
                throw new InvalidOperationException("Falha ao salvar o arquivo de atualização.");
            }

            UpdateProgressText = "Download concluído! Reiniciando aplicativo...";
            await Task.Delay(400);
            _updateService.InstallUpdate(downloadedFile);
        }
        catch (Exception ex)
        {
            UpdateProgressText = string.Empty;
            await _dialogService.ShowAlertAsync("Falha na Atualização", $"Não foi possível concluir a atualização: {ex.Message}", "OK");
        }
        finally
        {
            IsDownloadingUpdate = false;
        }
    }

    private static string FormatarNotasAtualizacao(string? rawNotes)
    {
        if (string.IsNullOrWhiteSpace(rawNotes))
        {
            return "• Melhorias de desempenho e correções gerais.";
        }

        var lines = rawNotes
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l)
                     && !l.StartsWith("## What's Changed", StringComparison.OrdinalIgnoreCase)
                     && !l.StartsWith("**Full Changelog**", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (lines.Count == 0)
        {
            return "• Melhorias de desempenho e correções gerais.";
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.StartsWith("* ") || line.StartsWith("- "))
            {
                line = "• " + line[2..];
            }
            else if (!line.StartsWith("• "))
            {
                line = "• " + line;
            }

            var inUrlIdx = line.IndexOf(" in https://", StringComparison.OrdinalIgnoreCase);
            if (inUrlIdx > 0)
            {
                line = line[..inUrlIdx];
            }

            lines[i] = line;
        }

        return string.Join("\n", lines);
    }
}
