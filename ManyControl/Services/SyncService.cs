using System.Globalization;
using System.Text.Json;
using ManyControl.Models;

namespace ManyControl.Services;

public class SyncService
{
    private const string LastSyncUtcKey = "manycontrol_last_sync_utc";
    private const string LastSyncModeKey = "manycontrol_last_sync_mode";
    private const string LastExportPathKey = "manycontrol_last_export_path";
    private const string LastSyncedDataVersionUtcKey = "manycontrol_last_synced_data_version_utc";
    private const string SyncFileName = "manycontrol-sync.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly FinanceService _financeService;
    private readonly GoogleDriveService _googleDriveService;
    private readonly string _backupFolder;
    private readonly string _syncFolder;
    private readonly string _syncFilePath;

    public SyncService(FinanceService financeService, GoogleDriveService googleDriveService)
    {
        _financeService = financeService;
        _googleDriveService = googleDriveService;
        _syncFolder = Path.Combine(FileSystem.AppDataDirectory, "sync");
        _backupFolder = Path.Combine(_syncFolder, "backups");
        _syncFilePath = Path.Combine(_syncFolder, SyncFileName);
    }

    public string SyncFilePath => _syncFilePath;

    public bool IsGoogleDriveConfigured => _googleDriveService.IsConfigured;

    public bool IsGoogleDriveConnected => _googleDriveService.IsConnected;

    public string? LastVisibleBackupError => _googleDriveService.LastVisibleBackupError;

    public async Task ConnectGoogleDriveAsync()
    {
        await _googleDriveService.AuthorizeAsync();
    }

    public async Task<string?> GetConnectedGoogleDriveEmailAsync()
    {
        return await _googleDriveService.GetConnectedUserEmailAsync();
    }

    public void DisconnectGoogleDrive()
    {
        _googleDriveService.Disconnect();
    }

    public void SaveGoogleDriveCredentials(string clientId, string clientSecret)
    {
        _googleDriveService.SaveCredentials(clientId, clientSecret);
    }

    public async Task SaveGoogleDriveCredentialsFromJsonAsync(Stream credentialsStream)
    {
        await _googleDriveService.SaveCredentialsFromJsonAsync(credentialsStream);
    }

    public string GetGoogleDriveClientId()
    {
        return _googleDriveService.GetClientId();
    }

    public async Task<SyncResult> SyncAsync()
    {
        if (_googleDriveService.IsConfigured)
        {
            return await SyncWithGoogleDriveAsync();
        }

        return await SyncWithLocalFileAsync();
    }

    public async Task<SyncResult> SyncWithLocalFileAsync()
    {
        Directory.CreateDirectory(_syncFolder);

        if (!File.Exists(_syncFilePath))
        {
            return await ExportAsync("Primeira sincronização criada com sucesso.");
        }

        var localPackage = await _financeService.CreateSyncPackageAsync();
        var remotePackage = await ReadPackageFromFileAsync(_syncFilePath);
        if (remotePackage is null)
        {
            return new SyncResult(false, "O arquivo de sincronização existe, mas não foi possível ler os dados dele.");
        }

        var localVersion = NormalizeVersion(localPackage.LastChangedAtUtc, localPackage.ExportedAtUtc);
        var remoteVersion = NormalizeVersion(remotePackage.LastChangedAtUtc, remotePackage.ExportedAtUtc);
        var lastSyncedVersion = GetLastSyncedDataVersionUtc();

        if (VersionsAreEqual(localVersion, remoteVersion))
        {
            SaveSyncPreferences("none", localVersion);
            return new SyncResult(true, "Tudo sincronizado. Nenhuma alteração nova encontrada.");
        }

        var localChangedAfterLastSync = localVersion > lastSyncedVersion;
        var remoteChangedAfterLastSync = remoteVersion > lastSyncedVersion;

        if (localChangedAfterLastSync && remoteChangedAfterLastSync && lastSyncedVersion != DateTime.MinValue)
        {
            await BackupCurrentLocalDataAsync("antes-mesclagem-local");
            await _financeService.ApplySyncPackageAsync(remotePackage);

            var mergedPackage = await _financeService.CreateSyncPackageAsync();
            var mergedVersion = NormalizeVersion(mergedPackage.LastChangedAtUtc, mergedPackage.ExportedAtUtc);
            await WritePackageToFileAsync(mergedPackage, _syncFilePath);

            SaveSyncPreferences("merge", mergedVersion);
            return new SyncResult(true, "Dados locais e do arquivo mesclados com sucesso!");
        }

        if (remoteVersion > localVersion)
        {
            await BackupCurrentLocalDataAsync("antes-importacao");
            await _financeService.ApplySyncPackageAsync(remotePackage);
            SaveSyncPreferences("import", remoteVersion);
            return new SyncResult(true, "Dados atualizados a partir do arquivo de sincronização.");
        }

        await BackupExistingSyncFileAsync("antes-exportacao");
        await WritePackageToFileAsync(localPackage, _syncFilePath);
        SaveSyncPreferences("export", localVersion);
        return new SyncResult(true, "Alterações locais enviadas para o arquivo de sincronização.");
    }

    public async Task<SyncResult> SyncWithGoogleDriveAsync()
    {
        if (!_googleDriveService.IsConfigured)
        {
            return new SyncResult(false, "Configure o Google Drive antes de sincronizar.");
        }

        try
        {
            Directory.CreateDirectory(_syncFolder);

            var localPackage = await _financeService.CreateSyncPackageAsync();
            var localVersion = NormalizeVersion(localPackage.LastChangedAtUtc, localPackage.ExportedAtUtc);
            var remoteFile = await _googleDriveService.DownloadSyncFileAsync();

            if (remoteFile is null)
            {
                var firstJson = SerializePackage(localPackage);
                await _googleDriveService.UploadSyncFileAsync(firstJson);
                await File.WriteAllTextAsync(_syncFilePath, firstJson);
                SaveSyncPreferences("export", localVersion);
                return new SyncResult(true, "Primeira sincronização criada no Google Drive.");
            }

            var remotePackage = DeserializePackageFromJson(remoteFile.Json);
            if (remotePackage is null)
            {
                return new SyncResult(false, "O arquivo do Google Drive existe, mas não foi possível ler os dados dele.");
            }

            var remoteVersion = NormalizeVersion(remotePackage.LastChangedAtUtc, remotePackage.ExportedAtUtc);
            var lastSyncedVersion = GetLastSyncedDataVersionUtc();

            if (VersionsAreEqual(localVersion, remoteVersion))
            {
                await File.WriteAllTextAsync(_syncFilePath, remoteFile.Json);
                await _googleDriveService.UploadSyncFileAsync(remoteFile.Json);
                SaveSyncPreferences("none", localVersion);
                return new SyncResult(true, "Tudo sincronizado com o Google Drive. Nenhuma alteração nova encontrada.");
            }

            var localChangedAfterLastSync = localVersion > lastSyncedVersion;
            var remoteChangedAfterLastSync = remoteVersion > lastSyncedVersion;

            // Se ambos os dispositivos tiveram alterações desde a última sincronização,
            // realizamos uma mesclagem automática segura: unimos os lançamentos por ID (Guid)
            // sem apagar nenhum dado de nenhum lado!
            if (localChangedAfterLastSync && remoteChangedAfterLastSync && lastSyncedVersion != DateTime.MinValue)
            {
                await BackupCurrentLocalDataAsync("antes-mesclagem-drive");

                // 1. Aplica dados remotos (do celular) na base local (computador), unindo registros
                await _financeService.ApplySyncPackageAsync(remotePackage);

                // 2. Cria o pacote unificado com os dados consolidados de ambos os aparelhos
                var mergedPackage = await _financeService.CreateSyncPackageAsync();
                var mergedVersion = NormalizeVersion(mergedPackage.LastChangedAtUtc, mergedPackage.ExportedAtUtc);
                var mergedJson = SerializePackage(mergedPackage);

                // 3. Atualiza o Google Drive e o arquivo local com a versão consolidada
                await _googleDriveService.UploadSyncFileAsync(mergedJson);
                await File.WriteAllTextAsync(_syncFilePath, mergedJson);

                SaveSyncPreferences("merge", mergedVersion);
                return new SyncResult(true, "Dados do computador e do celular mesclados com sucesso!");
            }

            if (remoteVersion > localVersion)
            {
                await BackupCurrentLocalDataAsync("antes-importacao-drive");
                await _financeService.ApplySyncPackageAsync(remotePackage);
                await File.WriteAllTextAsync(_syncFilePath, remoteFile.Json);
                SaveSyncPreferences("import", remoteVersion);
                return new SyncResult(true, "Dados atualizados a partir do Google Drive.");
            }

            await BackupExistingSyncFileAsync("antes-exportacao-drive");

            var json = SerializePackage(localPackage);
            await _googleDriveService.UploadSyncFileAsync(json);
            await File.WriteAllTextAsync(_syncFilePath, json);
            SaveSyncPreferences("export", localVersion);
            return new SyncResult(true, "Alterações locais enviadas para o Google Drive.");
        }
        catch (Exception ex)
        {
            return new SyncResult(false, $"Não foi possível sincronizar com o Google Drive: {ex.Message}");
        }
    }

    public async Task<SyncResult> ExportAsync()
    {
        return await ExportAsync("Pacote de sincronização exportado com sucesso.");
    }

    public async Task<SyncResult> ExportAsync(string successMessage)
    {
        Directory.CreateDirectory(_syncFolder);

        await BackupExistingSyncFileAsync("antes-exportacao-manual");

        var package = await _financeService.CreateSyncPackageAsync();
        await WritePackageToFileAsync(package, _syncFilePath);

        SaveSyncPreferences("export", NormalizeVersion(package.LastChangedAtUtc, package.ExportedAtUtc));
        return new SyncResult(true, successMessage);
    }

    public async Task<SyncResult> ImportAsync(Stream packageStream)
    {
        var package = await JsonSerializer.DeserializeAsync<SyncPackage>(packageStream, JsonOptions);
        if (package is null)
        {
            return new SyncResult(false, "Não foi possível ler o pacote de sincronização.");
        }

        await BackupCurrentLocalDataAsync("antes-importacao-manual");
        await _financeService.ApplySyncPackageAsync(package);

        var version = NormalizeVersion(package.LastChangedAtUtc, package.ExportedAtUtc);
        SaveSyncPreferences("import", version);

        return new SyncResult(true, "Pacote de sincronização aplicado com sucesso.");
    }

    public string GetLastSyncText()
    {
        var raw = Preferences.Get(LastSyncUtcKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw) || !DateTime.TryParse(raw, null, DateTimeStyles.RoundtripKind, out var utc))
        {
            return "Nunca sincronizado";
        }

        var local = utc.ToLocalTime();
        return $"Hoje às {local:HH:mm}";
    }

    public string GetLastSyncModeText()
    {
        var mode = Preferences.Get(LastSyncModeKey, string.Empty);
        return mode switch
        {
            "export" => "Última ação: envio dos dados",
            "import" => "Última ação: recebimento dos dados",
            "none" => "Última ação: conferência sem mudanças",
            _ => "Última ação: nenhuma"
        };
    }

    public string GetLastExportPath()
    {
        return Preferences.Get(LastExportPathKey, string.Empty);
    }

    private async Task<SyncPackage?> ReadPackageFromFileAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SyncPackage>(stream, JsonOptions);
    }

    private static async Task WritePackageToFileAsync(SyncPackage package, string path)
    {
        var json = SerializePackage(package);
        await File.WriteAllTextAsync(path, json);
    }

    private static string SerializePackage(SyncPackage package)
    {
        return JsonSerializer.Serialize(package, JsonOptions);
    }

    private static SyncPackage? DeserializePackageFromJson(string json)
    {
        return JsonSerializer.Deserialize<SyncPackage>(json, JsonOptions);
    }

    private async Task BackupCurrentLocalDataAsync(string reason)
    {
        Directory.CreateDirectory(_backupFolder);

        var package = await _financeService.CreateSyncPackageAsync();
        var backupPath = Path.Combine(_backupFolder, BuildBackupFileName(reason));
        await WritePackageToFileAsync(package, backupPath);
    }

    private async Task BackupExistingSyncFileAsync(string reason)
    {
        if (!File.Exists(_syncFilePath))
        {
            return;
        }

        Directory.CreateDirectory(_backupFolder);

        var backupPath = Path.Combine(_backupFolder, BuildBackupFileName(reason));
        await using var source = File.OpenRead(_syncFilePath);
        await using var destination = File.Create(backupPath);
        await source.CopyToAsync(destination);
    }

    private static string BuildBackupFileName(string reason)
    {
        return $"manycontrol-{reason}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
    }

    private void SaveSyncPreferences(string mode, DateTime dataVersionUtc)
    {
        Preferences.Set(LastSyncUtcKey, DateTime.UtcNow.ToString("O"));
        Preferences.Set(LastSyncModeKey, mode);
        Preferences.Set(LastExportPathKey, _syncFilePath);
        Preferences.Set(LastSyncedDataVersionUtcKey, dataVersionUtc.ToString("O"));
    }

    private DateTime GetLastSyncedDataVersionUtc()
    {
        var raw = Preferences.Get(LastSyncedDataVersionUtcKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw) || !DateTime.TryParse(raw, null, DateTimeStyles.RoundtripKind, out var utc))
        {
            return DateTime.MinValue;
        }

        return utc.ToUniversalTime();
    }

    private static DateTime NormalizeVersion(DateTime lastChangedAtUtc, DateTime exportedAtUtc)
    {
        var version = lastChangedAtUtc == DateTime.MinValue ? exportedAtUtc : lastChangedAtUtc;
        return version.Kind == DateTimeKind.Utc ? version : version.ToUniversalTime();
    }

    private static bool VersionsAreEqual(DateTime left, DateTime right)
    {
        return Math.Abs((left - right).TotalSeconds) < 1;
    }
}

public record SyncResult(bool Success, string Message);
