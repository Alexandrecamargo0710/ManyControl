using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManyControl.Services;

namespace ManyControl.ViewModels;

public partial class ConfiguracoesViewModel : ObservableObject
{
    private readonly SyncService _syncService;
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

    public ConfiguracoesViewModel(SyncService syncService, IDialogService dialogService)
    {
        _syncService = syncService;
        _dialogService = dialogService;
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
        IsSyncing = true;
        SyncStatusText = "Abrindo login do Google no navegador...";
        try
        {
            await _syncService.ConnectGoogleDriveAsync();
            await AtualizarStatusAsync();

            SyncStatusText = "Conta conectada com sucesso!";
            await _dialogService.ShowAlertAsync(
                "Sucesso",
                $"Conta do Google conectada com sucesso!\nVocê já pode sincronizar seus dados.",
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
}
