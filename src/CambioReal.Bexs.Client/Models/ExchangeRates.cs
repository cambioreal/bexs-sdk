namespace CambioReal.Bexs.Models;

/// <summary>
/// Resposta de <c>GET exchange-rate?from=&amp;to=</c> — validada ao vivo contra o sandbox
/// (2026-07-15): <c>{ quotation_time, quotes: [ { symbol, rate } ] }</c>.
/// </summary>
public sealed record ExchangeRateResponse
{
    /// <summary>Instante da cotação (ISO-8601 UTC).</summary>
    public DateTimeOffset? QuotationTime { get; init; }

    /// <summary>Cotações por símbolo.</summary>
    public IReadOnlyList<ExchangeRateQuote>? Quotes { get; init; }
}

/// <summary>Uma cotação.</summary>
public sealed record ExchangeRateQuote
{
    /// <summary>Símbolo da moeda de destino (<c>"USD"</c>).</summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Taxa como **string** (<c>"23.4840"</c>) — a Bexs serializa com precisão decimal preservada
    /// e o SDK não converte (regra do goal-loop: DTO representa o payload real). Consumidores
    /// convertem com <c>decimal.Parse(rate, CultureInfo.InvariantCulture)</c>. Cotação informativa,
    /// não é quote vinculante (sem id, sem TTL). Valores do sandbox são dummy — validar forma, não
    /// plausibilidade (discovery.md §6, §11.6).
    /// </summary>
    public string? Rate { get; init; }
}
