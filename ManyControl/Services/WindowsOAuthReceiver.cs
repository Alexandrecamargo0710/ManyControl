using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;

namespace ManyControl.Services;

/// <summary>
/// Receptor local de código OAuth 2.0 para Windows com página de retorno personalizada
/// e encerramento gracioso do servidor HTTP local, prevenindo erros de conexão no navegador.
/// </summary>
public class WindowsOAuthReceiver : ICodeReceiver
{
    private string _redirectUri = string.Empty;

    public string RedirectUri => _redirectUri;

    public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(
        AuthorizationCodeRequestUrl url,
        CancellationToken taskCancellationToken)
    {
        var port = GetRandomUnusedPort();
        _redirectUri = $"http://127.0.0.1:{port}/authorize/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(_redirectUri);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Não foi possível iniciar o receptor de autenticação na porta {port}: {ex.Message}", ex);
        }

        var authUrl = url.Build().ToString();
        try
        {
            await Launcher.OpenAsync(new Uri(authUrl));
        }
        catch
        {
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
        }

        try
        {
            var getContextTask = listener.GetContextAsync();
            var completedTask = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, taskCancellationToken));

            if (completedTask != getContextTask)
            {
                taskCancellationToken.ThrowIfCancellationRequested();
            }

            var context = await getContextTask;
            var query = context.Request.QueryString;

            var responseDict = new Dictionary<string, string>();
            foreach (string? key in query.AllKeys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    responseDict[key] = query[key] ?? string.Empty;
                }
            }

            var isError = responseDict.ContainsKey("error");
            var html = isError ? GetErrorHtml(responseDict.GetValueOrDefault("error_description", "Acesso cancelado.")) : GetSuccessHtml();
            var buffer = Encoding.UTF8.GetBytes(html);

            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, taskCancellationToken);
            await context.Response.OutputStream.FlushAsync(taskCancellationToken);
            context.Response.OutputStream.Close();

            // Breve espera para o navegador renderizar a resposta antes de encerrar o listener
            await Task.Delay(600, CancellationToken.None);

            return new AuthorizationCodeResponseUrl(responseDict);
        }
        finally
        {
            try
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }
            }
            catch
            {
                // Ignora falhas de cleanup
            }
        }
    }

    private static int GetRandomUnusedPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static string GetSuccessHtml() => """
<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>ManyControl - Autenticação Concluída</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
        body {
            background: linear-gradient(135deg, #0b0f19 0%, #111827 100%);
            color: #f3f4f6;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
        }
        .card {
            background: #1f2937;
            border: 1px solid #374151;
            border-radius: 20px;
            padding: 40px;
            max-width: 480px;
            text-align: center;
            box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5), 0 10px 10px -5px rgba(0, 0, 0, 0.4);
            animation: fadeIn 0.4s ease-out;
        }
        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(12px); }
            to { opacity: 1; transform: translateY(0); }
        }
        .icon-container {
            width: 72px;
            height: 72px;
            background: rgba(16, 185, 129, 0.15);
            border: 2px solid #10b981;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 20px auto;
            color: #10b981;
            font-size: 36px;
            font-weight: bold;
        }
        h1 { font-size: 22px; font-weight: 700; margin-bottom: 12px; color: #ffffff; }
        p { font-size: 15px; line-height: 1.6; color: #9ca3af; margin-bottom: 24px; }
        .badge {
            display: inline-block;
            background: rgba(59, 130, 246, 0.15);
            border: 1px solid rgba(59, 130, 246, 0.4);
            color: #60a5fa;
            padding: 8px 18px;
            border-radius: 9999px;
            font-size: 13px;
            font-weight: 600;
        }
    </style>
</head>
<body>
    <div class="card">
        <div class="icon-container">✓</div>
        <h1>Conexão Realizada com Sucesso!</h1>
        <p>Sua conta do Google foi conectada ao <strong>ManyControl</strong> com segurança.<br/>Você já pode fechar esta aba e voltar ao aplicativo.</p>
        <span class="badge">Fechando aba automaticamente...</span>
    </div>
    <script>
        setTimeout(function() {
            try {
                window.open('', '_self', '');
                window.close();
            } catch (e) {}
        }, 2200);
    </script>
</body>
</html>
""";

    private static string GetErrorHtml(string errorDescription) => $$"""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>ManyControl - Erro de Autenticação</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
        body {
            background: linear-gradient(135deg, #0b0f19 0%, #111827 100%);
            color: #f3f4f6;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
        }
        .card {
            background: #1f2937;
            border: 1px solid #374151;
            border-radius: 20px;
            padding: 40px;
            max-width: 480px;
            text-align: center;
            box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5);
        }
        .icon-container {
            width: 72px;
            height: 72px;
            background: rgba(239, 68, 68, 0.15);
            border: 2px solid #ef4444;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 20px auto;
            color: #ef4444;
            font-size: 36px;
            font-weight: bold;
        }
        h1 { font-size: 22px; font-weight: 700; margin-bottom: 12px; color: #ffffff; }
        p { font-size: 15px; line-height: 1.6; color: #9ca3af; margin-bottom: 20px; }
    </style>
</head>
<body>
    <div class="card">
        <div class="icon-container">✕</div>
        <h1>Falha na Autenticação</h1>
        <p>{{System.Net.WebUtility.HtmlEncode(errorDescription)}}</p>
    </div>
</body>
</html>
""";
}
