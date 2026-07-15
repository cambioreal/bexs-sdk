namespace CambioReal.Bexs;

/// <summary>Ambiente da API Bexs Payin Package.</summary>
public enum BexsEnvironment
{
    /// <summary>Sandbox/demo — <c>https://sandbox.bexs.com.br/v1/</c>.</summary>
    Sandbox = 0,

    /// <summary>Produção — <c>https://apis.bexs.com.br/v1/</c>.</summary>
    Production = 1,
}

/// <summary>Resolve endereços e audience de cada <see cref="BexsEnvironment"/>.</summary>
public static class BexsEnvironmentExtensions
{
    /// <summary>
    /// Endereço base da API do ambiente, confirmado em <c>cerebro/config/bexs.php</c>
    /// (<c>connections.demo.url</c> / <c>connections.production.url</c>) e validado ao vivo contra
    /// o sandbox em 2026-07-15 (ver discovery.md §4).
    /// </summary>
    public static Uri GetBaseAddress(this BexsEnvironment environment) => environment switch
    {
        BexsEnvironment.Production => new Uri("https://apis.bexs.com.br/v1/", UriKind.Absolute),
        BexsEnvironment.Sandbox => new Uri("https://sandbox.bexs.com.br/v1/", UriKind.Absolute),
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Ambiente Bexs desconhecido."),
    };

    /// <summary>
    /// Endereço da API de autenticação — o MESMO host para sandbox e produção
    /// (<c>connections.*.auth_url</c> no legado); o que discrimina o ambiente na auth é a
    /// <c>audience</c>, não o host.
    /// </summary>
    public static Uri GetAuthBaseAddress(this BexsEnvironment environment) => environment switch
    {
        BexsEnvironment.Production or BexsEnvironment.Sandbox =>
            new Uri("https://auth.bexs.com.br/v1/", UriKind.Absolute),
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Ambiente Bexs desconhecido."),
    };

    /// <summary>
    /// Audience OAuth2 default do ambiente, confirmada em <c>cerebro/config/bexs.php</c>:
    /// <c>payin-package-sandbox</c> (demo) / <c>https://forex.bexs.com.br</c> (produção).
    /// </summary>
    public static string GetDefaultAudience(this BexsEnvironment environment) => environment switch
    {
        BexsEnvironment.Production => "https://forex.bexs.com.br",
        BexsEnvironment.Sandbox => "payin-package-sandbox",
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Ambiente Bexs desconhecido."),
    };
}
