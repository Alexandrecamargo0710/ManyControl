using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ManyControl.Services;

public class UpdateService
{
    private const string RepoOwner = "Alexandrecamargo0710";
    private const string RepoName = "ManyControl";
    private const string GitHubApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService>? _logger;

    public UpdateService(ILogger<UpdateService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ManyControl-App", "1.0"));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Falha ao consultar releases do GitHub: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;

            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            var remoteVersion = ParseVersion(tagName);
            var currentVersion = AppInfo.Current.Version;

            if (remoteVersion == null || remoteVersion <= currentVersion)
            {
                return new UpdateInfo
                {
                    TagName = tagName,
                    Version = remoteVersion ?? currentVersion,
                    ReleaseTitle = name,
                    ReleaseNotes = body,
                    IsNewer = false
                };
            }

            // Procurar o asset correto para a plataforma atual
            string? downloadUrl = null;
            string? fileName = null;
            long fileSize = 0;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
#if WINDOWS
                var asset = assets.EnumerateArray().FirstOrDefault(a =>
                {
                    var assetName = a.GetProperty("name").GetString() ?? string.Empty;
                    return assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                });
#elif ANDROID
                var asset = assets.EnumerateArray().FirstOrDefault(a =>
                {
                    var assetName = a.GetProperty("name").GetString() ?? string.Empty;
                    return assetName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase);
                });
#else
                var asset = default(JsonElement);
#endif
                if (asset.ValueKind != JsonValueKind.Undefined)
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    fileName = asset.GetProperty("name").GetString();
                    fileSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                }
            }

            return new UpdateInfo
            {
                TagName = tagName,
                Version = remoteVersion,
                ReleaseTitle = name,
                ReleaseNotes = body,
                DownloadUrl = downloadUrl,
                FileName = fileName,
                FileSize = fileSize,
                IsNewer = true
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao verificar atualizações.");
            return null;
        }
    }

    public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            throw new InvalidOperationException("URL de download da atualização não encontrada.");
        }

        var destinationFolder = FileSystem.CacheDirectory;
        var destinationPath = Path.Combine(destinationFolder, updateInfo.FileName ?? "update_package");

        // Remove arquivo anterior se existir
        if (File.Exists(destinationPath))
        {
            try { File.Delete(destinationPath); } catch { }
        }

        using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSize;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

        var buffer = new byte[65536];
        long totalBytesRead = 0;
        int bytesRead;
        double lastReportedProgress = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                var percentage = (double)totalBytesRead / totalBytes;
                if (percentage - lastReportedProgress >= 0.01 || percentage >= 0.99)
                {
                    lastReportedProgress = percentage;
                    progress.Report(percentage);
                }
            }
        }

        progress?.Report(1.0);
        return destinationPath;
    }

    public void InstallUpdate(string downloadedFilePath)
    {
        if (!File.Exists(downloadedFilePath))
        {
            throw new FileNotFoundException("Arquivo de atualização não encontrado.", downloadedFilePath);
        }

#if WINDOWS
        // No Windows, executa o instalador em modo silencioso ou padrão e fecha o app
        var startInfo = new ProcessStartInfo
        {
            FileName = downloadedFilePath,
            Arguments = "/VERYSILENT /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = true
        };

        Process.Start(startInfo);
        // Força o encerramento imediato deste processo para que o instalador substitua os binários e abra a nova versão de forma limpa
        Environment.Exit(0);
#elif ANDROID
        var context = Android.App.Application.Context;
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var javaFile = new Java.IO.File(downloadedFilePath);

        if (!javaFile.Exists())
        {
            throw new FileNotFoundException("Arquivo APK não encontrado para instalação.", downloadedFilePath);
        }

        javaFile.SetReadable(true, false);

        // Se Android 8.0+ (API 26+), verifica se tem permissão para instalar fontes desconhecidas
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
        {
            if (context.PackageManager != null && !context.PackageManager.CanRequestPackageInstalls())
            {
                var permissionIntent = new Android.Content.Intent(
                    Android.Provider.Settings.ActionManageUnknownAppSources,
                    Android.Net.Uri.Parse($"package:{context.PackageName}"));
                permissionIntent.AddFlags(Android.Content.ActivityFlags.NewTask);

                if (activity != null)
                {
                    activity.StartActivity(permissionIntent);
                }
                else
                {
                    context.StartActivity(permissionIntent);
                }
                return;
            }
        }

        var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            context,
            $"{context.PackageName}.fileprovider",
            javaFile);

        var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
        intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
        intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
        intent.AddFlags(Android.Content.ActivityFlags.GrantPrefixUriPermission);
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        intent.AddFlags(Android.Content.ActivityFlags.ClearTop);

        // Concede permissão explícita de leitura para o PackageInstaller do sistema
        var resolveInfoList = context.PackageManager?.QueryIntentActivities(intent, Android.Content.PM.PackageInfoFlags.MatchDefaultOnly);
        if (resolveInfoList != null)
        {
            foreach (var resolveInfo in resolveInfoList)
            {
                var packageName = resolveInfo.ActivityInfo?.PackageName;
                if (!string.IsNullOrEmpty(packageName))
                {
                    context.GrantUriPermission(packageName, apkUri, Android.Content.ActivityFlags.GrantReadUriPermission);
                }
            }
        }

        if (activity != null)
        {
            activity.StartActivity(intent);
        }
        else
        {
            context.StartActivity(intent);
        }
#else
        throw new PlatformNotSupportedException("Instalação automática não suportada nesta plataforma.");
#endif
    }

    private static Version? ParseVersion(string tag)
    {
        var cleaned = tag.Trim().TrimStart('v', 'V');
        if (Version.TryParse(cleaned, out var version))
        {
            return version;
        }

        // Se for versão de 2 números (ex: "1.0"), expandir para "1.0.0"
        if (cleaned.Count(c => c == '.') == 1 && Version.TryParse(cleaned + ".0", out var shortVersion))
        {
            return shortVersion;
        }

        return null;
    }
}

public class UpdateInfo
{
    public string TagName { get; set; } = string.Empty;
    public Version Version { get; set; } = new Version(1, 0, 0);
    public string ReleaseTitle { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public bool IsNewer { get; set; }
}
