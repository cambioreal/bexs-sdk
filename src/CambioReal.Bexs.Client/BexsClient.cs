using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CambioReal.Bexs.Http;
using CambioReal.Bexs.Resources;
using CambioReal.Bexs.Serialization;

namespace CambioReal.Bexs;

/// <summary>
/// Cliente HTTP da API Bexs Payin Package.
/// </summary>
/// <remarks>
/// Camada de transporte, no mesmo espírito do <c>RippleClient</c>/<c>KiraClient</c>/<c>Bs2Client</c>.
/// Um único <see cref="HttpClient"/> de recursos (a Bexs tem um token universal por credencial,
/// diferente da BS2 que exige um por escopo), autenticado por um <c>BexsAuthenticationHandler</c>
/// (ver <see cref="BexsServiceCollectionExtensions"/>). Não há header de idempotência nem contexto
/// por requisição: a idempotência de negócio da Bexs vem do <c>correlation_id</c> no corpo, e um
/// header especulativo foi deliberadamente descartado (discovery.md §9).
/// </remarks>
public sealed class BexsClient
{
    private readonly HttpClient httpClient;

    /// <summary>Cria o cliente sobre um <see cref="HttpClient"/> já configurado e autenticado.</summary>
    public BexsClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        this.httpClient = httpClient;

        Payments = new PaymentsResource(this);
        ExchangeRates = new ExchangeRatesResource(this);
        Merchants = new MerchantsResource(this);
    }

    /// <summary>Pagamentos PIX payin. <c>payments</c>.</summary>
    public PaymentsResource Payments { get; }

    /// <summary>Cotações informativas. <c>exchange-rate</c>.</summary>
    public ExchangeRatesResource ExchangeRates { get; }

    /// <summary>Submerchants. <c>merchants</c>.</summary>
    public MerchantsResource Merchants { get; }

    internal async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path, content: null);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    internal async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken cancellationToken)
    {
        var content = JsonContent.Create(body, options: BexsJson.Options);
        using var request = CreateRequest(HttpMethod.Post, path, content);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    /// <summary>
    /// POST sem corpo (cancel). O legado não envia corpo nenhum (<c>AbstractRequest::request</c>
    /// pula o payload quando vazio) e trata a resposta como opcional — o shape de sucesso do
    /// cancel nunca foi fixado em fixture; ler defensivamente (corpo vazio ⇒ <see langword="null"/>).
    /// </summary>
    internal async Task<TResponse?> PostWithoutBodyAsync<TResponse>(string path, CancellationToken cancellationToken)
        where TResponse : class
    {
        using var request = CreateRequest(HttpMethod.Post, path, content: null);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TResponse>(body, BexsJson.Options);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (path.StartsWith('/'))
        {
            throw new ArgumentException(
                $"O path deve ser relativo e não pode começar com '/'. Recebido: '{path}'.",
                nameof(path));
        }

        return new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = content,
        };
    }

    private async Task<TResponse> SendAndReadAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(BexsJson.Options, cancellationToken);

        return payload ?? throw new BexsApiException(
            response.StatusCode,
            errorCode: null,
            "A API Bexs devolveu um corpo JSON vazio onde um valor era esperado.",
            responseBody: null);
    }

    private static async Task ThrowIfUnsuccessfulAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var (errorCode, errorMessage) = TryExtractError(body);
        var message = $"A API Bexs respondeu HTTP {(int)response.StatusCode} ({response.StatusCode})"
            + (errorMessage is null ? "." : $": {errorMessage}.");

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new BexsAuthenticationException(response.StatusCode, errorCode, message, body),
            _ => new BexsApiException(response.StatusCode, errorCode, message, body),
        };
    }

    /// <summary>
    /// Extrai código e mensagem do corpo de erro. Formas observadas (discovery.md §8):
    /// <c>{"code":"7","message":"Payment not found"}</c> (payments — <c>code</c> string numérica) e
    /// <c>{"message":"7 - Merchant not found"}</c> (merchants — sem campo <c>code</c>, código
    /// embutido na message). Não há catálogo público; não assumir schema único.
    /// </summary>
    private static (string? Code, string? Message) TryExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            string? code = null;
            string? message = null;

            if (root.TryGetProperty("code", out var codeElement))
            {
                code = codeElement.ValueKind switch
                {
                    JsonValueKind.String => codeElement.GetString(),
                    JsonValueKind.Number => codeElement.GetRawText(),
                    _ => null,
                };
            }

            if (root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
            {
                message = messageElement.GetString();
            }

            return (code, message);
        }
        catch (JsonException)
        {
            // Corpo não-JSON — status e corpo bruto já vão na exceção.
            return (null, null);
        }
    }
}
