using System.Text.Json.Serialization;

namespace CambioReal.Bexs.Auth;

/// <summary>
/// Corpo de <c>POST {auth}/v1/token</c> — **JSON**, não form-urlencoded (diverge da RFC 6749 e da
/// API nova da Ebury; confirmado no legado <c>AbstractRequest::authenticate()</c> e validado ao
/// vivo em 2026-07-15, HTTP 200 real). A <c>audience</c> é obrigatória e discrimina o
/// produto/ambiente. Anotado explicitamente com <see cref="JsonPropertyNameAttribute"/> por ser um
/// contrato OAuth2-like fixo, independente da naming policy do resto do SDK.
/// </summary>
internal sealed record BexsTokenRequest(
    [property: JsonPropertyName("grant_type")] string GrantType,
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_secret")] string ClientSecret,
    [property: JsonPropertyName("audience")] string Audience);

/// <summary>
/// Resposta de <c>POST {auth}/v1/token</c>: <c>{ access_token, token_type: "Bearer",
/// expires_in: 3600 }</c> — validado ao vivo contra o sandbox (2026-07-15).
/// </summary>
internal sealed record BexsTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

/// <summary>Token em cache com seu instante de expiração absoluto.</summary>
internal sealed record CachedAccessToken(string Value, string TokenType, DateTimeOffset ExpiresAtUtc);

/// <summary>Nomes dos <c>HttpClient</c> registrados no container.</summary>
internal static class BexsClientNames
{
    /// <summary>Cliente dos recursos da API (payments/exchange-rate/merchants). Passa pelo <c>BexsAuthenticationHandler</c>.</summary>
    public const string Api = "bexs.api";

    /// <summary>
    /// Cliente usado só para <c>POST token</c>. Precisa ser separado do cliente de recursos por
    /// dois motivos: (1) se passasse pelo handler de autenticação, obter um token exigiria um
    /// token; (2) a Bexs usa um HOST distinto para auth (<c>auth.bexs.com.br</c>) — diferente da
    /// BS2, onde auth e API compartilham o host.
    /// </summary>
    public const string Auth = "bexs.auth";
}
