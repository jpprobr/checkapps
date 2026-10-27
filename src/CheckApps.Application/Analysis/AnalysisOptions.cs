namespace CheckApps.Application.Analysis;

/// <summary>
/// Configuração da análise (seção <c>Analysis</c>). Fica em configuração local — o prefixo real dos pacotes da
/// empresa **nunca** entra em arquivo versionado nem na documentação.
/// </summary>
public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    /// <summary>Acima disso, só os primeiros arquivos de projeto são lidos e a análise fica parcial (nunca leitura em volume).</summary>
    public int MaxProjectFiles { get; set; } = 200;

    /// <summary>Arquivos maiores que isso não são lidos: aviso e análise parcial.</summary>
    public long MaxFileBytes { get; set; } = 512 * 1024;

    /// <summary>
    /// Prefixos de nome que marcam um pacote como interno (ex.: o nome da empresa). Calculado na leitura, não gravado.
    /// Sem nenhum, nada é marcado.
    /// </summary>
    public string[] InternalPackagePrefixes { get; set; } = [];

    /// <summary>
    /// Nomes de chave (último segmento) que guardam o endereço de uma integração (spec 014). Nulo = o padrão. É nulo por
    /// padrão de propósito: o binder do .NET **soma** itens de configuração a um array que já tem valor, em vez de
    /// substituí-lo; quem lê usa <see cref="EffectiveIntegrationKeyHints"/>.
    /// </summary>
    public string[]? IntegrationKeyHints { get; set; }

    public static readonly string[] DefaultIntegrationKeyHints = ["BaseUrl", "Url", "Endpoint", "Host"];

    public string[] EffectiveIntegrationKeyHints =>
        IntegrationKeyHints is { Length: > 0 } hints ? hints : DefaultIntegrationKeyHints;

    /// <summary>
    /// Apelidos de ambiente por categoria (<c>dev</c>, <c>hom</c>, <c>prod</c>) para os arquivos
    /// <c>appsettings.&lt;Nome&gt;.json</c>. Nulo = <see cref="EnvironmentNames.DefaultAliases"/> (mesmo motivo do binder).
    /// </summary>
    public Dictionary<string, string[]>? EnvironmentAliases { get; set; }

    public IReadOnlyDictionary<string, string[]> EffectiveEnvironmentAliases =>
        EnvironmentAliases is { Count: > 0 } aliases ? aliases : EnvironmentNames.DefaultAliases;
}
