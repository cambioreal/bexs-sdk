using System.Net;
using CambioReal.Bexs.Auth;
using CambioReal.Bexs.Http;
using CambioReal.Bexs.Tests.Fakes;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace CambioReal.Bexs.Tests;

public sealed class TokenProviderTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthenticatesOnceAndCachesTheToken()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"));

        (await provider.GetAccessTokenAsync(null)).Token.ShouldBe("tok-1");
        (await provider.GetAccessTokenAsync(null)).Token.ShouldBe("tok-1");

        transport.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AuthRequestIsJsonWithAudienceAndWithoutBasicHeader()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"));

        await provider.GetAccessTokenAsync(null);

        var request = transport.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);

        // Host de auth SEPARADO do host de API — connections.demo.auth_url no legado.
        request.RequestUri!.ToString().ShouldBe("https://auth.bexs.com.br/v1/token");

        // Confirmado no legado e ao vivo: corpo JSON (não form-urlencoded), audience obrigatória,
        // credenciais sempre no corpo — sem Authorization: Basic.
        request.Authorization.ShouldBeNull();
        request.ContentType.ShouldBe("application/json");
        request.Body.ShouldNotBeNull();
        request.Body!.ShouldContain("\"grant_type\":\"client_credentials\"");
        request.Body!.ShouldContain("\"client_id\":\"client-1\"");
        request.Body!.ShouldContain("\"client_secret\":\"secret-1\"");
        request.Body!.ShouldContain("\"audience\":\"payin-package-sandbox\"");
    }

    [Fact]
    public async Task RenewsAfterExpiryMinusSkew()
    {
        var clock = new MutableTimeProvider(Epoch);
        var (provider, transport) = Build(clock, TokenResponse("tok-1"), TokenResponse("tok-2"));

        (await provider.GetAccessTokenAsync(null)).Token.ShouldBe("tok-1");

        // expires_in = 3600 (validado ao vivo), skew = 60 → o token vale até Epoch + 3540s.
        clock.Advance(TimeSpan.FromSeconds(3539));
        (await provider.GetAccessTokenAsync(null)).Token.ShouldBe("tok-1");

        clock.Advance(TimeSpan.FromSeconds(2));
        (await provider.GetAccessTokenAsync(null)).Token.ShouldBe("tok-2");

        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task InvalidatedTokenForcesRenewalEvenIfNotExpired()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"), TokenResponse("tok-2"));

        (await provider.GetAccessTokenAsync(null)).Token.ShouldBe("tok-1");
        (await provider.GetAccessTokenAsync("tok-1")).Token.ShouldBe("tok-2");

        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ConcurrentInvalidationsShareASingleRefresh()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"), TokenResponse("tok-2"));

        await provider.GetAccessTokenAsync(null);

        var refreshed = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => provider.GetAccessTokenAsync("tok-1").AsTask()));

        refreshed.ShouldAllBe(result => result.Token == "tok-2");
        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FailedAuthenticationThrows()
    {
        var transport = new RecordingHttpMessageHandler();
        transport.RespondWith(HttpStatusCode.BadRequest, """{"error":"invalid_client","error_description":"Invalid client credentials"}""");

        var provider = NewProvider(transport, TestClient.NewOptions(), new MutableTimeProvider(Epoch));

        var error = await Should.ThrowAsync<BexsAuthenticationException>(
            async () => await provider.GetAccessTokenAsync(null));

        error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DefaultsToBearerWhenTokenTypeMissing()
    {
        var (provider, _) = Build(
            new MutableTimeProvider(Epoch),
            """{"access_token":"tok-1","expires_in":3600}""");

        var result = await provider.GetAccessTokenAsync(null);
        result.TokenType.ShouldBe("Bearer");
    }

    private static string TokenResponse(string token, int expiresIn = 3600) =>
        $$"""{"access_token":"{{token}}","token_type":"Bearer","expires_in":{{expiresIn}}}""";

    private static (IBexsTokenProvider Provider, RecordingHttpMessageHandler Transport) Build(
        TimeProvider clock,
        params string[] responses)
    {
        var transport = new RecordingHttpMessageHandler();

        foreach (var response in responses)
        {
            transport.RespondWith(HttpStatusCode.OK, response);
        }

        return (NewProvider(transport, TestClient.NewOptions(), clock), transport);
    }

    private static BexsTokenProvider NewProvider(RecordingHttpMessageHandler transport, BexsOptions options, TimeProvider clock) =>
        new(new SingleHandlerHttpClientFactory(transport, options.ResolveAuthBaseAddress()), Options.Create(options), clock);
}
