using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;

namespace ManyControl.Services;

public class MauiAndroidCodeReceiver : ICodeReceiver
{
    private readonly string _redirectUri;

    public string RedirectUri => _redirectUri;

    public MauiAndroidCodeReceiver(string clientId)
    {
        var prefix = clientId.Replace(".apps.googleusercontent.com", "");
        _redirectUri = $"com.googleusercontent.apps.{prefix}:/oauth2redirect";
    }

    public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(
        AuthorizationCodeRequestUrl url,
        CancellationToken taskCancellationToken)
    {
        var authUrl = url.Build();
        var callbackUri = new Uri(_redirectUri);

        WebAuthenticatorResult? authResult = null;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            authResult = await WebAuthenticator.Default.AuthenticateAsync(
                new WebAuthenticatorOptions
                {
                    Url = authUrl,
                    CallbackUrl = callbackUri,
                    PrefersEphemeralWebBrowserSession = false
                });
        });

        if (authResult == null || authResult.Properties == null)
        {
            throw new InvalidOperationException("Falha na autenticação com o Google: nenhum dado retornado pelo navegador.");
        }

        var dict = new Dictionary<string, string>();
        foreach (var kvp in authResult.Properties)
        {
            dict[kvp.Key] = kvp.Value;
        }

        return new AuthorizationCodeResponseUrl(dict);
    }
}
