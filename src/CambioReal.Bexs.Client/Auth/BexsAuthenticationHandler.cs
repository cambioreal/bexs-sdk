using System.Net;
using System.Net.Http.Headers;
using CambioReal.Bexs.Http;

namespace CambioReal.Bexs.Auth;

/// <summary>
/// Injeta <c>Authorization: {token_type} {access_token}</c> em toda requisição, reautenticando uma
/// única vez diante de um 401 — melhoria deliberada sobre o legado, que abortava sem retry.
/// </summary>
internal sealed class BexsAuthenticationHandler : DelegatingHandler
{
    private readonly IBexsTokenProvider tokenProvider;

    public BexsAuthenticationHandler(IBexsTokenProvider tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        this.tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (token, tokenType) = await tokenProvider.GetAccessTokenAsync(invalidatedToken: null, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, token);

        // A cópia precisa existir antes do envio — depois dele o Content já foi descartado.
        var retry = await request.CloneAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            retry.Dispose();
            return response;
        }

        response.Dispose();

        var (refreshedToken, refreshedTokenType) = await tokenProvider.GetAccessTokenAsync(invalidatedToken: token, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue(refreshedTokenType, refreshedToken);

        return await base.SendAsync(retry, cancellationToken);
    }
}
