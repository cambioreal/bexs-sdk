using System.Net;
using CambioReal.Bexs.Http;
using CambioReal.Bexs.Models;
using CambioReal.Bexs.Tests.Fakes;
using Shouldly;
using Xunit;

namespace CambioReal.Bexs.Tests;

public sealed class ResourceTests
{
    private static CreatePaymentRequest NewPaymentRequest() => new()
    {
        SoftDescriptor = "CambioReal Inc",
        Amount = 500.00m,
        CorrelationId = "BR05248213768",
        Consumer = new BexsConsumer
        {
            ExternalId = "12345",
            Email = "comprovante+12345@cambioreal.com",
            Type = BexsConsumerTypes.NaturalPerson,
            FullName = "Fulano de Tal",
            NationalId = "52998224725",
        },
    };

    [Fact]
    public async Task CreatePaymentPostsSnakeCaseBodyWithBearerToken()
    {
        var (client, transport) = TestClient.CreateOk("""
            {"id":"B-abc123","status":"WAITING_CONSUMER","qr_code":"00020126...","correlation_id":"BR05248213768"}
            """);

        var payment = await client.Payments.CreateAsync(NewPaymentRequest());

        payment.Id.ShouldBe("B-abc123");
        payment.Status.ShouldBe(BexsPaymentStatuses.WaitingConsumer);
        payment.QrCode.ShouldNotBeNullOrWhiteSpace();

        var request = transport.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("https://sandbox.bexs.com.br/v1/payments");
        request.Authorization.ShouldBe("Bearer tok-1");
        request.Body!.ShouldContain("\"correlation_id\":\"BR05248213768\"");
        request.Body!.ShouldContain("\"type\":\"PIX\"");
    }

    [Fact]
    public async Task GetPaymentHitsDetailsPath()
    {
        var (client, transport) = TestClient.CreateOk("""{"id":"B-abc123","status":"CONFIRMED"}""");

        var payment = await client.Payments.GetAsync("B-abc123");

        payment.Status.ShouldBe(BexsPaymentStatuses.Confirmed);
        transport.Requests.Single().RequestUri!.ToString()
            .ShouldBe("https://sandbox.bexs.com.br/v1/payments/B-abc123");
    }

    [Fact]
    public async Task PaymentIdIsEscapedInPath()
    {
        var (client, transport) = TestClient.CreateOk("""{"id":"x"}""");

        await client.Payments.GetAsync("a/b?c");

        transport.Requests.Single().RequestUri!.AbsoluteUri
            .ShouldBe("https://sandbox.bexs.com.br/v1/payments/a%2Fb%3Fc");
    }

    [Fact]
    public async Task CancelPaymentPostsWithoutBodyAndAcceptsEmptyResponse()
    {
        var (client, transport) = TestClient.Create((HttpStatusCode.OK, string.Empty));

        var result = await client.Payments.CancelAsync("B-abc123");

        result.ShouldBeNull();

        var request = transport.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("https://sandbox.bexs.com.br/v1/payments/B-abc123/cancel");

        // O legado não envia corpo nenhum no cancel (AbstractRequest::request pula payload vazio).
        request.Body.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task CancelPaymentParsesBodyWhenPresent()
    {
        var (client, _) = TestClient.CreateOk("""{"id":"B-abc123","status":"WAITING_CANCELATION"}""");

        var result = await client.Payments.CancelAsync("B-abc123");

        result.ShouldNotBeNull();
        result!.Status.ShouldBe(BexsPaymentStatuses.WaitingCancelation);
    }

    [Fact]
    public async Task ExchangeRateBuildsQueryAndParsesStringRate()
    {
        var (client, transport) = TestClient.CreateOk("""
            {"quotation_time":"2026-07-15T16:43:18Z","quotes":[{"symbol":"USD","rate":"23.4840"}]}
            """);

        var response = await client.ExchangeRates.GetAsync();

        response.Quotes![0].Rate.ShouldBe("23.4840");
        transport.Requests.Single().RequestUri!.ToString()
            .ShouldBe("https://sandbox.bexs.com.br/v1/exchange-rate?from=BRL&to=USD");
    }

    [Fact]
    public async Task MerchantsListParsesRootArray()
    {
        var (client, transport) = TestClient.CreateOk("""
            [{"id":"0b26f9efV1","locked":true,"company":{"trading_name":"CambioReal Inc"}}]
            """);

        var merchants = await client.Merchants.ListAsync();

        merchants.Count.ShouldBe(1);
        merchants[0].Id.ShouldBe("0b26f9efV1");
        merchants[0].Locked.ShouldBeTrue();
        transport.Requests.Single().RequestUri!.ToString()
            .ShouldBe("https://sandbox.bexs.com.br/v1/merchants");
    }

    [Fact]
    public async Task MerchantCreatePostsAndParsesId()
    {
        var (client, transport) = TestClient.CreateOk("""{"id":"B-new-merchant","locked":true}""");

        var merchant = await client.Merchants.CreateAsync(new CreateMerchantRequest
        {
            Document = "12-3456789",
            Company = new CreateMerchantCompany { Name = "X", TradingName = "X Inc" },
            FiscalAddress = new BexsFiscalAddress
            {
                Street = "S",
                Number = "1",
                City = "C",
                State = "DE",
                Country = "USA",
                ZipCode = "019713",
            },
        });

        merchant.Id.ShouldBe("B-new-merchant");
        transport.Requests.Single().Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task PaymentsNotFoundMapsDomainCodeAndMessage()
    {
        // Forma de erro real observada ao vivo (2026-07-15): {"code":"7","message":"Payment not found"}.
        var (client, _) = TestClient.Create((HttpStatusCode.NotFound, """{"code":"7","message":"Payment not found"}"""));

        var error = await Should.ThrowAsync<BexsApiException>(
            async () => await client.Payments.GetAsync("B-inexistente"));

        error.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        error.ErrorCode.ShouldBe("7");
        error.Message.ShouldContain("Payment not found");
    }

    [Fact]
    public async Task MerchantsNotFoundToleratesCodeEmbeddedInMessage()
    {
        // Forma de erro real dos merchants: sem campo code, código embutido na message.
        var (client, _) = TestClient.Create((HttpStatusCode.NotFound, """{"message":"7 - Merchant not found"}"""));

        var error = await Should.ThrowAsync<BexsApiException>(
            async () => await client.Merchants.GetAsync("B-inexistente"));

        error.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        error.ErrorCode.ShouldBeNull();
        error.Message.ShouldContain("Merchant not found");
    }

    [Fact]
    public async Task NonJsonErrorBodyStillThrowsWithStatus()
    {
        var (client, _) = TestClient.Create((HttpStatusCode.BadGateway, "<html>bad gateway</html>"));

        var error = await Should.ThrowAsync<BexsApiException>(
            async () => await client.Payments.GetAsync("B-x"));

        error.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        error.ResponseBody.ShouldNotBeNull();
        error.ResponseBody!.ShouldContain("bad gateway");
    }

    [Fact]
    public async Task UnauthorizedRetriesOnceWithRefreshedTokenThenSucceeds()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.Unauthorized, """{"message":"expired"}"""),
            (HttpStatusCode.OK, """{"id":"B-abc123","status":"CONFIRMED"}"""));

        var payment = await client.Payments.GetAsync("B-abc123");

        payment.Status.ShouldBe(BexsPaymentStatuses.Confirmed);

        // 1ª tentativa + retry único do BexsAuthenticationHandler.
        transport.Requests.Count.ShouldBe(2);
        transport.Requests[1].Authorization.ShouldBe("Bearer tok-1");
    }

    [Fact]
    public async Task UnauthorizedTwiceSurfacesAuthenticationException()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.Unauthorized, """{"message":"expired"}"""),
            (HttpStatusCode.Unauthorized, """{"message":"expired"}"""));

        await Should.ThrowAsync<BexsAuthenticationException>(
            async () => await client.Payments.GetAsync("B-abc123"));

        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CancellationTokenIsHonored()
    {
        var (client, _) = TestClient.CreateOk();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await client.Payments.GetAsync("B-abc123", cts.Token));
    }

    [Fact]
    public async Task EmptySuccessBodyWhereValueExpectedThrows()
    {
        var (client, _) = TestClient.Create((HttpStatusCode.OK, "null"));

        var error = await Should.ThrowAsync<BexsApiException>(
            async () => await client.Payments.GetAsync("B-abc123"));

        error.Message.ShouldContain("corpo JSON vazio");
    }
}
