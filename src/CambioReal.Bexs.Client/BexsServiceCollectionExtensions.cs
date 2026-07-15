using CambioReal.Bexs.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CambioReal.Bexs;

/// <summary>Registro do cliente Bexs no container.</summary>
public static class BexsServiceCollectionExtensions
{
    /// <summary>
    /// Registra o cliente a partir de uma seção de configuração.
    /// </summary>
    /// <remarks>
    /// As credenciais precisam chegar por um provider seguro (variáveis de ambiente, user-secrets,
    /// Vault). Nunca versione <c>ClientId</c>/<c>ClientSecret</c> em <c>appsettings.json</c> — a
    /// fonte da verdade é o <c>pass</c>, <c>cambio-real-v2/bexs/demo-env</c>.
    /// </remarks>
    public static IServiceCollection AddBexsClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddBexsClient(configuration.Bind);
    }

    /// <summary>
    /// Registra <see cref="BexsClient"/>, o provedor de token e os dois pipelines HTTP: o da API
    /// (autenticado) e o de auth (sem handler — host separado, <c>auth.bexs.com.br</c>).
    /// </summary>
    public static IServiceCollection AddBexsClient(this IServiceCollection services, Action<BexsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddOptions<BexsOptions>().Validate(
            options =>
            {
                options.Validate();
                return true;
            },
            "A configuração do BexsOptions é inválida.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IBexsTokenProvider, BexsTokenProvider>();

        // Cliente exclusivo do POST token: sem handler de autenticação (para não recorrer) e com
        // base address próprio — a Bexs separa host de auth do host de API.
        services.AddHttpClient(BexsClientNames.Auth, ConfigureAuthTransport);

        services.AddHttpClient(BexsClientNames.Api, ConfigureApiTransport)
            .AddHttpMessageHandler(provider =>
                new BexsAuthenticationHandler(provider.GetRequiredService<IBexsTokenProvider>()));

        services.TryAddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new BexsClient(factory.CreateClient(BexsClientNames.Api));
        });

        return services;
    }

    private static void ConfigureApiTransport(IServiceProvider provider, HttpClient client)
    {
        var options = GetValidatedOptions(provider);

        client.BaseAddress = options.ResolveBaseAddress();
        client.Timeout = options.Timeout;
    }

    private static void ConfigureAuthTransport(IServiceProvider provider, HttpClient client)
    {
        var options = GetValidatedOptions(provider);

        client.BaseAddress = options.ResolveAuthBaseAddress();
        client.Timeout = options.Timeout;
    }

    private static BexsOptions GetValidatedOptions(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<BexsOptions>>().Value;
        options.Validate();
        return options;
    }
}
