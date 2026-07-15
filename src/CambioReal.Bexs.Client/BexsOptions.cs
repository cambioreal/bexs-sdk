namespace CambioReal.Bexs;

/// <summary>Configuração do <see cref="BexsClient"/>.</summary>
public sealed class BexsOptions
{
    /// <summary>Nome da seção de configuração sugerida.</summary>
    public const string SectionName = "Bexs";

    /// <summary>Client ID OAuth2 (client_credentials), fornecido pela Bexs/Ebury.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret OAuth2. Deve vir do <c>pass</c> ou de um secret store — nunca do código.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Audience OAuth2 enviada no corpo JSON do token. Vazio usa o default do
    /// <see cref="Environment"/> (<c>payin-package-sandbox</c> / <c>https://forex.bexs.com.br</c>).
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Ambiente alvo. O padrão é <see cref="BexsEnvironment.Sandbox"/>, deliberadamente.</summary>
    public BexsEnvironment Environment { get; set; } = BexsEnvironment.Sandbox;

    /// <summary>Sobrescreve o endereço base da API derivado de <see cref="Environment"/>. Precisa terminar em <c>/</c>.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Sobrescreve o endereço da API de autenticação. Precisa terminar em <c>/</c>.</summary>
    public Uri? AuthBaseAddress { get; set; }

    /// <summary>
    /// Margem de segurança para renovar o token antes do vencimento real (<c>expires_in</c> = 3600s
    /// no sandbox, validado ao vivo). O legado cacheava 50min hardcoded com um TODO reconhecendo o
    /// risco de usar TTL fixo — aqui a expiração deriva do <c>expires_in</c> real menos esta margem.
    /// </summary>
    public TimeSpan TokenExpirationSkew { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Timeout de cada requisição HTTP. Paridade com o legado (30s), configurável.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Endereço base efetivo da API.</summary>
    public Uri ResolveBaseAddress() => BaseAddress ?? Environment.GetBaseAddress();

    /// <summary>Endereço efetivo da API de autenticação.</summary>
    public Uri ResolveAuthBaseAddress() => AuthBaseAddress ?? Environment.GetAuthBaseAddress();

    /// <summary>Audience efetiva.</summary>
    public string ResolveAudience() =>
        string.IsNullOrWhiteSpace(Audience) ? Environment.GetDefaultAudience() : Audience;

    /// <summary>Valida a configuração e lança se estiver inconsistente.</summary>
    /// <exception cref="InvalidOperationException">Alguma credencial obrigatória está ausente ou um base address é inválido.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException($"{nameof(BexsOptions)}.{nameof(ClientId)} é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException($"{nameof(BexsOptions)}.{nameof(ClientSecret)} é obrigatório.");
        }

        ValidateBaseAddress(ResolveBaseAddress(), nameof(BaseAddress));
        ValidateBaseAddress(ResolveAuthBaseAddress(), nameof(AuthBaseAddress));

        if (TokenExpirationSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(TokenExpirationSkew)} não pode ser negativo.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(Timeout)} precisa ser positivo.");
        }
    }

    private static void ValidateBaseAddress(Uri baseAddress, string propertyName)
    {
        if (!baseAddress.IsAbsoluteUri)
        {
            throw new InvalidOperationException($"{propertyName} precisa ser absoluto.");
        }

        if (!baseAddress.AbsolutePath.EndsWith('/'))
        {
            throw new InvalidOperationException($"{propertyName} precisa terminar em '/' (recebido: '{baseAddress}').");
        }
    }
}
