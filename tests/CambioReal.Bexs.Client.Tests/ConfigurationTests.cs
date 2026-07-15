using CambioReal.Bexs.Tests.Fakes;
using Shouldly;
using Xunit;

namespace CambioReal.Bexs.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void ValidOptionsPassValidation()
        => Should.NotThrow(() => TestClient.NewOptions().Validate());

    [Theory]
    [InlineData("", "secret-1")]
    [InlineData("client-1", "")]
    public void MissingRequiredFieldThrows(string clientId, string clientSecret)
    {
        var options = TestClient.NewOptions();
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void BaseAddressWithoutTrailingSlashThrows()
    {
        // Uma URI só-host (sem path) é normalizada pelo próprio Uri para AbsolutePath == "/",
        // então o caso que exercita a checagem precisa de um path explícito sem barra final.
        var options = TestClient.NewOptions();
        options.BaseAddress = new Uri("https://sandbox.bexs.com.br/v1", UriKind.Absolute);

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void AuthBaseAddressWithoutTrailingSlashThrows()
    {
        var options = TestClient.NewOptions();
        options.AuthBaseAddress = new Uri("https://auth.bexs.com.br/v1", UriKind.Absolute);

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void SandboxResolvesToSandboxHost()
        => BexsEnvironment.Sandbox.GetBaseAddress().ToString().ShouldBe("https://sandbox.bexs.com.br/v1/");

    [Fact]
    public void ProductionResolvesToProductionHost()
        => BexsEnvironment.Production.GetBaseAddress().ToString().ShouldBe("https://apis.bexs.com.br/v1/");

    [Fact]
    public void AuthHostIsTheSameForBothEnvironments()
    {
        BexsEnvironment.Sandbox.GetAuthBaseAddress().ToString().ShouldBe("https://auth.bexs.com.br/v1/");
        BexsEnvironment.Production.GetAuthBaseAddress().ToString().ShouldBe("https://auth.bexs.com.br/v1/");
    }

    [Fact]
    public void AudienceDefaultsFromEnvironment()
    {
        var options = TestClient.NewOptions();
        options.ResolveAudience().ShouldBe("payin-package-sandbox");

        options.Environment = BexsEnvironment.Production;
        options.ResolveAudience().ShouldBe("https://forex.bexs.com.br");
    }

    [Fact]
    public void ExplicitAudienceWinsOverEnvironmentDefault()
    {
        var options = TestClient.NewOptions();
        options.Audience = "custom-audience";

        options.ResolveAudience().ShouldBe("custom-audience");
    }
}
