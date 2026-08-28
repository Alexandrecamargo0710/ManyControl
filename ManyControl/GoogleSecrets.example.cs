namespace ManyControl;

/// <summary>
/// Modelo de configuração das credenciais de OAuth 2.0 do Google Cloud.
/// Para compilar com credenciais fixas locais, copie este arquivo como GoogleSecrets.cs
/// e preencha com seu Client ID e Client Secret.
/// 
/// O ManyControl também permite configurar essas credenciais dinamicamente
/// pela interface do aplicativo na aba Configurações.
/// </summary>
public static partial class GoogleSecrets
{
    public const string DefaultClientId = "";
    public const string DefaultClientSecret = "";
}
