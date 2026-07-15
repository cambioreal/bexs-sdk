using System.Net;
using CambioReal.Bexs.Auth;

namespace CambioReal.Bexs.Tests.Fakes;

internal static class TestClient
{
    public static BexsOptions NewOptions() => new()
    {
        Environment = BexsEnvironment.Sandbox,
        ClientId = "client-1",
        ClientSecret = "secret-1",
    };

    /// <summary>
    /// Monta um <see cref="BexsClient"/> sobre um transporte gravado, com token fixo — o pipeline
    /// real (<c>BexsAuthenticationHandler</c> → transporte), só sem rede.
    /// </summary>
    public static (BexsClient Client, RecordingHttpMessageHandler Transport) Create(
        params (HttpStatusCode Status, string Json)[] responses)
    {
        var transport = new RecordingHttpMessageHandler();

        foreach (var (status, json) in responses)
        {
            transport.RespondWith(status, json);
        }

        var options = NewOptions();

        var handler = new BexsAuthenticationHandler(new StubTokenProvider("tok-1"))
        {
            InnerHandler = transport,
        };

        var httpClient = new HttpClient(handler) { BaseAddress = options.ResolveBaseAddress() };

        return (new BexsClient(httpClient), transport);
    }

    public static (BexsClient Client, RecordingHttpMessageHandler Transport) CreateOk(string json = "{}") =>
        Create((HttpStatusCode.OK, json));
}
