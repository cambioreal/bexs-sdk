using System.Text.Json;
using System.Text.Json.Serialization;

namespace CambioReal.Bexs.Serialization;

/// <summary>Convenções de JSON da API Bexs Payin Package.</summary>
public static class BexsJson
{
    /// <summary>
    /// Nomes de campo em <c>snake_case</c> (<c>correlation_id</c>, <c>soft_descriptor</c>,
    /// <c>amount_info</c>, <c>foreign_gross_amount</c>, <c>qr_code</c>, <c>fiscal_address</c>, …),
    /// confirmado nos payloads reais do legado
    /// (<c>cerebro/app/Libraries/EnvioBr/Bexs/*.php</c> + <c>config/bexs-mock.php</c>) e nas
    /// respostas vivas do sandbox (2026-07-15).
    /// </summary>
    /// <remarks>
    /// Sem <see cref="JsonStringEnumConverter"/> global: apesar de os valores fechados observados
    /// serem UPPER_SNAKE uniformes (<c>WAITING_CONSUMER</c>, <c>NATURAL_PERSON</c>, …), o conjunto
    /// é aberto (statuses novos como <c>TRANSFERENCE</c> apareceram fora dos mocks) e não há
    /// documentação pública vigente do produto que prove o vocabulário completo (discovery.md §3).
    /// Campos de valor fechado são <see cref="string"/> simples em <c>Models/</c>, com os valores
    /// conhecidos em classes de constantes (<c>BexsPaymentStatuses</c>, <c>BexsConsumerTypes</c>) —
    /// mesma regra do goal-loop aplicada ao bs2-sdk.
    /// </remarks>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
