using System.Text.Json;
using CambioReal.Bexs.Models;
using CambioReal.Bexs.Serialization;
using Shouldly;
using Xunit;

namespace CambioReal.Bexs.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void CreatePaymentRequestSerializesInSnakeCaseWithDefaults()
    {
        var request = new CreatePaymentRequest
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

        var json = JsonSerializer.Serialize(request, BexsJson.Options);

        // Casing snake_case confirmado no legado (PixRequest::create).
        json.ShouldContain("\"type\":\"PIX\"");
        json.ShouldContain("\"currency\":\"BRL\"");
        json.ShouldContain("\"soft_descriptor\":\"CambioReal Inc\"");
        json.ShouldContain("\"amount\":500.00");
        json.ShouldContain("\"correlation_id\":\"BR05248213768\"");
        json.ShouldContain("\"external_id\":\"12345\"");
        json.ShouldContain("\"full_name\":\"Fulano de Tal\"");
        json.ShouldContain("\"national_id\":\"52998224725\"");
        json.ShouldContain("\"type\":\"NATURAL_PERSON\"");

        // Nulos omitidos: sem merchant_id (sandbox) e sem campos de pessoa jurídica.
        json.ShouldNotContain("merchant_id");
        json.ShouldNotContain("commercial_name");
        json.ShouldNotContain("document_number");
    }

    [Fact]
    public void LegalPersonConsumerSerializesCnpjFields()
    {
        var consumer = new BexsConsumer
        {
            ExternalId = "99",
            Email = "x@cambioreal.com",
            Type = BexsConsumerTypes.LegalPerson,
            CommercialName = "Empresa XYZ",
            DocumentNumber = "11222333000181",
        };

        var json = JsonSerializer.Serialize(consumer, BexsJson.Options);

        json.ShouldContain("\"commercial_name\":\"Empresa XYZ\"");
        json.ShouldContain("\"document_number\":\"11222333000181\"");
        json.ShouldNotContain("full_name");
        json.ShouldNotContain("national_id");
    }

    [Fact]
    public void PaymentDeserializesFromLegacyFixtureShape()
    {
        // Shape de config/bexs-mock.php (payment.pix.success) — fonte de verdade do legado.
        const string json = """
        {
            "id": "B-abc123",
            "status": "WAITING_CONSUMER",
            "type": "PIX",
            "correlation_id": "BR05248213768",
            "qr_code": "00020126580014br.gov.bcb.pix0136...",
            "expiration_datetime": "2023-05-25T20:36:11Z",
            "soft_descriptor": "CambioReal Inc",
            "amount_info": {
                "gross_amount": 500.00,
                "foreign_gross_amount": 24.50,
                "financial_tax": 0.38,
                "fee_amount": 0.80,
                "net_amount": 23.70,
                "tax_amount": 0.00000
            },
            "events": [
                { "date": "2023-05-24T20:36:11Z", "amount": 500.00, "foreign_amount": 24.50, "type": "AUTHORIZATION", "status": "PENDING" }
            ]
        }
        """;

        var payment = JsonSerializer.Deserialize<Payment>(json, BexsJson.Options)!;

        payment.Id.ShouldBe("B-abc123");
        payment.Status.ShouldBe(BexsPaymentStatuses.WaitingConsumer);
        payment.CorrelationId.ShouldBe("BR05248213768");
        payment.QrCode.ShouldStartWith("00020126580014br.gov.bcb.pix");
        payment.ExpirationDatetime.ShouldBe(new DateTimeOffset(2023, 5, 25, 20, 36, 11, TimeSpan.Zero));
        payment.AmountInfo.ShouldNotBeNull();
        payment.AmountInfo!.GrossAmount.ShouldBe(500.00m);
        payment.AmountInfo!.ForeignGrossAmount.ShouldBe(24.50m);
        payment.AmountInfo!.FinancialTax.ShouldBe(0.38m);
        payment.AmountInfo!.NetAmount.ShouldBe(23.70m);
        payment.Events.ShouldNotBeNull();
        payment.Events!.Count.ShouldBe(1);
        payment.Events![0].Type.ShouldBe("AUTHORIZATION");
        payment.Events![0].Status.ShouldBe("PENDING");
    }

    [Fact]
    public void ExchangeRateDeserializesRateAsString()
    {
        // Shape validado ao vivo contra o sandbox (2026-07-15): rate é string, precisão preservada.
        const string json = """
        {"quotation_time":"2026-07-15T16:43:18Z","quotes":[{"symbol":"USD","rate":"23.4840"}]}
        """;

        var response = JsonSerializer.Deserialize<ExchangeRateResponse>(json, BexsJson.Options)!;

        response.QuotationTime.ShouldBe(new DateTimeOffset(2026, 7, 15, 16, 43, 18, TimeSpan.Zero));
        response.Quotes.ShouldNotBeNull();
        response.Quotes!.Count.ShouldBe(1);
        response.Quotes![0].Symbol.ShouldBe("USD");
        response.Quotes![0].Rate.ShouldBe("23.4840");
        decimal.Parse(response.Quotes![0].Rate!, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(23.4840m);
    }

    [Fact]
    public void MerchantDeserializesFromLiveListShape()
    {
        // Shape real devolvido pela listagem viva do sandbox (2026-07-15), saneado.
        const string json = """
        {
            "id": "0b26f9ef47f14fdfa0609623683e024aV1",
            "locked": true,
            "logo": "",
            "document": "12-3123123",
            "company": {
                "name": "CambioReal",
                "trading_name": "CambioReal Inc",
                "state_registration_number": "",
                "state_registered": "",
                "foundation_date": "",
                "website_url": "https://www.cambioreal.com",
                "bacen_name": ""
            },
            "fiscal_address": {
                "street": "30 Prestbury Sq, Suite 323",
                "number": "30",
                "city": "Newark",
                "state": "DE",
                "country": "US",
                "zip_code": "19713"
            }
        }
        """;

        var merchant = JsonSerializer.Deserialize<Merchant>(json, BexsJson.Options)!;

        merchant.Id.ShouldBe("0b26f9ef47f14fdfa0609623683e024aV1");
        merchant.Locked.ShouldBeTrue();
        merchant.Company.ShouldNotBeNull();
        merchant.Company!.TradingName.ShouldBe("CambioReal Inc");
        merchant.Company!.WebsiteUrl.ShouldBe("https://www.cambioreal.com");
        merchant.FiscalAddress.ShouldNotBeNull();
        merchant.FiscalAddress!.ZipCode.ShouldBe("19713");
        merchant.FiscalAddress!.Country.ShouldBe("US");
    }

    [Fact]
    public void CreateMerchantRequestSerializesInSnakeCase()
    {
        var request = new CreateMerchantRequest
        {
            Document = "12-3456789",
            Company = new CreateMerchantCompany
            {
                Name = "CambioSubmerchant",
                TradingName = "Cambio Submerchant",
                WebsiteUrl = "https://example.com",
            },
            FiscalAddress = new BexsFiscalAddress
            {
                Street = "30 Prestbury Sq, Suite 323",
                Number = "30",
                City = "Newark",
                State = "DE",
                Country = "USA",
                ZipCode = "019713",
            },
        };

        var json = JsonSerializer.Serialize(request, BexsJson.Options);

        json.ShouldContain("\"document\":\"12-3456789\"");
        json.ShouldContain("\"trading_name\":\"Cambio Submerchant\"");
        json.ShouldContain("\"website_url\":\"https://example.com\"");
        json.ShouldContain("\"fiscal_address\":");
        json.ShouldContain("\"zip_code\":\"019713\"");
    }

    [Fact]
    public void UnknownStatusValuesSurviveDeserialization()
    {
        // O conjunto de statuses é aberto (discovery.md §5) — um valor novo não pode quebrar o parse.
        const string json = """{"id":"B-1","status":"SOME_FUTURE_STATUS"}""";

        var payment = JsonSerializer.Deserialize<Payment>(json, BexsJson.Options)!;

        payment.Status.ShouldBe("SOME_FUTURE_STATUS");
    }
}
