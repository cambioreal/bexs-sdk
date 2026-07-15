namespace CambioReal.Bexs.Auth;

/// <summary>Fornece o access token OAuth2 da Bexs, com cache e renovação sob demanda.</summary>
public interface IBexsTokenProvider
{
    /// <summary>
    /// Devolve um token válido, do cache ou renovado. <paramref name="invalidatedToken"/> força a
    /// renovação quando o token informado (recebido num 401) ainda for o corrente — é o mecanismo
    /// do retry único do <c>BexsAuthenticationHandler</c>.
    /// </summary>
    public ValueTask<(string Token, string TokenType)> GetAccessTokenAsync(
        string? invalidatedToken, CancellationToken cancellationToken = default);
}
