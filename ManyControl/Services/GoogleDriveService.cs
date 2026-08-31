using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace ManyControl.Services;

public class GoogleDriveService
{
    private const string ClientIdKey = "manycontrol_google_client_id";
    private const string ClientSecretKey = "manycontrol_google_client_secret";
    private const string VisibleBackupFolderName = "ManyControl";
    private const string SyncFileName = "manycontrol-sync.json";
    private static readonly string[] Scopes = [DriveService.Scope.DriveAppdata, DriveService.Scope.DriveFile];

    private readonly string _tokenFolder;

    public GoogleDriveService()
    {
        _tokenFolder = Path.Combine(FileSystem.AppDataDirectory, "google-drive-token");
    }

    public static string DefaultClientId => GoogleSecrets.DefaultClientId;
    public static string DefaultClientSecret => GoogleSecrets.DefaultClientSecret;

    public string? LastVisibleBackupError { get; private set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GetClientId()) &&
        !string.IsNullOrWhiteSpace(GetClientSecret());

    public bool IsConnected =>
        IsConfigured &&
        Directory.Exists(_tokenFolder) &&
        Directory.GetFiles(_tokenFolder).Length > 0;

    public void Disconnect()
    {
        if (Directory.Exists(_tokenFolder))
        {
            try
            {
                Directory.Delete(_tokenFolder, true);
            }
            catch
            {
                // Ignora se algum arquivo estiver bloqueado temporariamente
            }
        }
    }

    public string GetClientId()
    {
        return Preferences.Get(ClientIdKey, DefaultClientId);
    }

    public string GetClientSecret()
    {
        return Preferences.Get(ClientSecretKey, DefaultClientSecret);
    }

    public async Task AuthorizeAsync()
    {
        await CreateDriveServiceAsync();
    }

    public async Task<string?> GetConnectedUserEmailAsync()
    {
        if (!IsConnected)
        {
            return null;
        }

        try
        {
            var service = await CreateDriveServiceAsync();
            var request = service.About.Get();
            request.Fields = "user(displayName, emailAddress)";
            var about = await request.ExecuteAsync();
            return about?.User?.EmailAddress;
        }
        catch
        {
            return null;
        }
    }

    public void SaveCredentials(string clientId, string clientSecret)
    {
        Preferences.Set(ClientIdKey, clientId.Trim());
        Preferences.Set(ClientSecretKey, clientSecret.Trim());
    }

    public async Task SaveCredentialsFromJsonAsync(Stream credentialsStream)
    {
        using var document = await JsonDocument.ParseAsync(credentialsStream);
        var root = document.RootElement;

        if (root.TryGetProperty("installed", out var installed))
        {
            SaveCredentialsFromJsonElement(installed);
            return;
        }

        if (root.TryGetProperty("web", out var web))
        {
            SaveCredentialsFromJsonElement(web);
            return;
        }

        SaveCredentialsFromJsonElement(root);
    }

    public async Task<GoogleDriveSyncFile?> DownloadSyncFileAsync()
    {
        var service = await CreateDriveServiceAsync();
        var file = await FindSyncFileAsync(service);

        if (file is null)
        {
            return null;
        }

        await using var stream = new MemoryStream();
        var request = service.Files.Get(file.Id);
        await request.DownloadAsync(stream);

        return new GoogleDriveSyncFile(
            Encoding.UTF8.GetString(stream.ToArray()),
            file.ModifiedTimeDateTimeOffset?.UtcDateTime ?? DateTime.MinValue);
    }

    public async Task UploadSyncFileAsync(string json)
    {
        var service = await CreateDriveServiceAsync();
        await UploadAppDataSyncFileAsync(service, json);

        try
        {
            await UploadVisibleBackupFileAsync(service, json);
            LastVisibleBackupError = null;
        }
        catch (Exception ex)
        {
            LastVisibleBackupError = ex.Message;
        }
    }

    private async Task UploadAppDataSyncFileAsync(DriveService service, string json)
    {
        var existing = await FindSyncFileAsync(service);
        var bytes = Encoding.UTF8.GetBytes(json);

        await using var stream = new MemoryStream(bytes);

        if (existing is null)
        {
            var metadata = new DriveFile
            {
                Name = SyncFileName,
                Parents = ["appDataFolder"]
            };

            var createRequest = service.Files.Create(metadata, stream, "application/json");
            createRequest.Fields = "id, modifiedTime";

            var upload = await createRequest.UploadAsync();
            EnsureUploadSucceeded(upload);
            return;
        }

        var updateMetadata = new DriveFile
        {
            Name = SyncFileName
        };

        var updateRequest = service.Files.Update(updateMetadata, existing.Id, stream, "application/json");
        updateRequest.Fields = "id, modifiedTime";

        var update = await updateRequest.UploadAsync();
        EnsureUploadSucceeded(update);
    }

    private async Task<DriveService> CreateDriveServiceAsync()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Configure o Client ID e o Client Secret do Google Drive antes de sincronizar.");
        }

        Directory.CreateDirectory(_tokenFolder);

        var secrets = new ClientSecrets
        {
            ClientId = GetClientId(),
            ClientSecret = GetClientSecret()
        };

        ICodeReceiver codeReceiver;
#if ANDROID
        codeReceiver = new MauiAndroidCodeReceiver(GetClientId());
#else
        codeReceiver = new WindowsOAuthReceiver();
#endif

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "manycontrol-user",
            CancellationToken.None,
            new FileDataStore(_tokenFolder, true),
            codeReceiver);

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ManyControl"
        });
    }

    private static async Task<DriveFile?> FindSyncFileAsync(DriveService service)
    {
        var request = service.Files.List();
        request.Spaces = "appDataFolder";
        request.Q = $"name = '{SyncFileName}' and trashed = false";
        request.Fields = "files(id, name, modifiedTime)";
        request.PageSize = 1;

        var result = await request.ExecuteAsync();
        return result.Files.FirstOrDefault();
    }

    private static async Task UploadVisibleBackupFileAsync(DriveService service, string json)
    {
        var folder = await FindOrCreateVisibleBackupFolderAsync(service);
        var existing = await FindVisibleBackupFileAsync(service, folder.Id);
        var bytes = Encoding.UTF8.GetBytes(json);

        await using var stream = new MemoryStream(bytes);

        if (existing is null)
        {
            var metadata = new DriveFile
            {
                Name = SyncFileName,
                Parents = [folder.Id]
            };

            var createRequest = service.Files.Create(metadata, stream, "application/json");
            createRequest.Fields = "id, modifiedTime";

            var upload = await createRequest.UploadAsync();
            EnsureUploadSucceeded(upload);
            return;
        }

        var updateMetadata = new DriveFile
        {
            Name = SyncFileName
        };

        var updateRequest = service.Files.Update(updateMetadata, existing.Id, stream, "application/json");
        updateRequest.Fields = "id, modifiedTime";

        var update = await updateRequest.UploadAsync();
        EnsureUploadSucceeded(update);
    }

    private static async Task<DriveFile> FindOrCreateVisibleBackupFolderAsync(DriveService service)
    {
        var existing = await FindVisibleBackupFolderAsync(service);
        if (existing is not null)
        {
            return existing;
        }

        var metadata = new DriveFile
        {
            Name = VisibleBackupFolderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = ["root"]
        };

        var request = service.Files.Create(metadata);
        request.Fields = "id, name";

        return await request.ExecuteAsync();
    }

    private static async Task<DriveFile?> FindVisibleBackupFolderAsync(DriveService service)
    {
        var request = service.Files.List();
        request.Spaces = "drive";
        request.Q = $"name = '{VisibleBackupFolderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        request.Fields = "files(id, name)";
        request.PageSize = 1;

        var result = await request.ExecuteAsync();
        return result.Files.FirstOrDefault();
    }

    private static async Task<DriveFile?> FindVisibleBackupFileAsync(DriveService service, string folderId)
    {
        var request = service.Files.List();
        request.Spaces = "drive";
        request.Q = $"name = '{SyncFileName}' and '{folderId}' in parents and trashed = false";
        request.Fields = "files(id, name, modifiedTime)";
        request.PageSize = 1;

        var result = await request.ExecuteAsync();
        return result.Files.FirstOrDefault();
    }

    private void SaveCredentialsFromJsonElement(JsonElement element)
    {
        var clientId = element.TryGetProperty("client_id", out var clientIdProperty)
            ? clientIdProperty.GetString()
            : string.Empty;

        var clientSecret = element.TryGetProperty("client_secret", out var clientSecretProperty)
            ? clientSecretProperty.GetString()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("O arquivo selecionado não contém Client ID e Client Secret válidos.");
        }

        SaveCredentials(clientId, clientSecret);
    }

    private static void EnsureUploadSucceeded(IUploadProgress upload)
    {
        if (upload.Status == UploadStatus.Failed)
        {
            throw upload.Exception ?? new InvalidOperationException("Falha ao enviar o arquivo para o Google Drive.");
        }
    }
}

public record GoogleDriveSyncFile(string Json, DateTime ModifiedAtUtc);
