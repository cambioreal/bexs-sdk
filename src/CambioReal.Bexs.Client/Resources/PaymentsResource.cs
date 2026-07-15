using CambioReal.Bexs.Models;

namespace CambioReal.Bexs.Resources;

/// <summary>Pagamentos PIX payin. <c>payments</c>.</summary>
public sealed class PaymentsResource
{
    private readonly BexsClient client;

    internal PaymentsResource(BexsClient client) => this.client = client;

    /// <summary>
    /// Cria um pagamento PIX. <c>POST payments</c>. A resposta já traz o QR code
    /// (<see cref="Payment.QrCode"/>) sincronamente — confirmado no legado
    /// (<c>PixRequest::create</c>), sem polling.
    /// Efeito: escrita não financeira enquanto o QR não é pago (nada liquida sem pagador);
    /// cleanup canônico é <see cref="CancelAsync"/> antes do pagamento (discovery.md §10 #3).
    /// </summary>
    public Task<Payment> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default) =>
        client.PostAsync<CreatePaymentRequest, Payment>(BexsPaths.Payments, request, cancellationToken);

    /// <summary>
    /// Consulta detalhes/status de um pagamento. <c>GET payments/{id}</c>. É a ÚNICA fonte de
    /// verdade de status confirmada do produto — o legado sempre re-consulta aqui antes de
    /// confirmar qualquer notificação (discovery.md §7).
    /// </summary>
    public Task<Payment> GetAsync(string paymentId, CancellationToken cancellationToken = default) =>
        client.GetAsync<Payment>(BexsPaths.Payment(paymentId), cancellationToken);

    /// <summary>
    /// Cancela/reembolsa um pagamento. <c>POST payments/{id}/cancel</c>, sem corpo.
    /// Semântica dependente do estado: não pago ⇒ reversão do QR (não financeiro, é o cleanup do
    /// <see cref="CreateAsync"/>); pago ⇒ REEMBOLSO (financeiro — não executar contra sandbox/
    /// produção sem autorização explícita; discovery.md §10 #7).
    /// A resposta de sucesso não tem shape fixado em fixture — pode vir vazia
    /// (⇒ <see langword="null"/>) ou com o pagamento atualizado.
    /// </summary>
    public Task<Payment?> CancelAsync(string paymentId, CancellationToken cancellationToken = default) =>
        client.PostWithoutBodyAsync<Payment>(BexsPaths.PaymentCancel(paymentId), cancellationToken);
}
