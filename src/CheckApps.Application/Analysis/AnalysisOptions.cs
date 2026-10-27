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
}
