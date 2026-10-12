namespace CheckApps.Application.Discovery;

/// <summary>
/// Configuração do scan (seção <c>Scan</c>). Fica em configuração local — nunca em arquivo
/// versionado: a convenção de nome e os formatos são do ambiente, não do produto.
/// </summary>
public class ScanOptions
{
    public const string SectionName = "Scan";

    /// <summary>
    /// Convenção de nome dos repositórios, com <c>{sigla}</c> (ex.: <c>{prefixo}-{sigla}-</c>).
    /// Sem ela o scan não roda; a triagem funciona, só sem sugestões.
    /// </summary>
    public string? NameTemplate { get; set; }

    /// <summary>Formato válido de uma sigla. Vazia ou fora disso, o scan é recusado antes de qualquer consulta.</summary>
    public string SiglaPattern { get; set; } = "^[a-z0-9]{3}$";

    /// <summary>Acima disso, a busca é recusada sem ler nada além da contagem.</summary>
    public int MaxResults { get; set; } = 50;
}
