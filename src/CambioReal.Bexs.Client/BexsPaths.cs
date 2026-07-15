namespace CambioReal.Bexs;

/// <summary>
/// Paths centralizados da API Bexs Payin Package. Cada membro cita o arquivo do legado PHP onde
/// foi confirmado (<c>cerebro/app/Libraries/EnvioBr/Bexs/*</c>). Todos relativos aos base
/// addresses (<c>.../v1/</c>) resolvidos por <see cref="BexsOptions"/>.
/// </summary>
internal static class BexsPaths
{
    /// <summary>
    /// <c>POST {auth}/v1/token</c> — <c>AbstractRequest::authenticate()</c>. Nota do
    /// <c>PROVIDER-MAP.md</c>: o path correto é <c>/v1/token</c>, não <c>/oauth/token</c> (o
    /// <c>oauth/token</c> é da API nova da Ebury, onde esta credencial não vale — discovery.md §3).
    /// </summary>
    public const string Token = "token";

    /// <summary><c>POST payments</c> — <c>AbstractRequest::pay()</c>.</summary>
    public const string Payments = "payments";

    /// <summary><c>GET payments/{id}</c> — <c>PixRequest::details</c>.</summary>
    public static string Payment(string paymentId) =>
        $"payments/{Uri.EscapeDataString(paymentId)}";

    /// <summary><c>POST payments/{id}/cancel</c> (sem corpo) — <c>AbstractRequest::cancel()</c>.</summary>
    public static string PaymentCancel(string paymentId) =>
        $"payments/{Uri.EscapeDataString(paymentId)}/cancel";

    /// <summary><c>GET exchange-rate?from=&amp;to=</c> — <c>ExchangeRateRequest::exchangeRate()</c>.</summary>
    public static string ExchangeRate(string fromCurrency, string toCurrency) =>
        $"exchange-rate?from={Uri.EscapeDataString(fromCurrency)}&to={Uri.EscapeDataString(toCurrency)}";

    /// <summary>
    /// <c>POST merchants</c> — <c>MerchantsRequest::create</c>; <c>GET merchants</c> (listagem)
    /// validada ao vivo em 2026-07-15 (não usada pelo legado — discovery.md §6).
    /// </summary>
    public const string Merchants = "merchants";

    /// <summary><c>GET merchants/{id}</c> — <c>MerchantsRequest::details</c>.</summary>
    public static string Merchant(string merchantId) =>
        $"merchants/{Uri.EscapeDataString(merchantId)}";
}
