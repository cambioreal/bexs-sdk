using CambioReal.Bexs.Models;

namespace CambioReal.Bexs.Resources;

/// <summary>Submerchants. <c>merchants</c>.</summary>
public sealed class MerchantsResource
{
    private readonly BexsClient client;

    internal MerchantsResource(BexsClient client) => this.client = client;

    /// <summary>
    /// Solicita o cadastro de um submerchant. <c>POST merchants</c>. O cadastro entra em análise
    /// de compliance da Bexs (<see cref="Merchant.Locked"/>/status <c>ANALYSING</c> até aprovação)
    /// e NÃO há endpoint de remoção — sem cleanup, não executar contra sandbox por padrão
    /// (discovery.md §10 #8).
    /// </summary>
    public Task<Merchant> CreateAsync(CreateMerchantRequest request, CancellationToken cancellationToken = default) =>
        client.PostAsync<CreateMerchantRequest, Merchant>(BexsPaths.Merchants, request, cancellationToken);

    /// <summary>Consulta um merchant. <c>GET merchants/{id}</c>.</summary>
    public Task<Merchant> GetAsync(string merchantId, CancellationToken cancellationToken = default) =>
        client.GetAsync<Merchant>(BexsPaths.Merchant(merchantId), cancellationToken);

    /// <summary>
    /// Lista os merchants da conta. <c>GET merchants</c> — endpoint validado ao vivo (2026-07-15;
    /// array na raiz, sem paginação observada), não usado pelo legado.
    /// </summary>
    public Task<IReadOnlyList<Merchant>> ListAsync(CancellationToken cancellationToken = default) =>
        client.GetAsync<IReadOnlyList<Merchant>>(BexsPaths.Merchants, cancellationToken);
}
