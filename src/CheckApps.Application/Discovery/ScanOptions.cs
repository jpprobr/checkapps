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

    /// <summary>De onde vem a leitura: <c>github</c> (padrão) ou <c>fake</c> (demonstração, sem rede nem token). O <c>--source</c> do CLI o sobrepõe.</summary>
    public string Source { get; set; } = "github";

    /// <summary>Só na fonte fake da análise: qual versão do contrato de demonstração servir (spec 021).</summary>
    public int FakeRevision { get; set; }

    /// <summary>
    /// Liga o endpoint de scan da **API** (desligado por padrão: sem autenticação, quem abre o sistema
    /// poderia disparar scans com o token configurado). A ferramenta de linha de comando ignora isto.
    /// </summary>
    public bool Enabled { get; set; }
}
