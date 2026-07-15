using System.Net;

namespace CambioReal.Bexs.Http;

/// <summary>Erro devolvido pela API Bexs.</summary>
public class BexsApiException : Exception
{
    /// <summary>Cria uma exceção sem contexto de resposta.</summary>
    public BexsApiException()
    {
    }

    /// <summary>Cria uma exceção com mensagem.</summary>
    public BexsApiException(string message)
        : base(message)
    {
    }

    /// <summary>Cria uma exceção com mensagem e causa.</summary>
    public BexsApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Cria uma exceção a partir de uma resposta da API.</summary>
    public BexsApiException(HttpStatusCode statusCode, string? errorCode, string message, string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ResponseBody = responseBody;
    }

    /// <summary>Status HTTP da resposta.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Código de erro Bexs extraído do corpo, quando presente. É uma string numérica curta
    /// (<c>"6"</c> = merchant blocked, <c>"7"</c> = not found — os únicos valores confirmados;
    /// não há catálogo público). Atenção: em alguns recursos o código vem embutido na
    /// <c>message</c> (<c>"7 - Merchant not found"</c>) em vez de num campo próprio — nesses
    /// casos este campo fica nulo e a mensagem carrega o texto integral (discovery.md §8).
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>Corpo bruto da resposta, para diagnóstico.</summary>
    public string? ResponseBody { get; }
}

/// <summary>A autenticação falhou mesmo após uma renovação de token.</summary>
public sealed class BexsAuthenticationException : BexsApiException
{
    /// <inheritdoc/>
    public BexsAuthenticationException()
    {
    }

    /// <inheritdoc/>
    public BexsAuthenticationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc/>
    public BexsAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public BexsAuthenticationException(HttpStatusCode statusCode, string? errorCode, string message, string? responseBody)
        : base(statusCode, errorCode, message, responseBody)
    {
    }
}
