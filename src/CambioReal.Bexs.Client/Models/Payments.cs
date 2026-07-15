namespace CambioReal.Bexs.Models;

/// <summary>
/// Corpo de <c>POST payments</c> — confirmado campo a campo no legado
/// (<c>PixRequest::create</c>). O QR code volta síncrono na resposta do próprio POST (diferente
/// da BS2, que exige polling do details).
/// </summary>
public sealed record CreatePaymentRequest
{
    /// <summary>Tipo do pagamento. Único valor confirmado: <see cref="BexsPaymentTypes.Pix"/>.</summary>
    public string Type { get; init; } = BexsPaymentTypes.Pix;

    /// <summary>Moeda nacional do pagamento. Único valor confirmado: <c>"BRL"</c>.</summary>
    public string Currency { get; init; } = "BRL";

    /// <summary>Descrição exibida ao pagador (<c>"CambioReal Inc"</c> no legado).</summary>
    public required string SoftDescriptor { get; init; }

    /// <summary>Valor em BRL.</summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Código externo da transação (ex.: <c>BR05248213768</c>) — a idempotência de negócio da
    /// Bexs vem deste campo; não há header de idempotência no produto (discovery.md §9).
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>Dados do pagador.</summary>
    public required BexsConsumer Consumer { get; init; }

    /// <summary>
    /// Id do submerchant. O legado só envia em PRODUÇÃO — no sandbox o merchant default da conta
    /// é usado e o campo deve ficar nulo (discovery.md §11.7).
    /// </summary>
    public string? MerchantId { get; init; }
}

/// <summary>
/// Pagador do PIX — <c>consumer</c>. Pessoa física usa <see cref="FullName"/> +
/// <see cref="NationalId"/> (CPF); pessoa jurídica usa <see cref="CommercialName"/> +
/// <see cref="DocumentNumber"/> (CNPJ). Confirmado no legado (<c>PixRequest::create</c>).
/// </summary>
public sealed record BexsConsumer
{
    /// <summary>Identificador do pagador no sistema do parceiro.</summary>
    public required string ExternalId { get; init; }

    /// <summary>E-mail do pagador.</summary>
    public required string Email { get; init; }

    /// <summary>
    /// Tipo de pessoa. Valores confirmados: <see cref="BexsConsumerTypes.NaturalPerson"/>,
    /// <see cref="BexsConsumerTypes.LegalPerson"/>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>Nome completo — pessoa física.</summary>
    public string? FullName { get; init; }

    /// <summary>CPF — pessoa física.</summary>
    public string? NationalId { get; init; }

    /// <summary>Razão social — pessoa jurídica.</summary>
    public string? CommercialName { get; init; }

    /// <summary>CNPJ — pessoa jurídica.</summary>
    public string? DocumentNumber { get; init; }
}

/// <summary>
/// Pagamento PIX — resposta de <c>POST payments</c> e de <c>GET payments/{id}</c>. Shape
/// confirmado nas fixtures do legado (<c>config/bexs-mock.php</c>) e nos payloads reais que o
/// <c>PaymentNotification::check</c> consome.
/// </summary>
public sealed record Payment
{
    /// <summary>Id do pagamento na Bexs (<c>"B-..."</c> nas fixtures).</summary>
    public string? Id { get; init; }

    /// <summary>
    /// Status corrente. Valores conhecidos em <see cref="BexsPaymentStatuses"/>; o conjunto é
    /// aberto — tratar valores desconhecidos como pendentes (fail-safe do legado).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>Tipo (<c>"PIX"</c>).</summary>
    public string? Type { get; init; }

    /// <summary>Código externo ecoado.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Payload EMV copia-e-cola do QR (não é URL de imagem).</summary>
    public string? QrCode { get; init; }

    /// <summary>
    /// Expiração informada pela Bexs. O legado a ignora e computa <c>now()+15min</c> client-side —
    /// política de consumidor, não do SDK (discovery.md §5).
    /// </summary>
    public DateTimeOffset? ExpirationDatetime { get; init; }

    /// <summary>Descrição exibida ao pagador.</summary>
    public string? SoftDescriptor { get; init; }

    /// <summary>Valores e taxas da operação de câmbio embutida.</summary>
    public BexsAmountInfo? AmountInfo { get; init; }

    /// <summary>Linha do tempo de eventos (<c>AUTHORIZATION</c> → <c>CONFIRMATION</c> → <c>SETTLEMENT</c>).</summary>
    public IReadOnlyList<BexsPaymentEvent>? Events { get; init; }
}

/// <summary>Bloco <c>amount_info</c> — valores em BRL + moeda estrangeira e taxas.</summary>
public sealed record BexsAmountInfo
{
    /// <summary>Valor bruto em BRL.</summary>
    public decimal GrossAmount { get; init; }

    /// <summary>Valor bruto na moeda estrangeira.</summary>
    public decimal ForeignGrossAmount { get; init; }

    /// <summary>IOF.</summary>
    public decimal FinancialTax { get; init; }

    /// <summary>Valor líquido na moeda estrangeira (presente nos details).</summary>
    public decimal? NetAmount { get; init; }

    /// <summary>Tarifa da Bexs.</summary>
    public decimal FeeAmount { get; init; }

    /// <summary>Outros impostos (presente nos details).</summary>
    public decimal? TaxAmount { get; init; }
}

/// <summary>Evento da linha do tempo do pagamento.</summary>
public sealed record BexsPaymentEvent
{
    public DateTimeOffset? Date { get; init; }
    public decimal Amount { get; init; }
    public decimal ForeignAmount { get; init; }

    /// <summary>Fase: <c>AUTHORIZATION</c>, <c>CONFIRMATION</c>, <c>SETTLEMENT</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Resultado da fase: <c>SUCCESS</c>, <c>PENDING</c>.</summary>
    public string? Status { get; init; }
}

/// <summary>Tipos de pagamento conhecidos.</summary>
public static class BexsPaymentTypes
{
    /// <summary>PIX — único tipo com request class no legado (nupay existiu só como fee config).</summary>
    public const string Pix = "PIX";
}

/// <summary>Tipos de pessoa do <c>consumer</c>.</summary>
public static class BexsConsumerTypes
{
    public const string NaturalPerson = "NATURAL_PERSON";
    public const string LegalPerson = "LEGAL_PERSON";
}

/// <summary>
/// Statuses de pagamento confirmados no legado (<c>PixRequest::getStatusPago</c> +
/// <c>PaymentNotification::check</c> + fixtures). O conjunto é aberto — valores fora desta lista
/// devem ser tratados como pendentes, replicando o fail-safe do legado (discovery.md §5).
/// </summary>
public static class BexsPaymentStatuses
{
    /// <summary>QR emitido, aguardando o pagador.</summary>
    public const string WaitingConsumer = "WAITING_CONSUMER";

    /// <summary>Pago/confirmado.</summary>
    public const string Confirmed = "CONFIRMED";

    /// <summary>Pago, em transferência — aceito como pago pelo legado.</summary>
    public const string Transference = "TRANSFERENCE";

    /// <summary>Cancelamento em andamento — transitório, re-poll.</summary>
    public const string WaitingCancelation = "WAITING_CANCELATION";

    /// <summary>Cancelado/reembolsado.</summary>
    public const string Canceled = "CANCELED";

    /// <summary>Recusado pelo emissor.</summary>
    public const string DeclinedByIssuer = "DECLINED_BY_ISSUER";

    /// <summary>Recusado por regra de negócio.</summary>
    public const string DeclinedByBusinessRules = "DECLINED_BY_BUSINESS_RULES";
}
