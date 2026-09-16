using CheckApps.Domain;

namespace CheckApps.UnitTests;

public class SlugTests
{
    [Theory]
    [InlineData("checkout-api", "checkout-api")]
    [InlineData("Checkout API", "checkout-api")]
    [InlineData("Squad Pagamentos", "squad-pagamentos")]
    [InlineData("Autenticação e Notificações", "autenticacao-e-notificacoes")]
    [InlineData("  Espaços   Extras  ", "espacos-extras")]
    [InlineData("app_v2.0!", "app-v2-0")]
    public void Generate_ProducesUrlSafeLowercaseSlug(string input, string expected)
    {
        Assert.Equal(expected, Slug.Generate(input));
    }
}
