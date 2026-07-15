using CambioReal.Bexs.Auth;

namespace CambioReal.Bexs.Tests.Fakes;

/// <summary>Token fixo, sem chamada de rede — para testes que não exercitam o fluxo de auth em si.</summary>
internal sealed class StubTokenProvider(string token, string tokenType = "Bearer") : IBexsTokenProvider
{
    public ValueTask<(string Token, string TokenType)> GetAccessTokenAsync(
        string? invalidatedToken, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult((token, tokenType));
}
