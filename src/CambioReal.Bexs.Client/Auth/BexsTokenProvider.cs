using System.Net.Http.Json;
using CambioReal.Bexs.Http;
using CambioReal.Bexs.Serialization;
using Microsoft.Extensions.Options;

namespace CambioReal.Bexs.Auth;

/// <summary>
/// Cacheia o token OAuth2 da Bexs e o renova sob demanda.
/// </summary>
/// <remarks>
/// Espelha <c>CambioReal.Ripple.Auth.RippleTokenProvider</c>/<c>Bs2TokenProvider</c>: singleton,
/// single-flight (uma rajada de 401 concorrentes produz uma reautenticação, não N). Diferente da
/// BS2 (um token por escopo), a Bexs tem um único token por credencial/audience — cache e gate
/// únicos. A expiração deriva do <c>expires_in</c> real (3600s no sandbox) menos
/// <see cref="BexsOptions.TokenExpirationSkew"/> — corrige o TODO do legado, que cacheava 50min
/// fixos e admitia o risco de servir token expirado.
/// </remarks>
internal sealed class BexsTokenProvider : IBexsTokenProvider, IDisposable
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly BexsOptions options;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private CachedAccessToken? cachedToken;

    public BexsTokenProvider(IHttpClientFactory httpClientFactory, IOptions<BexsOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<(string Token, string TokenType)> GetAccessTokenAsync(
        string? invalidatedToken, CancellationToken cancellationToken = default)
    {
        if (TryUseCached(invalidatedToken, out var token, out var tokenType))
        {
            return (token, tokenType);
        }

        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (TryUseCached(invalidatedToken, out token, out tokenType))
            {
                return (token, tokenType);
            }

            var fresh = await RequestTokenAsync(cancellationToken);
            cachedToken = fresh;
            return (fresh.Value, fresh.TokenType);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public void Dispose() => refreshGate.Dispose();

    private bool TryUseCached(string? invalidatedToken, out string token, out string tokenType)
    {
        token = string.Empty;
        tokenType = string.Empty;

        var current = cachedToken;

        if (current is null)
        {
            return false;
        }

        if (invalidatedToken is not null && string.Equals(current.Value, invalidatedToken, StringComparison.Ordinal))
        {
            return false;
        }

        if (timeProvider.GetUtcNow() >= current.ExpiresAtUtc)
        {
            return false;
        }

        token = current.Value;
        tokenType = current.TokenType;
        return true;
    }

    private async Task<CachedAccessToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(BexsClientNames.Auth);

        // Confirmado no legado (AbstractRequest::authenticate()) e ao vivo: corpo JSON com
        // audience obrigatória, credenciais SEMPRE no corpo — sem Authorization: Basic.
        var body = new BexsTokenRequest(
            GrantType: "client_credentials",
            ClientId: options.ClientId,
            ClientSecret: options.ClientSecret,
            Audience: options.ResolveAudience());

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BexsPaths.Token, UriKind.Relative))
        {
            Content = JsonContent.Create(body, options: BexsJson.Options),
        };

        var issuedAt = timeProvider.GetUtcNow();

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BexsAuthenticationException(
                response.StatusCode,
                errorCode: null,
                $"Falha ao autenticar na Bexs (HTTP {(int)response.StatusCode}).",
                errorBody);
        }

        var payload = await response.Content.ReadFromJsonAsync<BexsTokenResponse>(BexsJson.Options, cancellationToken)
            ?? throw new BexsAuthenticationException($"A Bexs devolveu um corpo vazio em POST {BexsPaths.Token}.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new BexsAuthenticationException("A Bexs devolveu um access_token vazio.");
        }

        var lifetime = TimeSpan.FromSeconds(payload.ExpiresIn);
        var skew = options.TokenExpirationSkew;

        if (skew >= lifetime)
        {
            skew = TimeSpan.FromTicks(lifetime.Ticks / 2);
        }

        var tokenType = string.IsNullOrWhiteSpace(payload.TokenType) ? "Bearer" : payload.TokenType;

        return new CachedAccessToken(payload.AccessToken, tokenType, issuedAt + lifetime - skew);
    }
}
