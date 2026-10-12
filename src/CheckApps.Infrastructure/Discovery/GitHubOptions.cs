namespace CheckApps.Infrastructure.Discovery;

/// <summary>
/// Configuração do GitHub (seção <c>GitHub</c>), sempre em configuração local — <c>dotnet user-secrets</c>
/// ou variáveis de ambiente (<c>GitHub__Token</c>). O token nunca vai para um arquivo versionado.
/// </summary>
public class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>Organização (ou conta) cujos repositórios são lidos.</summary>
    public string? Organization { get; set; }

    /// <summary>Token fine-grained somente leitura (Metadata e Contents: leitura).</summary>
    public string? Token { get; set; }

    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>Caminho, dentro de cada repositório, do arquivo YAML que traz a sigla-app.</summary>
    public string ConfigFilePath { get; set; } = "config.yaml";

    /// <summary>Chave do YAML com a sigla-app (sem diferenciar caixa).</summary>
    public string SiglaAppKey { get; set; } = "siglaApp";
}
