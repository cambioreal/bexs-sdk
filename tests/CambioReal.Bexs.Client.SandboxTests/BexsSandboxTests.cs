using CambioReal.Bexs;
using CambioReal.Bexs.Auth;
using CambioReal.Bexs.Http;
using CambioReal.Bexs.Models;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace CambioReal.Bexs.SandboxTests;

/// <summary>
/// Integração sandbox real, opt-in — goal-loop §2.5: "nunca rodem por padrão em CI e não
/// imprimam segredos". Credenciais vêm de variáveis de ambiente populadas a partir do
/// <c>pass</c>, nunca hardcoded aqui:
///
/// <code>
/// set -a
/// eval "$(pass show cambio-real-v2/bexs/demo-env | sed -n \
///   's/^CLIENT_ID=/BEXS_SANDBOX_CLIENT_ID=/p;s/^CLIENT_SECRET=/BEXS_SANDBOX_CLIENT_SECRET=/p')"
/// set +a
/// dotnet test tests/CambioReal.Bexs.Client.SandboxTests/CambioReal.Bexs.Client.SandboxTests.csproj
/// </code>
///
/// Sem as duas variáveis, os testes falham explicitamente (não pulam em silêncio). Os testes de
/// ESCRITA (create→cancel de um pagamento não pago — não financeiro com cleanup, discovery.md §10
/// #3/#6) exigem opt-in adicional: <c>BEXS_SANDBOX_ALLOW_WRITE=1</c>. Nenhum teste imprime token,
/// secret, CPF ou payload completo — só status HTTP e resumo saneado.
/// </summary>
public sealed class BexsSandboxTests
{
    private readonly ITestOutputHelper output;

    public BexsSandboxTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task AuthenticatesLiveAgainstSandbox()
    {
        using var provider = BuildServiceProvider();
        var tokenProvider = provider.GetRequiredService<IBexsTokenProvider>();

        var (token, tokenType) = await tokenProvider.GetAccessTokenAsync(invalidatedToken: null);

        tokenType.ShouldBe("Bearer");
        token.ShouldNotBeNullOrWhiteSpace();
        output.WriteLine($"payin-package-sandbox: token issued, length={token.Length} (masked).");
    }

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task ExchangeRateReadsLive()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<BexsClient>();

        var response = await client.ExchangeRates.GetAsync();

        response.Quotes.ShouldNotBeNull();
        response.Quotes!.Count.ShouldBeGreaterThan(0);
        response.Quotes![0].Symbol.ShouldBe("USD");
        response.Quotes![0].Rate.ShouldNotBeNullOrWhiteSpace();

        // Valores do sandbox são dummy — validar forma, não plausibilidade (discovery.md §11.6).
        decimal.Parse(response.Quotes![0].Rate!, System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBeGreaterThan(0m);

        output.WriteLine($"GET exchange-rate: 200 OK, quotation_time={response.QuotationTime:O}, rate parseável.");
    }

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task PaymentGetWithFictitiousIdReturnsAuthenticatedDomain404()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<BexsClient>();

        // 404 de domínio autenticado ({"code":"7"}) prova acesso real ao recurso — o contraste
        // deliberado com o precedente BS2, onde tudo era 403 de provisionamento.
        var error = await Should.ThrowAsync<BexsApiException>(
            async () => await client.Payments.GetAsync("B-000000000000000000000000"));

        error.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        error.ErrorCode.ShouldBe("7");
        output.WriteLine($"GET payments/{{fictício}}: HTTP 404, code=7 — acesso real ao recurso confirmado.");
    }

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task MerchantsListAndDetailsReadLive()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<BexsClient>();

        var merchants = await client.Merchants.ListAsync();
        output.WriteLine($"GET merchants: 200 OK, {merchants.Count} merchant(s).");

        if (merchants.Count > 0 && !string.IsNullOrWhiteSpace(merchants[0].Id))
        {
            var merchant = await client.Merchants.GetAsync(merchants[0].Id!);
            merchant.Id.ShouldBe(merchants[0].Id);
            output.WriteLine($"GET merchants/{{id}}: 200 OK, locked={merchant.Locked}.");
        }
    }

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task MerchantGetWithFictitiousIdReturns404()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<BexsClient>();

        var error = await Should.ThrowAsync<BexsApiException>(
            async () => await client.Merchants.GetAsync("B-000000000000000000000000"));

        error.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        output.WriteLine("GET merchants/{fictício}: HTTP 404 — forma de erro sem campo code tolerada.");
    }

    /// <summary>
    /// E2E do fluxo payin sem dinheiro: cria um pagamento PIX (QR não pago — escrita não
    /// financeira), consulta os detalhes e cancela (reversão = cleanup). Caso permitido pelo goal
    /// §0.4 ("criação de recurso somente se o sandbox o exigir e houver cleanup/idempotência") e
    /// classificado na matriz do discovery.md §10 #3/#4/#6. Opt-in duplo: além das credenciais,
    /// exige <c>BEXS_SANDBOX_ALLOW_WRITE=1</c>.
    ///
    /// **Sensor de bloqueio externo (2026-07-15):** a criação está bloqueada por provisionamento
    /// do lado da Bexs — todos os 10 merchants da conta sandbox estão <c>locked</c> em compliance
    /// (create com <c>merchant_id</c> ⇒ <c>403 {"code":"6","message":"Merchant blocked"}</c>,
    /// confirmado individualmente nos 10) e o fluxo default sem <c>merchant_id</c> responde
    /// <c>422 {"code":"42","message":"Invalid event flow on state machine"}</c>. Não é bug do SDK
    /// (o mock de falha do legado documenta exatamente o code 6). Enquanto o bloqueio durar, o
    /// teste reporta o estado e passa; quando a Bexs aprovar um merchant, ele executa o
    /// round-trip completo com cleanup automaticamente. Ver discovery.md §10/§11.
    /// </summary>
    [Fact]
    [Trait("Category", "SandboxWrite")]
    public async Task PaymentCreateGetCancelRoundTripsLive()
    {
        if (Environment.GetEnvironmentVariable("BEXS_SANDBOX_ALLOW_WRITE") != "1")
        {
            output.WriteLine("BEXS_SANDBOX_ALLOW_WRITE != 1 — E2E de escrita pulado por design (rode com a variável para executar).");
            return;
        }

        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<BexsClient>();

        var correlationId = $"SDKE2E{DateTime.UtcNow:yyyyMMddHHmmss}";

        Payment created;

        try
        {
            created = await client.Payments.CreateAsync(new CreatePaymentRequest
            {
                SoftDescriptor = "CambioReal Inc",
                Amount = 10.00m,
                CorrelationId = correlationId,
                Consumer = new BexsConsumer
                {
                    ExternalId = "sdk-sandbox-e2e",
                    Email = "comprovante+sdke2e@cambioreal.com",
                    Type = BexsConsumerTypes.NaturalPerson,
                    FullName = "Sdk Sandbox E2E",
                    // CPF de teste válido por dígito verificador, público e não vinculado a pessoa real.
                    NationalId = "52998224725",
                },
            });
        }
        catch (BexsApiException exception) when (exception.ErrorCode is "6" or "42")
        {
            output.WriteLine(
                $"POST payments: HTTP {(int)exception.StatusCode}, code={exception.ErrorCode} — bloqueio de " +
                "provisionamento sandbox conhecido (merchants locked em compliance / fluxo default indisponível). " +
                "Quando a Bexs aprovar um merchant, este sensor executará o round-trip completo.");
            return;
        }

        created.Id.ShouldNotBeNullOrWhiteSpace();
        created.QrCode.ShouldNotBeNullOrWhiteSpace();
        output.WriteLine($"POST payments: criado, status={created.Status}, qr_code length={created.QrCode!.Length}.");

        Exception? detailsFailure = null;

        try
        {
            var details = await client.Payments.GetAsync(created.Id!);
            details.Id.ShouldBe(created.Id);
            details.CorrelationId.ShouldBe(correlationId);
            output.WriteLine($"GET payments/{{id}}: status={details.Status}.");
        }
        catch (Exception exception)
        {
            detailsFailure = exception;
        }

        // Cleanup SEMPRE, mesmo com o details falhando: reversão do QR não pago (não financeiro
        // — discovery.md §10 #6).
        try
        {
            var cancelled = await client.Payments.CancelAsync(created.Id!);
            output.WriteLine($"POST payments/{{id}}/cancel: ok, status={(cancelled?.Status ?? "(corpo vazio)")}.");
        }
        catch (BexsApiException exception)
        {
            output.WriteLine($"POST payments/{{id}}/cancel: HTTP {(int)exception.StatusCode} — {Truncate(exception.Message)}.");

            if (detailsFailure is null)
            {
                throw;
            }
        }

        if (detailsFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(detailsFailure).Throw();
        }
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var clientId = Environment.GetEnvironmentVariable("BEXS_SANDBOX_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("BEXS_SANDBOX_CLIENT_SECRET");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "BEXS_SANDBOX_CLIENT_ID/BEXS_SANDBOX_CLIENT_SECRET ausentes — carregue-os de " +
                "`pass cambio-real-v2/bexs/demo-env` antes de rodar este projeto. " +
                "Ver o comentário XML no topo de BexsSandboxTests.cs.");
        }

        var services = new ServiceCollection();
        services.AddBexsClient(options =>
        {
            options.Environment = BexsEnvironment.Sandbox;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
        });

        return services.BuildServiceProvider();
    }

    private static string Truncate(string? value, int maxLength = 120)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(sem descrição)";
        }

        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }
}
