namespace CambioReal.Bexs.Models;

/// <summary>
/// Corpo de <c>POST merchants</c> — confirmado no legado
/// (<c>EmpresaApiController::postCreateBexsMerchant</c>). O cadastro entra em análise de
/// compliance na Bexs; não há endpoint de remoção (sem cleanup — discovery.md §6).
/// </summary>
public sealed record CreateMerchantRequest
{
    /// <summary>Documento fiscal do merchant (EIN/tax id — <c>usDocument</c> no legado).</summary>
    public required string Document { get; init; }

    /// <summary>Dados da empresa.</summary>
    public required CreateMerchantCompany Company { get; init; }

    /// <summary>Endereço fiscal.</summary>
    public required BexsFiscalAddress FiscalAddress { get; init; }
}

/// <summary>Bloco <c>company</c> do cadastro — só os 3 campos que o legado envia.</summary>
public sealed record CreateMerchantCompany
{
    public required string Name { get; init; }
    public required string TradingName { get; init; }
    public string? WebsiteUrl { get; init; }
}

/// <summary>
/// Merchant — resposta de <c>POST merchants</c>, <c>GET merchants/{id}</c> e itens de
/// <c>GET merchants</c> (listagem validada ao vivo em 2026-07-15).
/// </summary>
public sealed record Merchant
{
    /// <summary>Id do merchant (ao vivo: hex com sufixo <c>V1</c>, sem prefixo <c>B-</c>).</summary>
    public string? Id { get; init; }

    /// <summary>Merchant travado (em análise/bloqueado para transacionar).</summary>
    public bool Locked { get; init; }

    /// <summary>URL do logo (pode ser vazio).</summary>
    public string? Logo { get; init; }

    /// <summary>Documento fiscal.</summary>
    public string? Document { get; init; }

    /// <summary>Dados da empresa.</summary>
    public BexsMerchantCompany? Company { get; init; }

    /// <summary>Endereço fiscal.</summary>
    public BexsFiscalAddress? FiscalAddress { get; init; }
}

/// <summary>Bloco <c>company</c> completo devolvido pela Bexs.</summary>
public sealed record BexsMerchantCompany
{
    public string? Name { get; init; }
    public string? TradingName { get; init; }
    public string? StateRegistrationNumber { get; init; }
    public string? StateRegistered { get; init; }
    public string? FoundationDate { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? BacenName { get; init; }
}

/// <summary>Endereço fiscal — <c>fiscal_address</c>.</summary>
public sealed record BexsFiscalAddress
{
    /// <summary>Logradouro — o legado trunca em 30 caracteres ao criar.</summary>
    public required string Street { get; init; }

    public required string Number { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }

    /// <summary>País (<c>"USA"</c> no fluxo do legado; a listagem viva devolve <c>"US"</c>).</summary>
    public required string Country { get; init; }

    public required string ZipCode { get; init; }
}
