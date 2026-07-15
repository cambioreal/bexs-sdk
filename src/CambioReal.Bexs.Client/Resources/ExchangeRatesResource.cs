using CambioReal.Bexs.Models;

namespace CambioReal.Bexs.Resources;

/// <summary>Cotações informativas. <c>exchange-rate</c>.</summary>
public sealed class ExchangeRatesResource
{
    private readonly BexsClient client;

    internal ExchangeRatesResource(BexsClient client) => this.client = client;

    /// <summary>
    /// Consulta a cotação corrente. <c>GET exchange-rate?from=&amp;to=</c> — validado ao vivo
    /// (2026-07-15). Não é quote vinculante (sem id, sem TTL); <see cref="ExchangeRateQuote.Rate"/>
    /// é string com precisão preservada. O legado só usa <c>from=BRL&amp;to=USD</c>.
    /// </summary>
    public Task<ExchangeRateResponse> GetAsync(
        string fromCurrency = "BRL", string toCurrency = "USD", CancellationToken cancellationToken = default) =>
        client.GetAsync<ExchangeRateResponse>(BexsPaths.ExchangeRate(fromCurrency, toCurrency), cancellationToken);
}
