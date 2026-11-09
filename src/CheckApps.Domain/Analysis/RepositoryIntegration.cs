namespace CheckApps.Domain.Analysis;

/// <summary>
/// Uma integração **declarada** vista numa análise (spec 014): "este repositório chama o host X", com a evidência
/// (arquivo e chave de configuração). Pertence à mesma <see cref="RepositoryAnalysis"/> dos pacotes. Só guarda as partes
/// normalizadas do endereço: nunca o valor bruto, e nunca usuário, senha ou query (a redação é do núcleo).
/// </summary>
public sealed record RepositoryIntegration : IAnalysisFact
{
    public required string Id { get; init; }
    public required string AnalysisId { get; init; }

    /// <summary>Um de <see cref="IntegrationMechanisms"/>.</summary>
    public required string Mechanism { get; init; }

    /// <summary><c>http</c> ou <c>https</c>. Nulo quando a integração não foi resolvida.</summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// Host em minúsculas. Pode ser um **modelo** (<c>empresa.{env}.exemplo.com</c>) quando o ambiente é parametrizado;
    /// nulo quando a integração não foi resolvida.
    /// </summary>
    public string? Host { get; init; }

    /// <summary>Porta explícita; nula quando é a padrão do esquema ou quando não há host.</summary>
    public int? Port { get; init; }

    /// <summary>Caminho base, sem barra final (ex.: <c>/pagamentos</c>). Nulo se não há.</summary>
    public string? BasePath { get; init; }

    /// <summary>Seção de configuração (ex.: <c>IPagamentosServiceCliente</c>), usada na spec 015 para ligar à interface Refit.</summary>
    public required string Reference { get; init; }

    /// <summary>Chave completa da evidência (ex.: <c>IPagamentosServiceCliente:BaseUrl</c>).</summary>
    public required string ConfigKey { get; init; }

    /// <summary>Nome do ambiente como está no arquivo (<c>Homolog</c>). Nulo no arquivo base.</summary>
    public string? Environment { get; init; }

    /// <summary>Um de <see cref="EnvironmentKinds"/>, ou nulo se o nome do ambiente não é conhecido (nunca palpite).</summary>
    public string? EnvironmentKind { get; init; }

    /// <summary>Um de <see cref="IntegrationStates"/>.</summary>
    public required string State { get; init; }

    /// <summary>Diretório do projeto de origem (o <c>.csproj</c> mais próximo), relativo à raiz do repositório.</summary>
    public required string ProjectPath { get; init; }

    public required string SourceFile { get; init; }

    /// <summary>Um de <see cref="FactOrigins"/>.</summary>
    public string Origin { get; init; } = FactOrigins.Parser;

    /// <summary>Um de <see cref="Confidences"/>.</summary>
    public required string Confidence { get; init; }

    /// <summary>Em texto, o motivo de a confiança não ser alta.</summary>
    public string? ConfidenceNote { get; init; }

    public required string ScannerId { get; init; }
    public required string ScannerVersion { get; init; }
}

/// <summary>Como a integração foi declarada. Hoje só configuração; <c>refit</c> e <c>httpclient</c> entram na spec 015.</summary>
public static class IntegrationMechanisms
{
    public const string Config = "config";
}

/// <summary>O quanto o endereço foi resolvido.</summary>
public static class IntegrationStates
{
    /// <summary>Host concreto lido do arquivo.</summary>
    public const string Resolved = "resolvida";

    /// <summary>O host depende do ambiente (<c>{env}</c>): guardado como modelo, sem substituir por palpite.</summary>
    public const string Parameterized = "parametrizada";

    /// <summary>Valor vazio ou com referência de substituição (<c>${VAR}</c>): não há host a gravar.</summary>
    public const string Unresolved = "nao-resolvida";
}

/// <summary>Categoria normalizada do ambiente de um arquivo <c>appsettings.&lt;Nome&gt;.json</c>.</summary>
public static class EnvironmentKinds
{
    public const string Dev = "dev";
    public const string Hom = "hom";
    public const string Prod = "prod";
}
